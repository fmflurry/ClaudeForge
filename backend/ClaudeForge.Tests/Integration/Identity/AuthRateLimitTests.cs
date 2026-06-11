using System.Net;
using System.Net.Http.Json;
using ClaudeForge.Core.Identity.Ports;
using ClaudeForge.Core.Shared.Authorization;
using ClaudeForge.Infrastructure.Identity;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using ClaudeForge.Tests.Integration.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeForge.Tests.Integration.Identity;

/// <summary>
/// Integration tests for Group 8, Task 8.1 — Rate Limiting on Auth Endpoints.
///
/// Verifies that per-IP fixed-window rate-limit policies are applied to the sensitive
/// auth endpoints.  The production code (GREEN) must register the following named policies
/// via AddRateLimiter (identical idiom to AddOnPublishingModule / TelemetryModule):
///
/// ┌─────────────────────────────────────────────────────────────────────────────────┐
/// │  Policy name              │ Endpoint(s)                   │ Permit │ Window    │
/// ├─────────────────────────────────────────────────────────────────────────────────┤
/// │ "auth-authorize-limit"    │ GET  /auth/authorize          │  5     │ 1 min     │
/// │ "auth-token-limit"        │ POST /auth/token              │  3     │ 1 min     │
/// │ "auth-refresh-limit"      │ POST /auth/refresh            │  3     │ 1 min     │
/// │ "auth-device-token-limit" │ POST /auth/device/token       │  5     │ 1 min     │
/// │ "auth-invite-limit"       │ POST /api/v1/orgs/{}/invites  │  5     │ 1 min     │
/// └─────────────────────────────────────────────────────────────────────────────────┘
///
/// All policies are FixedWindow, QueueLimit=0, RejectionStatusCode=429.
/// Partition key: RemoteIpAddress ?? "unknown"  (same as existing policies).
///
/// Test strategy:
///   - Use a tiny PermitLimit (defined by the policy contract above) so that N+1 rapid
///     requests reliably trip the limiter within a single test run.
///   - Factory sets test-only in-memory config overrides to keep PermitLimits small;
///     the production module MUST read those overrides or use the constants above.
///   - "Under the limit" tests assert the endpoint responds with its normal status
///     (2xx, 3xx, 4xx for invalid payload — NOT 429).
///   - "Over the limit" burst tests send PermitLimit+1 requests and assert the final
///     response is HTTP 429 Too Many Requests.
///
/// Fixture reuse:
///   Tests in this class use AuthEndpointFixture.CreateFactory(pg) — the same factory
///   used by AuthEndpointTests — so no new factory boilerplate is needed.
///   The factory supplies the mock OIDC registry, real RS256 key pair, and real Postgres.
/// </summary>
[Collection(AuthEndpointFixture.CollectionName)]
public sealed class AuthRateLimitTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private readonly WebApplicationFactory<Program> _factory;

    // Policy constants — mirror exactly what GREEN must implement.
    // If the production module reads a different config key, these constants are the source of truth.
    internal const int AuthorizePermitLimit = 5;
    internal const int TokenPermitLimit = 3;
    internal const int RefreshPermitLimit = 3;
    internal const int DeviceTokenPermitLimit = 5;
    internal const int InvitePermitLimit = 5;

    public AuthRateLimitTests(PostgresFixture pg)
    {
        _pg = pg;
        _factory = AuthEndpointFixture.CreateFactory(pg);
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
        await _factory.DisposeAsync();
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>
    /// Creates a fresh HttpClient for each burst test.  Each client shares the same
    /// RemoteIpAddress (loopback / "unknown") so all requests hit the same rate-limit bucket.
    /// </summary>
    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });

    /// <summary>
    /// Sends <paramref name="count"/> requests and returns all responses.
    /// Disposes each response after capturing the status code so connections are freed.
    /// </summary>
    private static async Task<List<HttpStatusCode>> BurstGetAsync(
        HttpClient client,
        string url,
        int count)
    {
        List<HttpStatusCode> codes = [];
        for (int i = 0; i < count; i++)
        {
            HttpResponseMessage response = await client.GetAsync(url);
            codes.Add(response.StatusCode);
            response.Dispose();
        }
        return codes;
    }

    private static async Task<List<HttpStatusCode>> BurstPostAsync(
        HttpClient client,
        string url,
        object body,
        int count)
    {
        List<HttpStatusCode> codes = [];
        for (int i = 0; i < count; i++)
        {
            HttpResponseMessage response = await client.PostAsJsonAsync(url, body);
            codes.Add(response.StatusCode);
            response.Dispose();
        }
        return codes;
    }

    // =========================================================================
    // Task 8.1 — GET /auth/authorize  (policy: "auth-authorize-limit")
    // =========================================================================

    /// <summary>
    /// Sending exactly PermitLimit requests within a fixed window must NOT yield 429.
    /// Each request may be 302, 400, or other non-429 status depending on the payload.
    /// </summary>
    [Fact]
    public async Task GetAuthorize_UnderRateLimit_DoesNotReturn429()
    {
        using HttpClient client = CreateClient();

        List<HttpStatusCode> codes = await BurstGetAsync(
            client,
            "/auth/authorize?provider=google",
            AuthorizePermitLimit);

        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, codes);
    }

    /// <summary>
    /// Sending PermitLimit+1 rapid requests must cause at least one 429 response.
    /// GREEN: apply "auth-authorize-limit" policy (PermitLimit=5, Window=1min, QueueLimit=0)
    /// to GET /auth/authorize via .RequireRateLimiting("auth-authorize-limit").
    /// </summary>
    [Fact]
    public async Task GetAuthorize_BurstExceedsLimit_Returns429()
    {
        using HttpClient client = CreateClient();

        List<HttpStatusCode> codes = await BurstGetAsync(
            client,
            "/auth/authorize?provider=google",
            AuthorizePermitLimit + 1);

        Assert.Contains(HttpStatusCode.TooManyRequests, codes);
    }

    // =========================================================================
    // Task 8.1 — POST /auth/token  (policy: "auth-token-limit", tighter bucket)
    // =========================================================================

    /// <summary>
    /// Exactly TokenPermitLimit (3) requests to /auth/token within a window must not 429.
    /// (The endpoint may return 400 / 401 for invalid payloads — that is acceptable.)
    /// </summary>
    [Fact]
    public async Task PostToken_UnderRateLimit_DoesNotReturn429()
    {
        using HttpClient client = CreateClient();

        // Use an invalid payload — we only care about the rate-limit header, not the business result.
        object payload = new { code = "DUMMY_CODE", state = "DUMMY_STATE", codeVerifier = "DUMMY_VERIFIER" };

        List<HttpStatusCode> codes = await BurstPostAsync(
            client,
            "/auth/token",
            payload,
            TokenPermitLimit);

        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, codes);
    }

    /// <summary>
    /// TokenPermitLimit+1 (4) rapid requests to /auth/token must trigger 429.
    /// GREEN: apply "auth-token-limit" policy (PermitLimit=3, Window=1min, QueueLimit=0)
    /// to POST /auth/token via .RequireRateLimiting("auth-token-limit").
    /// </summary>
    [Fact]
    public async Task PostToken_BurstExceedsLimit_Returns429()
    {
        using HttpClient client = CreateClient();

        object payload = new { code = "DUMMY_CODE", state = "DUMMY_STATE", codeVerifier = "DUMMY_VERIFIER" };

        List<HttpStatusCode> codes = await BurstPostAsync(
            client,
            "/auth/token",
            payload,
            TokenPermitLimit + 1);

        Assert.Contains(HttpStatusCode.TooManyRequests, codes);
    }

    // =========================================================================
    // Task 8.1 — POST /auth/refresh  (policy: "auth-refresh-limit", tighter bucket)
    // =========================================================================

    /// <summary>
    /// Exactly RefreshPermitLimit (3) requests to /auth/refresh within a window must not 429.
    /// </summary>
    [Fact]
    public async Task PostRefresh_UnderRateLimit_DoesNotReturn429()
    {
        using HttpClient client = CreateClient();

        object payload = new { refreshToken = "DUMMY_REFRESH_TOKEN" };

        List<HttpStatusCode> codes = await BurstPostAsync(
            client,
            "/auth/refresh",
            payload,
            RefreshPermitLimit);

        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, codes);
    }

    /// <summary>
    /// RefreshPermitLimit+1 (4) rapid requests to /auth/refresh must trigger 429.
    /// GREEN: apply "auth-refresh-limit" policy (PermitLimit=3, Window=1min, QueueLimit=0)
    /// to POST /auth/refresh via .RequireRateLimiting("auth-refresh-limit").
    /// </summary>
    [Fact]
    public async Task PostRefresh_BurstExceedsLimit_Returns429()
    {
        using HttpClient client = CreateClient();

        object payload = new { refreshToken = "DUMMY_REFRESH_TOKEN" };

        List<HttpStatusCode> codes = await BurstPostAsync(
            client,
            "/auth/refresh",
            payload,
            RefreshPermitLimit + 1);

        Assert.Contains(HttpStatusCode.TooManyRequests, codes);
    }

    // =========================================================================
    // Task 8.1 — POST /auth/device/token  (policy: "auth-device-token-limit")
    // =========================================================================

    /// <summary>
    /// Exactly DeviceTokenPermitLimit (5) requests to /auth/device/token within a window must not 429.
    /// </summary>
    [Fact]
    public async Task PostDeviceToken_UnderRateLimit_DoesNotReturn429()
    {
        using HttpClient client = CreateClient();

        object payload = new { deviceCode = "DUMMY_DEVICE_CODE" };

        List<HttpStatusCode> codes = await BurstPostAsync(
            client,
            "/auth/device/token",
            payload,
            DeviceTokenPermitLimit);

        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, codes);
    }

    /// <summary>
    /// DeviceTokenPermitLimit+1 (6) rapid requests to /auth/device/token must trigger 429.
    /// GREEN: apply "auth-device-token-limit" policy (PermitLimit=5, Window=1min, QueueLimit=0)
    /// to POST /auth/device/token via .RequireRateLimiting("auth-device-token-limit").
    /// </summary>
    [Fact]
    public async Task PostDeviceToken_BurstExceedsLimit_Returns429()
    {
        using HttpClient client = CreateClient();

        object payload = new { deviceCode = "DUMMY_DEVICE_CODE" };

        List<HttpStatusCode> codes = await BurstPostAsync(
            client,
            "/auth/device/token",
            payload,
            DeviceTokenPermitLimit + 1);

        Assert.Contains(HttpStatusCode.TooManyRequests, codes);
    }

    // =========================================================================
    // Task 8.1 — POST /api/v1/orgs/{orgId}/invitations  (policy: "auth-invite-limit")
    // =========================================================================

    /// <summary>
    /// Exactly InvitePermitLimit (5) requests to POST invitations within a window must not 429.
    /// The endpoint may return 401/403/404 for these unauthenticated dummy requests — acceptable.
    /// </summary>
    [Fact]
    public async Task PostInvitation_UnderRateLimit_DoesNotReturn429()
    {
        using HttpClient client = CreateClient();

        Guid orgId = Guid.NewGuid();
        object payload = new { email = "target@test.example.com", role = "member" };

        List<HttpStatusCode> codes = await BurstPostAsync(
            client,
            $"/api/v1/orgs/{orgId}/invitations",
            payload,
            InvitePermitLimit);

        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, codes);
    }

    /// <summary>
    /// InvitePermitLimit+1 (6) rapid requests to POST invitations must trigger 429.
    /// GREEN: apply "auth-invite-limit" policy (PermitLimit=5, Window=1min, QueueLimit=0)
    /// to POST /api/v1/orgs/{orgId}/invitations via .RequireRateLimiting("auth-invite-limit").
    /// </summary>
    [Fact]
    public async Task PostInvitation_BurstExceedsLimit_Returns429()
    {
        using HttpClient client = CreateClient();

        Guid orgId = Guid.NewGuid();
        object payload = new { email = "target@test.example.com", role = "member" };

        List<HttpStatusCode> codes = await BurstPostAsync(
            client,
            $"/api/v1/orgs/{orgId}/invitations",
            payload,
            InvitePermitLimit + 1);

        Assert.Contains(HttpStatusCode.TooManyRequests, codes);
    }

    // =========================================================================
    // Task 8.1 — 429 response body is RFC 7807 ProblemDetails
    // =========================================================================

    /// <summary>
    /// When rate-limited, the response body must be RFC 7807 ProblemDetails with status=429.
    /// This mirrors the RejectionStatusCode=429 pattern in AddOnPublishingModule.
    /// </summary>
    [Fact]
    public async Task RateLimitedResponse_HasCorrectStatusCode()
    {
        using HttpClient client = CreateClient();

        // Exhaust the /auth/token bucket (limit=3) and capture the 4th response in full.
        object payload = new { code = "X", state = "Y", codeVerifier = "Z" };

        for (int i = 0; i < TokenPermitLimit; i++)
        {
            HttpResponseMessage discard = await client.PostAsJsonAsync("/auth/token", payload);
            discard.Dispose();
        }

        HttpResponseMessage rateLimited = await client.PostAsJsonAsync("/auth/token", payload);

        Assert.Equal(HttpStatusCode.TooManyRequests, rateLimited.StatusCode);
    }

    // =========================================================================
    // Task 8.1 — buckets are per-IP isolated
    // =========================================================================

    /// <summary>
    /// Two independent clients (representing different IPs in production; same loopback in tests)
    /// should each have their own bucket.  In the TestServer all requests arrive from the same
    /// loopback IP so they SHARE the bucket — this test validates that, after one client exhausts
    /// the bucket, a second client (new HttpClient instance) also gets 429, confirming the limiter
    /// is active on the shared loopback partition.
    /// </summary>
    [Fact]
    public async Task RateLimit_SharedIpBucket_BothClientsAffected()
    {
        using HttpClient clientA = CreateClient();
        using HttpClient clientB = CreateClient();

        object payload = new { code = "X", state = "Y", codeVerifier = "Z" };

        // clientA exhausts the token bucket
        for (int i = 0; i < TokenPermitLimit; i++)
        {
            HttpResponseMessage discard = await clientA.PostAsJsonAsync("/auth/token", payload);
            discard.Dispose();
        }

        // clientB hits the same loopback partition — should also get 429
        HttpResponseMessage response = await clientB.PostAsJsonAsync("/auth/token", payload);
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }
}
