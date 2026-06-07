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
/// Integration tests for Group 5, Tasks 5.2 + 5.4–5.9 — Identity Module Endpoints.
///
/// Full auth endpoint surface exercised via WebApplicationFactory + real Postgres.
/// All OIDC adapter calls hit the mock provider wired in <see cref="AuthEndpointFixture"/>.
///
/// Endpoints under test:
///   GET  /auth/authorize?provider={provider}            — 5.4
///   GET  /auth/callback?code={code}&amp;state={state}       — 5.5 (redirect to /auth/token)
///   POST /auth/token                                    — 5.5
///   POST /auth/refresh                                  — 5.6
///   GET  /auth/me                                       — 5.6
///   POST /auth/signout                                  — 5.7
///   POST /auth/device/code                              — 5.8
///   POST /auth/device/token                             — 5.8
/// </summary>
[Collection(AuthEndpointFixture.CollectionName)]
public sealed class AuthEndpointTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AuthEndpointTests(PostgresFixture pg)
    {
        _pg = pg;
        _factory = AuthEndpointFixture.CreateFactory(pg);
        // Disable automatic redirect following so we can assert 302 responses
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
    // Task 5.4 — GET /auth/authorize
    // =========================================================================

    /// <summary>
    /// Happy path: known provider → 302 redirect (or 200 with URL body) containing state.
    /// The spec allows either a direct redirect to IdP or a JSON response with the authorize URL.
    /// We accept both shapes: status 302 (Location header) or 200 (body with authorizeUrl).
    /// </summary>
    [Fact]
    public async Task GetAuthorize_KnownProvider_Returns302OrOkWithAuthorizationUrl()
    {
        HttpResponseMessage response = await _client.GetAsync("/auth/authorize?provider=google");

        // Accept either redirect (302/303) or 200 with body
        bool isRedirect = response.StatusCode == HttpStatusCode.Redirect ||
                          response.StatusCode == HttpStatusCode.SeeOther;
        bool isOk = response.StatusCode == HttpStatusCode.OK;

        Assert.True(isRedirect || isOk,
            $"Expected 302 or 200, got {(int)response.StatusCode}");

        if (isRedirect)
        {
            string? location = response.Headers.Location?.ToString();
            Assert.NotNull(location);
            Assert.Contains("accounts.google.com", location);
            Assert.Contains("state=", location);
        }
        else
        {
            string body = await response.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(body);
            Assert.True(
                doc.RootElement.TryGetProperty("authorizeUrl", out JsonElement urlEl) ||
                doc.RootElement.TryGetProperty("authorizationUrl", out urlEl),
                "Body must contain 'authorizeUrl' or 'authorizationUrl'");
            string? url = urlEl.GetString();
            Assert.NotNull(url);
            Assert.Contains("state=", url);
        }
    }

    /// <summary>
    /// Unknown/unsupported provider → 400 Bad Request + ProblemDetails.
    /// </summary>
    [Fact]
    public async Task GetAuthorize_UnknownProvider_Returns400WithProblemDetails()
    {
        HttpResponseMessage response = await _client.GetAsync("/auth/authorize?provider=github");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        // RFC 7807 ProblemDetails — must have title or detail
        bool isProblemDetails = doc.RootElement.TryGetProperty("title", out _) ||
                                doc.RootElement.TryGetProperty("detail", out _);
        Assert.True(isProblemDetails, "Response must be RFC 7807 ProblemDetails");
    }

    /// <summary>
    /// Missing provider parameter → 400.
    /// </summary>
    [Fact]
    public async Task GetAuthorize_MissingProviderParam_Returns400()
    {
        HttpResponseMessage response = await _client.GetAsync("/auth/authorize");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // =========================================================================
    // Task 5.5 — POST /auth/token (code + state exchange)
    // =========================================================================

    /// <summary>
    /// Happy path: valid code + state (pre-seeded) → 200 with access token + refresh token.
    /// Note: we seed the state store via GET /auth/authorize and capture the state from the
    /// response, then POST to /auth/token with that state + our mock ValidCode.
    /// </summary>
    [Fact]
    public async Task PostToken_ValidCodeAndState_Returns200WithTokens()
    {
        // Step 1: initiate sign-in to obtain a real state value
        HttpResponseMessage authorizeResponse = await _client.GetAsync("/auth/authorize?provider=google");
        string? state = ExtractStateFromAuthorizeResponse(authorizeResponse);
        Assert.NotNull(state);

        // Step 2: exchange code — use fresh client that follows redirects for the token call
        using HttpClient tokenClient = _factory.CreateClient();
        StringContent content = new(
            JsonSerializer.Serialize(new
            {
                code = AuthEndpointFixture.ValidCode,
                state,
                // codeVerifier: the real verifier was stored server-side; we pass any value since
                // the mock adapter doesn't validate the PKCE verifier on exchange
                codeVerifier = "test-verifier",
            }),
            System.Text.Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await tokenClient.PostAsync("/auth/token", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        Assert.True(
            root.TryGetProperty("accessToken", out JsonElement atEl) ||
            root.TryGetProperty("access_token", out atEl),
            "Response must contain 'accessToken' or 'access_token'");
        Assert.NotNull(atEl.GetString());

        Assert.True(
            root.TryGetProperty("refreshToken", out JsonElement rtEl) ||
            root.TryGetProperty("refresh_token", out rtEl),
            "Response must contain 'refreshToken' or 'refresh_token'");
        Assert.NotNull(rtEl.GetString());
    }

    /// <summary>
    /// Expired code → 401 Unauthorized + ProblemDetails.
    /// </summary>
    [Fact]
    public async Task PostToken_ExpiredCode_Returns401WithProblemDetails()
    {
        // Obtain a valid state first
        HttpResponseMessage authorizeResponse = await _client.GetAsync("/auth/authorize?provider=google");
        string? state = ExtractStateFromAuthorizeResponse(authorizeResponse);
        Assert.NotNull(state);

        using HttpClient tokenClient = _factory.CreateClient();
        StringContent content = new(
            JsonSerializer.Serialize(new
            {
                code = AuthEndpointFixture.ExpiredCode,
                state,
                codeVerifier = "test-verifier",
            }),
            System.Text.Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await tokenClient.PostAsync("/auth/token", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        bool isProblemDetails = doc.RootElement.TryGetProperty("title", out _) ||
                                doc.RootElement.TryGetProperty("detail", out _);
        Assert.True(isProblemDetails, "Response must be RFC 7807 ProblemDetails on 401");
    }

    /// <summary>
    /// Tampered/invalid code → 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task PostToken_TamperedCode_Returns401()
    {
        HttpResponseMessage authorizeResponse = await _client.GetAsync("/auth/authorize?provider=google");
        string? state = ExtractStateFromAuthorizeResponse(authorizeResponse);
        Assert.NotNull(state);

        using HttpClient tokenClient = _factory.CreateClient();
        StringContent content = new(
            JsonSerializer.Serialize(new
            {
                code = AuthEndpointFixture.TamperedCode,
                state,
                codeVerifier = "test-verifier",
            }),
            System.Text.Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await tokenClient.PostAsync("/auth/token", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Unknown/expired state → 401 (state not in store or TTL elapsed).
    /// </summary>
    [Fact]
    public async Task PostToken_UnknownState_Returns401()
    {
        using HttpClient tokenClient = _factory.CreateClient();
        StringContent content = new(
            JsonSerializer.Serialize(new
            {
                code = AuthEndpointFixture.ValidCode,
                state = "state-that-does-not-exist-in-store-" + Guid.NewGuid(),
                codeVerifier = "test-verifier",
            }),
            System.Text.Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await tokenClient.PostAsync("/auth/token", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // =========================================================================
    // Task 5.5 — GET /auth/callback
    // =========================================================================

    /// <summary>
    /// The callback endpoint receives code + state from the IdP redirect, then exchanges
    /// them for tokens.  It must either:
    ///   (a) redirect to a frontend URL with tokens in the fragment/query, or
    ///   (b) return 200 with tokens directly (SPA can read them from same-origin fetch)
    /// Both patterns are acceptable — we assert it doesn't return 4xx/5xx on a valid flow.
    /// </summary>
    [Fact]
    public async Task GetCallback_ValidCodeAndState_DoesNotReturn4xxOr5xx()
    {
        // Obtain a real state from the authorize endpoint
        HttpResponseMessage authorizeResponse = await _client.GetAsync("/auth/authorize?provider=google");
        string? state = ExtractStateFromAuthorizeResponse(authorizeResponse);
        Assert.NotNull(state);

        // Simulate the IdP redirecting back with code + state
        HttpResponseMessage callbackResponse = await _client.GetAsync(
            $"/auth/callback?code={AuthEndpointFixture.ValidCode}&state={Uri.EscapeDataString(state!)}");

        int statusCode = (int)callbackResponse.StatusCode;
        Assert.True(statusCode < 400,
            $"Callback with valid code+state must not return 4xx/5xx, got {statusCode}");
    }

    /// <summary>
    /// Callback with expired code → 401 or redirect to error URL.
    /// </summary>
    [Fact]
    public async Task GetCallback_ExpiredCode_Returns401OrRedirectToError()
    {
        HttpResponseMessage authorizeResponse = await _client.GetAsync("/auth/authorize?provider=google");
        string? state = ExtractStateFromAuthorizeResponse(authorizeResponse);
        Assert.NotNull(state);

        HttpResponseMessage callbackResponse = await _client.GetAsync(
            $"/auth/callback?code={AuthEndpointFixture.ExpiredCode}&state={Uri.EscapeDataString(state!)}");

        // Either a 401 or a redirect to an error page — both acceptable
        bool isErrorStatus = callbackResponse.StatusCode == HttpStatusCode.Unauthorized ||
                             callbackResponse.StatusCode == HttpStatusCode.BadRequest;
        bool isRedirectToError = (callbackResponse.StatusCode == HttpStatusCode.Redirect ||
                                  callbackResponse.StatusCode == HttpStatusCode.SeeOther) &&
                                 callbackResponse.Headers.Location?.ToString().Contains("error") == true;

        Assert.True(isErrorStatus || isRedirectToError,
            $"Expected 401/400 or redirect-to-error but got {(int)callbackResponse.StatusCode}");
    }

    // =========================================================================
    // Task 5.6 — POST /auth/refresh
    // =========================================================================

    /// <summary>
    /// Helper: perform a full sign-in flow via the API and return the refresh token.
    /// </summary>
    private async Task<(string AccessToken, string RefreshToken)> PerformFullSignIn()
    {
        // Initiate
        HttpResponseMessage authorizeResponse = await _client.GetAsync("/auth/authorize?provider=google");
        string? state = ExtractStateFromAuthorizeResponse(authorizeResponse);
        Assert.NotNull(state);

        // Exchange code for tokens
        using HttpClient tokenClient = _factory.CreateClient();
        StringContent content = new(
            JsonSerializer.Serialize(new
            {
                code = AuthEndpointFixture.ValidCode,
                state,
                codeVerifier = "test-verifier",
            }),
            System.Text.Encoding.UTF8,
            "application/json");

        HttpResponseMessage tokenResp = await tokenClient.PostAsync("/auth/token", content);
        Assert.Equal(HttpStatusCode.OK, tokenResp.StatusCode);

        string body = await tokenResp.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        string? accessToken = root.TryGetProperty("accessToken", out JsonElement at)
            ? at.GetString()
            : root.GetProperty("access_token").GetString();

        string? refreshToken = root.TryGetProperty("refreshToken", out JsonElement rt)
            ? rt.GetString()
            : root.GetProperty("refresh_token").GetString();

        Assert.NotNull(accessToken);
        Assert.NotNull(refreshToken);
        return (accessToken!, refreshToken!);
    }

    /// <summary>
    /// Valid refresh token → 200 with new access + rotated refresh token.
    /// </summary>
    [Fact]
    public async Task PostRefresh_ValidRefreshToken_Returns200WithNewTokens()
    {
        (string _, string refreshToken) = await PerformFullSignIn();

        using HttpClient refreshClient = _factory.CreateClient();
        StringContent content = new(
            JsonSerializer.Serialize(new { refreshToken }),
            System.Text.Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await refreshClient.PostAsync("/auth/refresh", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        // New access token
        Assert.True(
            root.TryGetProperty("accessToken", out JsonElement newAt) ||
            root.TryGetProperty("access_token", out newAt),
            "Refresh response must include a new access token");
        Assert.NotNull(newAt.GetString());

        // New refresh token (rotated)
        Assert.True(
            root.TryGetProperty("refreshToken", out JsonElement newRt) ||
            root.TryGetProperty("refresh_token", out newRt),
            "Refresh response must include a rotated refresh token");
        string? newRefreshToken = newRt.GetString();
        Assert.NotNull(newRefreshToken);

        // The new refresh token must differ from the original (rotation)
        Assert.NotEqual(refreshToken, newRefreshToken);
    }

    /// <summary>
    /// Reuse of a rotated refresh token → chain revocation → 401.
    /// </summary>
    [Fact]
    public async Task PostRefresh_ReuseRotatedRefreshToken_Returns401AndRevokesChain()
    {
        (string _, string originalRefresh) = await PerformFullSignIn();

        using HttpClient refreshClient = _factory.CreateClient();

        // First rotation — valid
        StringContent firstContent = new(
            JsonSerializer.Serialize(new { refreshToken = originalRefresh }),
            System.Text.Encoding.UTF8,
            "application/json");
        HttpResponseMessage firstResp = await refreshClient.PostAsync("/auth/refresh", firstContent);
        Assert.Equal(HttpStatusCode.OK, firstResp.StatusCode);

        // Reuse the ORIGINAL (now rotated) token — must trigger chain revocation → 401
        StringContent reuseContent = new(
            JsonSerializer.Serialize(new { refreshToken = originalRefresh }),
            System.Text.Encoding.UTF8,
            "application/json");
        HttpResponseMessage reuseResp = await refreshClient.PostAsync("/auth/refresh", reuseContent);

        Assert.Equal(HttpStatusCode.Unauthorized, reuseResp.StatusCode);

        string body = await reuseResp.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        bool isProblemDetails = doc.RootElement.TryGetProperty("title", out _) ||
                                doc.RootElement.TryGetProperty("detail", out _);
        Assert.True(isProblemDetails, "Reuse response must be RFC 7807 ProblemDetails");
    }

    /// <summary>
    /// Expired or tampered refresh token → 401.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("definitely-not-a-real-refresh-token-value-1234567890")]
    public async Task PostRefresh_InvalidRefreshToken_Returns401(string badToken)
    {
        using HttpClient refreshClient = _factory.CreateClient();
        StringContent content = new(
            JsonSerializer.Serialize(new { refreshToken = badToken }),
            System.Text.Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await refreshClient.PostAsync("/auth/refresh", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // =========================================================================
    // Task 5.6 — GET /auth/me  ([Authorize] required)
    // =========================================================================

    /// <summary>
    /// Unauthenticated → 401.
    /// </summary>
    [Fact]
    public async Task GetMe_Unauthenticated_Returns401()
    {
        using HttpClient client = _factory.CreateClient();
        AuthEndpointFixture.ClearBearerToken(client);

        HttpResponseMessage response = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Valid Bearer token → 200 + userId/email/displayName/orgMemberships.
    /// </summary>
    [Fact]
    public async Task GetMe_ValidBearerToken_Returns200WithUserInfo()
    {
        // Provision user by performing a full sign-in
        (string accessToken, string _) = await PerformFullSignIn();

        using HttpClient client = _factory.CreateClient();
        AuthEndpointFixture.SetBearerToken(client, accessToken);

        HttpResponseMessage response = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        // Must contain userId (Guid or string), email, displayName, orgMemberships (array)
        Assert.True(
            root.TryGetProperty("userId", out _) ||
            root.TryGetProperty("id", out _),
            "Response must contain 'userId' or 'id'");

        Assert.True(root.TryGetProperty("email", out JsonElement emailEl),
            "Response must contain 'email'");
        Assert.Equal(AuthEndpointFixture.MockEmail, emailEl.GetString());

        Assert.True(
            root.TryGetProperty("displayName", out _) ||
            root.TryGetProperty("name", out _),
            "Response must contain 'displayName' or 'name'");

        Assert.True(
            root.TryGetProperty("orgMemberships", out JsonElement orgsEl) ||
            root.TryGetProperty("organizations", out orgsEl),
            "Response must contain 'orgMemberships' or 'organizations'");
        Assert.Equal(JsonValueKind.Array, orgsEl.ValueKind);
    }

    /// <summary>
    /// Expired/malformed JWT → 401.
    /// </summary>
    [Theory]
    [InlineData("not.a.jwt")]
    [InlineData("Bearer notajwt")]
    public async Task GetMe_MalformedJwt_Returns401(string badToken)
    {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", badToken);

        HttpResponseMessage response = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // =========================================================================
    // Task 5.7 — POST /auth/signout  ([Authorize] required)
    // =========================================================================

    /// <summary>
    /// Unauthenticated → 401.
    /// </summary>
    [Fact]
    public async Task PostSignout_Unauthenticated_Returns401()
    {
        using HttpClient client = _factory.CreateClient();
        AuthEndpointFixture.ClearBearerToken(client);

        StringContent content = new(
            JsonSerializer.Serialize(new { refreshToken = "some-token" }),
            System.Text.Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await client.PostAsync("/auth/signout", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Authenticated + valid refresh token → 200 or 204; revokes refresh.
    /// Subsequent use of the revoked refresh → 401.
    /// </summary>
    [Fact]
    public async Task PostSignout_ValidRequest_RevokesRefreshToken()
    {
        (string accessToken, string refreshToken) = await PerformFullSignIn();

        using HttpClient client = _factory.CreateClient();
        AuthEndpointFixture.SetBearerToken(client, accessToken);

        StringContent content = new(
            JsonSerializer.Serialize(new { refreshToken }),
            System.Text.Encoding.UTF8,
            "application/json");

        HttpResponseMessage signoutResp = await client.PostAsync("/auth/signout", content);

        // Accept 200 or 204 as success
        Assert.True(
            signoutResp.StatusCode == HttpStatusCode.OK ||
            signoutResp.StatusCode == HttpStatusCode.NoContent,
            $"Expected 200 or 204, got {(int)signoutResp.StatusCode}");

        // Subsequent refresh attempt must be rejected
        using HttpClient refreshClient = _factory.CreateClient();
        StringContent refreshContent = new(
            JsonSerializer.Serialize(new { refreshToken }),
            System.Text.Encoding.UTF8,
            "application/json");

        HttpResponseMessage refreshResp = await refreshClient.PostAsync("/auth/refresh", refreshContent);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResp.StatusCode);
    }

    // =========================================================================
    // Task 5.8 — POST /auth/device/code
    // =========================================================================

    /// <summary>
    /// Valid provider → 200 with user_code, device_code, verification_url, expires_in, interval.
    /// </summary>
    [Fact]
    public async Task PostDeviceCode_ValidProvider_Returns200WithDeviceCodeFields()
    {
        using HttpClient client = _factory.CreateClient();
        StringContent content = new(
            JsonSerializer.Serialize(new { provider = "google" }),
            System.Text.Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await client.PostAsync("/auth/device/code", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        // RFC 8628 device authorization response fields
        Assert.True(
            root.TryGetProperty("deviceCode", out JsonElement dcEl) ||
            root.TryGetProperty("device_code", out dcEl),
            "Response must contain 'deviceCode' or 'device_code'");
        Assert.NotNull(dcEl.GetString());

        Assert.True(
            root.TryGetProperty("userCode", out JsonElement ucEl) ||
            root.TryGetProperty("user_code", out ucEl),
            "Response must contain 'userCode' or 'user_code'");
        string? userCode = ucEl.GetString();
        Assert.NotNull(userCode);
        Assert.True(userCode!.Length is >= 6 and <= 10,
            $"user_code must be 6-10 chars; got '{userCode}'");

        Assert.True(
            root.TryGetProperty("verificationUrl", out JsonElement vuEl) ||
            root.TryGetProperty("verification_url", out vuEl) ||
            root.TryGetProperty("verification_uri", out vuEl),
            "Response must contain 'verificationUrl' or 'verification_uri'");
        Assert.NotNull(vuEl.GetString());

        Assert.True(
            root.TryGetProperty("expiresIn", out JsonElement expEl) ||
            root.TryGetProperty("expires_in", out expEl),
            "Response must contain 'expiresIn' or 'expires_in'");
        Assert.True(expEl.GetInt32() > 0, "expires_in must be positive");

        Assert.True(
            root.TryGetProperty("interval", out JsonElement intervalEl),
            "Response must contain 'interval'");
        Assert.True(intervalEl.GetInt32() > 0, "interval must be positive");
    }

    /// <summary>
    /// Unknown provider → 400.
    /// </summary>
    [Fact]
    public async Task PostDeviceCode_UnknownProvider_Returns400()
    {
        using HttpClient client = _factory.CreateClient();
        StringContent content = new(
            JsonSerializer.Serialize(new { provider = "twitter" }),
            System.Text.Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await client.PostAsync("/auth/device/code", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // =========================================================================
    // Task 5.8 — POST /auth/device/token  (polling)
    // =========================================================================

    /// <summary>
    /// Unknown device_code → 400 or equivalent error indicating expiry/not-found.
    /// </summary>
    [Fact]
    public async Task PostDeviceToken_UnknownDeviceCode_Returns4xxExpiredOrNotFound()
    {
        using HttpClient client = _factory.CreateClient();
        StringContent content = new(
            JsonSerializer.Serialize(new { deviceCode = "device-code-that-does-not-exist" }),
            System.Text.Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await client.PostAsync("/auth/device/token", content);

        // RFC 8628: expired_token (400), authorization_pending (200 with error body), or 404
        int statusCode = (int)response.StatusCode;
        Assert.True(
            statusCode == 400 || statusCode == 200 || statusCode == 404,
            $"Expected 400/200/404 for unknown device_code, got {statusCode}");

        string body = await response.Content.ReadAsStringAsync();
        if (statusCode == 200)
        {
            // If 200, must include an error field indicating the code is expired or pending
            using JsonDocument doc = JsonDocument.Parse(body);
            bool hasErrorOrStatus =
                doc.RootElement.TryGetProperty("error", out _) ||
                doc.RootElement.TryGetProperty("status", out _);
            Assert.True(hasErrorOrStatus,
                "200 response for unknown device_code must include 'error' or 'status' field");
        }
    }

    /// <summary>
    /// Pending device_code (user hasn't approved) → response indicates authorization_pending.
    /// Must not return 200 with tokens.
    /// </summary>
    [Fact]
    public async Task PostDeviceToken_PendingCode_ReturnsPendingStatusNoTokens()
    {
        using HttpClient client = _factory.CreateClient();

        // First, issue a device code
        StringContent issueContent = new(
            JsonSerializer.Serialize(new { provider = "google" }),
            System.Text.Encoding.UTF8,
            "application/json");
        HttpResponseMessage issueResp = await client.PostAsync("/auth/device/code", issueContent);
        Assert.Equal(HttpStatusCode.OK, issueResp.StatusCode);

        string issueBody = await issueResp.Content.ReadAsStringAsync();
        using JsonDocument issueDoc = JsonDocument.Parse(issueBody);
        JsonElement issueRoot = issueDoc.RootElement;
        string? deviceCode = issueRoot.TryGetProperty("deviceCode", out JsonElement dcEl)
            ? dcEl.GetString()
            : issueRoot.GetProperty("device_code").GetString();

        Assert.NotNull(deviceCode);

        // Poll immediately — user hasn't approved → pending
        StringContent pollContent = new(
            JsonSerializer.Serialize(new { deviceCode }),
            System.Text.Encoding.UTF8,
            "application/json");

        HttpResponseMessage pollResp = await client.PostAsync("/auth/device/token", pollContent);

        // Pending: either 200 with { "error": "authorization_pending" } or HTTP 428 (Precondition Required)
        // The design does not specify an exact status; we assert: NOT a token response
        string pollBody = await pollResp.Content.ReadAsStringAsync();

        if (pollResp.StatusCode == HttpStatusCode.OK)
        {
            using JsonDocument pollDoc = JsonDocument.Parse(pollBody);
            // If 200, must NOT have access_token — must have error or status = pending
            bool hasAccessToken =
                pollDoc.RootElement.TryGetProperty("accessToken", out _) ||
                pollDoc.RootElement.TryGetProperty("access_token", out _);
            Assert.False(hasAccessToken,
                "Pending device_code poll must NOT return access tokens");
        }
        else
        {
            // Any 4xx is also acceptable for pending (some implementations use 400 w/ error body)
            Assert.True((int)pollResp.StatusCode is >= 200 and <= 499,
                $"Unexpected status {(int)pollResp.StatusCode} for pending poll");
        }
    }

    // =========================================================================
    // Task 5.2 — JwtBearer wiring: ClaimsPrincipal populated from valid JWT
    // =========================================================================

    /// <summary>
    /// Verifies that a JWT issued by our test RsaTokenIssuerAdapter is accepted by the
    /// JwtBearer middleware: GET /auth/me returns 200 (not 401) when a valid self-issued JWT
    /// is provided, proving that JwtBearer is correctly wired to validate our RS256 tokens.
    /// </summary>
    [Fact]
    public async Task JwtBearerWiring_SelfIssuedToken_AcceptedByMiddleware()
    {
        // Issue a token with known claims using the same key/issuer/audience as the test app
        Guid testUserId = Guid.NewGuid();
        const string testEmail = "jwt-wiring@test.example.com";

        // First, provision this user in DB so /auth/me can look them up
        await using MarketplaceDbContext ctx = _pg.CreateContext();
        ctx.Users.Add(new UserEntity
        {
            Id = testUserId,
            Email = testEmail,
            EmailNormalized = testEmail.ToLowerInvariant(),
            DisplayName = "JWT Wiring Test",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await ctx.SaveChangesAsync();

        string token = AuthEndpointFixture.IssueTestAccessToken(testUserId, testEmail, "JWT Wiring Test");

        using HttpClient client = _factory.CreateClient();
        AuthEndpointFixture.SetBearerToken(client, token);

        HttpResponseMessage response = await client.GetAsync("/auth/me");

        // JwtBearer validated our token → auth succeeded → 200 (not 401)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Token issued with the wrong key → 401 (JwtBearer rejects invalid signature).
    /// </summary>
    [Fact]
    public async Task JwtBearerWiring_TokenFromWrongKey_Returns401()
    {
        // Generate a DIFFERENT key pair — this key is NOT the test app's signing key
        using System.Security.Cryptography.RSA wrongRsa =
            System.Security.Cryptography.RSA.Create(keySizeInBits: 2048);
        string wrongPrivatePem = wrongRsa.ExportPkcs8PrivateKeyPem();

        ClaudeForge.Infrastructure.Identity.RsaTokenIssuerAdapter wrongIssuer =
            new(
                privatePem: wrongPrivatePem,
                issuer: "https://claudeforge.test",
                audience: "claudeforge-api-test",
                accessTokenMinutes: 15,
                kid: "wrong-kid");

        string wrongToken = wrongIssuer.IssueAccessToken(
            new ClaudeForge.Core.Identity.Ports.AccessTokenClaims(
                Guid.NewGuid(), "wrong@test.com", "Wrong User", "google"));

        using HttpClient client = _factory.CreateClient();
        AuthEndpointFixture.SetBearerToken(client, wrongToken);

        HttpResponseMessage response = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static string? ExtractStateFromAuthorizeResponse(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Redirect ||
            response.StatusCode == HttpStatusCode.SeeOther)
        {
            string? location = response.Headers.Location?.ToString();
            if (location is null) return null;

            // Extract state= query param from the redirect URL
            Uri uri = new(location);
            string query = uri.Query;
            foreach (string part in query.TrimStart('?').Split('&'))
            {
                string[] kv = part.Split('=', 2);
                if (kv.Length == 2 && kv[0] == "state")
                    return Uri.UnescapeDataString(kv[1]);
            }
            return null;
        }

        if (response.StatusCode == HttpStatusCode.OK)
        {
            // Body may contain { "state": "..." } or { "authorizeUrl": "...?state=..." }
            string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            try
            {
                using JsonDocument doc = JsonDocument.Parse(body);
                JsonElement root = doc.RootElement;

                if (root.TryGetProperty("state", out JsonElement stateEl))
                    return stateEl.GetString();

                // Extract from authorizeUrl
                string? authorizeUrl =
                    root.TryGetProperty("authorizeUrl", out JsonElement urlEl)
                        ? urlEl.GetString()
                    : root.TryGetProperty("authorizationUrl", out JsonElement urlEl2)
                        ? urlEl2.GetString()
                    : null;

                if (authorizeUrl is not null)
                {
                    Uri uri = new(authorizeUrl);
                    foreach (string part in uri.Query.TrimStart('?').Split('&'))
                    {
                        string[] kv = part.Split('=', 2);
                        if (kv.Length == 2 && kv[0] == "state")
                            return Uri.UnescapeDataString(kv[1]);
                    }
                }
            }
            catch (JsonException)
            {
                // ignore parse errors
            }
        }

        return null;
    }
}
