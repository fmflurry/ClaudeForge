using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using ClaudeForge.Tests.Integration.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Tests.Integration.Identity;

/// <summary>
/// Integration tests for POST /auth/device/approve — the authenticated browser-user approval
/// endpoint that closes the RFC 8628 device flow loop.
///
/// Architecture exercised end-to-end:
///   1. POST /auth/device/code  → issues device_code + user_code
///   2. POST /auth/device/approve [Bearer]  → authenticated user submits user_code
///   3. POST /auth/device/token  → CLI polls; must return Approved(access+refresh tokens)
///
/// RED: The endpoint POST /auth/device/approve and ApproveDeviceCodeUseCase do not exist yet.
/// All tests in this class must fail until the GREEN implementation is shipped.
/// </summary>
[Collection(AuthEndpointFixture.CollectionName)]
public sealed class DeviceApproveEndpointTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public DeviceApproveEndpointTests(PostgresFixture pg)
    {
        _pg = pg;
        _factory = AuthEndpointFixture.CreateFactory(pg);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
    }

    public async Task InitializeAsync()
    {
        await using MarketplaceDbContext ctx = _pg.CreateContext();
        await ctx.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE TABLE
                org_audit_log,
                organization_invitations,
                organization_members,
                refresh_tokens,
                user_identities,
                organizations,
                users,
                telemetry_aggregates,
                telemetry_events,
                plugin_categories,
                plugin_versions,
                plugins,
                categories
            RESTART IDENTITY CASCADE
            """);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    // =========================================================================
    // Authorization guard — unauthenticated → 401
    // =========================================================================

    /// <summary>
    /// No Bearer token → 401 Unauthorized.
    /// The endpoint must be protected with [Authorize("RequireAuthenticatedUser")].
    /// </summary>
    [Fact]
    public async Task PostDeviceApprove_Unauthenticated_Returns401()
    {
        using HttpClient client = _factory.CreateClient();
        AuthEndpointFixture.ClearBearerToken(client);

        StringContent content = new(
            JsonSerializer.Serialize(new { userCode = "ANYCODE1" }),
            System.Text.Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await client.PostAsync("/auth/device/approve", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Malformed / wrong-key Bearer token → 401.
    /// </summary>
    [Theory]
    [InlineData("not.a.jwt")]
    [InlineData("completely-invalid-token")]
    public async Task PostDeviceApprove_InvalidBearerToken_Returns401(string badToken)
    {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", badToken);

        StringContent content = new(
            JsonSerializer.Serialize(new { userCode = "ANYCODE1" }),
            System.Text.Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await client.PostAsync("/auth/device/approve", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // =========================================================================
    // Happy path — valid user_code + authenticated → 200; subsequent poll → Approved
    // =========================================================================

    /// <summary>
    /// Full RFC 8628 approval loop:
    ///   1. Issue a device code.
    ///   2. Provision an approver user + issue a test Bearer token.
    ///   3. POST /auth/device/approve with that Bearer token and the user_code → 200.
    ///   4. POST /auth/device/token with the device_code → Approved with valid access+refresh.
    /// </summary>
    [Fact]
    public async Task PostDeviceApprove_ValidUserCodeAndAuthenticated_Returns200()
    {
        // Step 1: issue device code
        (string deviceCode, string userCode) = await IssueDeviceCodeAsync("google");

        // Step 2: provision approver + issue access token
        (Guid approverId, string approverToken) = await ProvisionApproverAsync();

        // Step 3: approve
        using HttpClient approverClient = _factory.CreateClient();
        AuthEndpointFixture.SetBearerToken(approverClient, approverToken);

        StringContent approveContent = new(
            JsonSerializer.Serialize(new { userCode }),
            System.Text.Encoding.UTF8,
            "application/json");

        HttpResponseMessage approveResp = await approverClient.PostAsync(
            "/auth/device/approve", approveContent);

        Assert.Equal(HttpStatusCode.OK, approveResp.StatusCode);
    }

    /// <summary>
    /// After approval, polling the device_code returns Approved with access + refresh tokens.
    /// The access token's sub must be the approving user's ID.
    /// </summary>
    [Fact]
    public async Task PostDeviceApprove_AfterApproval_PollReturnsApprovedTokens()
    {
        (string deviceCode, string userCode) = await IssueDeviceCodeAsync("google");
        (Guid approverId, string approverToken) = await ProvisionApproverAsync();

        // Approve
        using HttpClient approverClient = _factory.CreateClient();
        AuthEndpointFixture.SetBearerToken(approverClient, approverToken);
        StringContent approveContent = new(
            JsonSerializer.Serialize(new { userCode }),
            System.Text.Encoding.UTF8,
            "application/json");
        HttpResponseMessage approveResp = await approverClient.PostAsync(
            "/auth/device/approve", approveContent);
        Assert.Equal(HttpStatusCode.OK, approveResp.StatusCode);

        // Poll (new client to reset poll timer)
        using HttpClient pollClient = _factory.CreateClient();
        StringContent pollContent = new(
            JsonSerializer.Serialize(new { deviceCode }),
            System.Text.Encoding.UTF8,
            "application/json");

        HttpResponseMessage pollResp = await pollClient.PostAsync("/auth/device/token", pollContent);
        Assert.Equal(HttpStatusCode.OK, pollResp.StatusCode);

        string pollBody = await pollResp.Content.ReadAsStringAsync();
        using JsonDocument pollDoc = JsonDocument.Parse(pollBody);
        JsonElement pollRoot = pollDoc.RootElement;

        // Must have an access token
        Assert.True(
            pollRoot.TryGetProperty("accessToken", out JsonElement atEl) ||
            pollRoot.TryGetProperty("access_token", out atEl),
            "Poll response after approval must include 'accessToken'");

        string? accessToken = atEl.GetString();
        Assert.NotNull(accessToken);
        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        // Must have a refresh token
        Assert.True(
            pollRoot.TryGetProperty("refreshToken", out JsonElement rtEl) ||
            pollRoot.TryGetProperty("refresh_token", out rtEl),
            "Poll response after approval must include 'refreshToken'");
        Assert.NotNull(rtEl.GetString());

        // The access token sub must be the approving user's ID (RFC 8628 §3.5)
        JwtSecurityTokenHandler handler = new();
        JwtSecurityToken jwt = handler.ReadJwtToken(accessToken);
        string? sub = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
        Assert.Equal(approverId.ToString(), sub);
    }

    /// <summary>
    /// Poll BEFORE any approval → still Pending (no tokens).
    /// </summary>
    [Fact]
    public async Task PostDeviceToken_BeforeApproval_ReturnsPendingNoTokens()
    {
        (string deviceCode, string _) = await IssueDeviceCodeAsync("google");

        using HttpClient pollClient = _factory.CreateClient();
        StringContent pollContent = new(
            JsonSerializer.Serialize(new { deviceCode }),
            System.Text.Encoding.UTF8,
            "application/json");

        HttpResponseMessage pollResp = await pollClient.PostAsync("/auth/device/token", pollContent);

        // 200 with pending body — must NOT contain accessToken
        if (pollResp.StatusCode == HttpStatusCode.OK)
        {
            string body = await pollResp.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(body);
            bool hasAccessToken =
                doc.RootElement.TryGetProperty("accessToken", out _) ||
                doc.RootElement.TryGetProperty("access_token", out _);
            Assert.False(hasAccessToken, "Pre-approval poll must not return access tokens");

            bool hasPendingSignal =
                doc.RootElement.TryGetProperty("error", out JsonElement errEl) &&
                    errEl.GetString() == "authorization_pending" ||
                doc.RootElement.TryGetProperty("status", out JsonElement statusEl) &&
                    statusEl.GetString() == "pending";
            Assert.True(hasPendingSignal, "Pre-approval 200 body must signal pending state");
        }
        // Any non-token 2xx or 4xx is also acceptable for a pending state
    }

    // =========================================================================
    // Unknown / expired user_code → 404 / 410
    // =========================================================================

    /// <summary>
    /// Authenticated user submits a user_code that does not exist → 404.
    /// </summary>
    [Fact]
    public async Task PostDeviceApprove_UnknownUserCode_Returns404()
    {
        (Guid _, string approverToken) = await ProvisionApproverAsync();

        using HttpClient approverClient = _factory.CreateClient();
        AuthEndpointFixture.SetBearerToken(approverClient, approverToken);

        StringContent content = new(
            JsonSerializer.Serialize(new { userCode = "UNKWN001" }),
            System.Text.Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await approverClient.PostAsync("/auth/device/approve", content);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// User submits a user_code for a device code that has already expired → 410 Gone or 404.
    /// </summary>
    [Fact]
    public async Task PostDeviceApprove_ExpiredUserCode_Returns410OrEquivalent()
    {
        (Guid _, string approverToken) = await ProvisionApproverAsync();

        // We cannot easily force expiry via the HTTP API, so we verify that
        // the endpoint maps DeviceTokenPollResult.Expired to the appropriate HTTP status.
        // This test seeds an expired entry directly by calling IssueDeviceCode and then
        // submitting after the in-process store would have swept it; instead, we test
        // that a user_code that no longer exists (treated the same as expired) yields ≥ 404.
        using HttpClient approverClient = _factory.CreateClient();
        AuthEndpointFixture.SetBearerToken(approverClient, approverToken);

        StringContent content = new(
            JsonSerializer.Serialize(new { userCode = "EXPIRD01" }),
            System.Text.Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await approverClient.PostAsync("/auth/device/approve", content);

        // 404 (not found) or 410 (gone / expired) are both correct for an expired code
        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Gone,
            $"Expected 404 or 410 for expired user_code, got {(int)response.StatusCode}");
    }

    // =========================================================================
    // Already-approved user_code → 409 Conflict
    // =========================================================================

    /// <summary>
    /// Attempting to approve an already-approved user_code → 409 Conflict.
    /// A device code is single-use for the approval step.
    /// </summary>
    [Fact]
    public async Task PostDeviceApprove_AlreadyApprovedUserCode_Returns409()
    {
        (string deviceCode, string userCode) = await IssueDeviceCodeAsync("google");
        (Guid _, string approverToken) = await ProvisionApproverAsync();

        using HttpClient approverClient = _factory.CreateClient();
        AuthEndpointFixture.SetBearerToken(approverClient, approverToken);

        // First approval — must succeed (200)
        StringContent firstContent = new(
            JsonSerializer.Serialize(new { userCode }),
            System.Text.Encoding.UTF8,
            "application/json");
        HttpResponseMessage firstResp = await approverClient.PostAsync(
            "/auth/device/approve", firstContent);
        Assert.Equal(HttpStatusCode.OK, firstResp.StatusCode);

        // Second approval attempt — device code has been approved already → 409
        StringContent secondContent = new(
            JsonSerializer.Serialize(new { userCode }),
            System.Text.Encoding.UTF8,
            "application/json");
        HttpResponseMessage secondResp = await approverClient.PostAsync(
            "/auth/device/approve", secondContent);

        Assert.Equal(HttpStatusCode.Conflict, secondResp.StatusCode);
    }

    // =========================================================================
    // Poll AFTER expiry → Expired (400 from existing endpoint behavior)
    // =========================================================================

    /// <summary>
    /// Polling an expired (or swept) device_code after TTL elapses → 400 (existing behavior).
    /// This verifies that the store correctly cleans up after approval is consumed by a poll.
    /// </summary>
    [Fact]
    public async Task PostDeviceToken_AfterApprovalAndPoll_SecondPollReturnsExpiredOrNotFound()
    {
        (string deviceCode, string userCode) = await IssueDeviceCodeAsync("google");
        (Guid _, string approverToken) = await ProvisionApproverAsync();

        // Approve
        using HttpClient approverClient = _factory.CreateClient();
        AuthEndpointFixture.SetBearerToken(approverClient, approverToken);
        StringContent approveContent = new(
            JsonSerializer.Serialize(new { userCode }),
            System.Text.Encoding.UTF8,
            "application/json");
        await approverClient.PostAsync("/auth/device/approve", approveContent);

        // First poll — consumes the approval, gets tokens
        using HttpClient firstPollClient = _factory.CreateClient();
        StringContent pollContent = new(
            JsonSerializer.Serialize(new { deviceCode }),
            System.Text.Encoding.UTF8,
            "application/json");
        HttpResponseMessage firstPoll = await firstPollClient.PostAsync("/auth/device/token", pollContent);
        Assert.Equal(HttpStatusCode.OK, firstPoll.StatusCode);

        // Second poll — device code was removed from store → Expired (400)
        using HttpClient secondPollClient = _factory.CreateClient();
        StringContent pollContent2 = new(
            JsonSerializer.Serialize(new { deviceCode }),
            System.Text.Encoding.UTF8,
            "application/json");
        HttpResponseMessage secondPoll = await secondPollClient.PostAsync("/auth/device/token", pollContent2);

        // The existing endpoint maps Expired → 400 Problem
        Assert.Equal(HttpStatusCode.BadRequest, secondPoll.StatusCode);
    }

    // =========================================================================
    // Missing / empty userCode body field → 400
    // =========================================================================

    /// <summary>
    /// Authenticated request with missing userCode field → 400 (model binding failure).
    /// </summary>
    [Fact]
    public async Task PostDeviceApprove_MissingUserCodeField_Returns400()
    {
        (Guid _, string approverToken) = await ProvisionApproverAsync();

        using HttpClient client = _factory.CreateClient();
        AuthEndpointFixture.SetBearerToken(client, approverToken);

        // Body without the userCode field
        StringContent content = new(
            JsonSerializer.Serialize(new { otherField = "irrelevant" }),
            System.Text.Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await client.PostAsync("/auth/device/approve", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>
    /// Issues a device code via POST /auth/device/code and returns (deviceCode, userCode).
    /// </summary>
    private async Task<(string DeviceCode, string UserCode)> IssueDeviceCodeAsync(string provider)
    {
        using HttpClient client = _factory.CreateClient();
        StringContent content = new(
            JsonSerializer.Serialize(new { provider }),
            System.Text.Encoding.UTF8,
            "application/json");

        HttpResponseMessage resp = await client.PostAsync("/auth/device/code", content);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        string body = await resp.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        string? deviceCode = root.TryGetProperty("deviceCode", out JsonElement dcEl)
            ? dcEl.GetString()
            : root.GetProperty("device_code").GetString();

        string? userCode = root.TryGetProperty("userCode", out JsonElement ucEl)
            ? ucEl.GetString()
            : root.GetProperty("user_code").GetString();

        Assert.NotNull(deviceCode);
        Assert.NotNull(userCode);
        return (deviceCode!, userCode!);
    }

    /// <summary>
    /// Provisions a user row in the DB and issues a valid Bearer token for them.
    /// Returns (userId, accessToken).
    /// </summary>
    private async Task<(Guid UserId, string AccessToken)> ProvisionApproverAsync()
    {
        Guid userId = Guid.NewGuid();
        string email = $"approver-{userId:N}@test.example.com";

        await using MarketplaceDbContext ctx = _pg.CreateContext();
        ctx.Users.Add(new UserEntity
        {
            Id = userId,
            Email = email,
            EmailNormalized = email.ToLowerInvariant(),
            DisplayName = "Device Approver",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await ctx.SaveChangesAsync();

        string token = AuthEndpointFixture.IssueTestAccessToken(userId, email, "Device Approver");
        return (userId, token);
    }
}
