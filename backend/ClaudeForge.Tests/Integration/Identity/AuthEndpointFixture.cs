using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using ClaudeForge.Core.Identity.Ports;
using ClaudeForge.Core.Shared.Authorization;
using ClaudeForge.Infrastructure.Identity;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using ClaudeForge.Tests.Integration.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace ClaudeForge.Tests.Integration.Identity;

/// <summary>
/// Shared WebApplicationFactory fixture for auth-endpoint integration tests (Group 5, Tasks 5.2–5.9).
///
/// Responsibilities:
///   1. Boots the full ASP.NET Core application against a real test Postgres container.
///   2. Replaces IIdentityProviderPort / IIdentityProviderRegistry with mocks so no real
///      Google/Microsoft network call happens.
///   3. Wires a real RS256 key pair so JwtBearer validation works end-to-end.
///   4. Provides helper methods:
///        - CreateAuthenticatedClient(userId, email, displayName) → HttpClient with Bearer
///        - CreateUserAndIssueToken(…) → persists user + returns access token string
///        - CreateRefreshToken(userId) → persists a valid refresh token, returns plain value
///        - MakeAuthorizeUrl(provider) → mock returns a deterministic authorize URL
///
/// The mock IIdentityProviderRegistry.Resolve("google") returns a mock adapter that:
///   - BuildAuthorizationUrl(…) → "https://accounts.google.com/o/oauth2/v2/auth?state={state}&test=1"
///   - ExchangeCodeAsync("TEST_VALID_CODE", …) → returns raw id_token string "raw-id-token-google"
///   - ValidateIdTokenAsync("raw-id-token-google", …) → VerifiedIdentity("sub-google-001",
///       "user@test.example.com", true, "Test User")
///   - ExchangeCodeAsync("TEST_EXPIRED_CODE", …) → throws InvalidOperationException("expired")
///   - ExchangeCodeAsync("TEST_TAMPERED_CODE", …) → throws InvalidOperationException("invalid_grant")
///
/// ═══════════════════════════════════════════════════════════════════════════════
/// Contracts the coder MUST create (in addition to the ports already declared in G3/G4):
///
///   NAMESPACE: ClaudeForge.Core.Modules.Identity.UseCases
///
///   sealed class InitiateSignInUseCase
///     Constructor: (IIdentityProviderRegistry registry, IAuthFlowStatePort stateStore)
///     Method: Task&lt;InitiateSignInResult&gt; ExecuteAsync(string provider, string? customRedirectUri, CancellationToken ct)
///       - Resolves provider via registry (unknown → UnsupportedProviderException → HTTP 400)
///       - Generates state (min 32 URL-safe chars) + PKCE verifier/challenge (SHA-256 base64url)
///       - Calls IIdentityProviderPort.BuildAuthorizationUrl(provider, challenge, state, redirectUri)
///       - Stores AuthFlowState via IAuthFlowStatePort.StoreAsync (TTL = 5 min)
///       - Returns InitiateSignInResult { AuthorizationUrl, State }
///
///   sealed record InitiateSignInResult(string AuthorizationUrl, string State);
///
///   sealed class CompleteSignInUseCase
///     Constructor: (IIdentityProviderRegistry registry, IAuthFlowStatePort stateStore,
///                   IUserStorePort userStore, ITokenIssuerPort tokenIssuer,
///                   IRefreshTokenStorePort refreshStore, int refreshTokenDays)
///     Method: Task&lt;SignInTokens&gt; ExecuteAsync(string code, string state, CancellationToken ct)
///       - Consumes state from IAuthFlowStatePort (null/expired → InvalidOperationException → HTTP 401)
///       - Exchanges code via IIdentityProviderPort.ExchangeCodeAsync (error → HTTP 401)
///       - Validates id_token via IIdentityProviderPort.ValidateIdTokenAsync
///       - Provisions user via IUserStorePort.ProvisionOrLinkAsync
///       - Issues access JWT via ITokenIssuerPort.IssueAccessToken
///       - Creates refresh token via IRefreshTokenStorePort.CreateAsync
///       - Returns SignInTokens { AccessToken, RefreshToken, ExpiresAt }
///
///   sealed record SignInTokens(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);
///
///   sealed class RefreshTokensUseCase
///     Constructor: (IRefreshTokenStorePort refreshStore, ITokenIssuerPort tokenIssuer,
///                   IRevokedJtiStorePort jtiStore, int refreshTokenDays)
///     Method: Task&lt;SignInTokens&gt; ExecuteAsync(string plainRefreshToken, CancellationToken ct)
///       - FindByHash → null/expired/revoked → InvalidOperationException → HTTP 401
///       - Already-rotated token (rotated_to set) → call RevokeChainAsync → HTTP 401
///       - RotateAsync → issue new access JWT → create new refresh token
///       - Returns new SignInTokens
///
///   sealed class GetCurrentUserUseCase
///     Constructor: (IUserStorePort userStore, IOrgMembershipQueryPort membershipQuery)
///     Method: Task&lt;CurrentUserResponse&gt; ExecuteAsync(Guid userId, CancellationToken ct)
///
///   sealed record CurrentUserResponse(Guid UserId, string Email, string DisplayName,
///       IReadOnlyList&lt;OrgMembershipSummary&gt; OrgMemberships);
///
///   sealed record OrgMembershipSummary(Guid OrgId, string OrgName, string Role);
///
///   sealed class SignOutUseCase
///     Constructor: (IRefreshTokenStorePort refreshStore, IRevokedJtiStorePort jtiStore)
///     Method: Task ExecuteAsync(string plainRefreshToken, string? accessJti,
///                               DateTimeOffset? accessExpiresAt, CancellationToken ct)
///       - RevokeChainAsync on refresh token
///       - If jti non-null: IRevokedJtiStorePort.AddAsync(jti, expiresAt)
///
///   sealed class IssueDeviceCodeUseCase
///     Method: Task&lt;DeviceCodeResponse&gt; ExecuteAsync(string provider, CancellationToken ct)
///       - Issues user_code (8 chars uppercase alphanumeric) + device_code (opaque)
///       - verification_url = "{issuer}/activate"
///       - Stores pending device authorization state (in-memory or DB) with 15-min TTL
///
///   sealed record DeviceCodeResponse(string DeviceCode, string UserCode,
///       string VerificationUrl, int ExpiresIn, int Interval);
///
///   sealed class PollDeviceTokenUseCase
///     Method: Task&lt;DeviceTokenPollResult&gt; ExecuteAsync(string deviceCode, CancellationToken ct)
///       - Unknown/expired device_code → DeviceTokenPollResult.Expired
///       - Pending (user hasn't approved yet) → DeviceTokenPollResult.Pending
///       - Approved → DeviceTokenPollResult.Approved(SignInTokens)
///
///   abstract record DeviceTokenPollResult
///     sealed record Pending    : DeviceTokenPollResult
///     sealed record SlowDown   : DeviceTokenPollResult
///     sealed record Approved(SignInTokens Tokens) : DeviceTokenPollResult
///     sealed record Expired    : DeviceTokenPollResult
///
///   JwtBearer wiring (in IdentityModule or Program):
///     AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
///       .AddJwtBearer(options =>
///         options.TokenValidationParameters = ITokenIssuerPort.GetValidationParameters() or equiv)
///     Authorization policy "RequireAuthenticatedUser":
///       AddAuthorization(opts => opts.AddPolicy("RequireAuthenticatedUser",
///         p => p.RequireAuthenticatedUser()))
///
///   HttpContextCurrentUser is already implemented — register as Scoped ICurrentUser.
///
///   Endpoints (all under /auth route prefix, no api/v1 version prefix):
///     GET  /auth/authorize?provider={provider}[&redirect_uri={uri}]
///     GET  /auth/callback?code={code}&state={state}
///     POST /auth/token  body: { "code": "...", "state": "...", "codeVerifier": "..." }
///     POST /auth/refresh  body: { "refreshToken": "..." }
///     GET  /auth/me  [Authorize]
///     POST /auth/signout  body: { "refreshToken": "..." }  [Authorize]
///     POST /auth/device/code  body: { "provider": "google" }
///     POST /auth/device/token  body: { "deviceCode": "..." }
/// ═══════════════════════════════════════════════════════════════════════════════
/// </summary>
[CollectionDefinition(AuthEndpointFixture.CollectionName)]
public sealed class AuthEndpointCollection : ICollectionFixture<PostgresFixture> { }

public static class AuthEndpointFixture
{
    public const string CollectionName = "Postgres";

    // Test RS256 key pair — generated once per process, reused across all auth tests.
    public static readonly (string PrivatePem, string PublicPem, string Kid) TestKey = GenerateTestKey();

    // Well-known test code values that the mock OIDC adapter recognises.
    public const string ValidCode = "TEST_VALID_CODE";
    public const string ExpiredCode = "TEST_EXPIRED_CODE";
    public const string TamperedCode = "TEST_TAMPERED_CODE";
    public const string PkceMismatchCode = "TEST_PKCE_MISMATCH_CODE";

    // The deterministic identity returned by the mock for ValidCode.
    public const string MockSubject = "sub-google-001";
    public const string MockEmail = "user@test.example.com";
    public const string MockDisplayName = "Test User";
    public const string MockProvider = "google";

    private static (string PrivatePem, string PublicPem, string Kid) GenerateTestKey()
    {
        using RSA rsa = RSA.Create(keySizeInBits: 2048);
        return (rsa.ExportPkcs8PrivateKeyPem(), rsa.ExportRSAPublicKeyPem(), "auth-test-kid-1");
    }

    /// <summary>
    /// Creates a WebApplicationFactory with:
    ///   - Test Postgres (from PostgresFixture)
    ///   - Real RsaTokenIssuerAdapter using <see cref="TestKey"/>
    ///   - Mock IIdentityProviderRegistry with deterministic OIDC behavior
    ///   - Mock IAuthFlowStatePort per-request (no real state needed for token tests)
    ///   - Scoped ICurrentUser via HttpContextCurrentUser (from JWT claims)
    /// </summary>
    public static WebApplicationFactory<Program> CreateFactory(PostgresFixture pgFixture)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    // Supply JWT signing key so RsaTokenIssuerAdapter is registered with our test key
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Jwt:PrivatePem"] = TestKey.PrivatePem,
                        ["Jwt:PublicPem"] = TestKey.PublicPem,
                        ["Jwt:Kid"] = TestKey.Kid,
                        ["Jwt:Issuer"] = "https://claudeforge.test",
                        ["Jwt:Audience"] = "claudeforge-api-test",
                        ["Jwt:AccessTokenMinutes"] = "15",
                        ["Jwt:RefreshTokenDays"] = "30",
                        ["OIDC__ENABLEDPROVIDERS"] = "google",
                        // Dummy OIDC values so OidcConfigValidator does not fail startup
                        ["OIDC__GOOGLE__CLIENTID"] = "test-client-id",
                        ["OIDC__GOOGLE__CLIENTSECRET"] = "test-client-secret",
                        ["OIDC__GOOGLE__REDIRECTURI"] = "https://app.test/auth/callback",
                    });
                });

                builder.ConfigureServices(services =>
                {
                    // ── Replace DbContext with test Postgres ──────────────────────────
                    ServiceDescriptor? dbOpts = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<MarketplaceDbContext>));
                    if (dbOpts is not null) services.Remove(dbOpts);

                    ServiceDescriptor? dbCtx = services.SingleOrDefault(
                        d => d.ServiceType == typeof(MarketplaceDbContext));
                    if (dbCtx is not null) services.Remove(dbCtx);

                    services.AddDbContext<MarketplaceDbContext>(opts =>
                        opts.UseNpgsql(pgFixture.ConnectionString));

                    // ── Replace IIdentityProviderRegistry with a deterministic mock ──
                    ServiceDescriptor? registryDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(IIdentityProviderRegistry));
                    if (registryDescriptor is not null) services.Remove(registryDescriptor);

                    IIdentityProviderPort mockProvider = BuildMockOidcProvider();
                    IIdentityProviderRegistry mockRegistry = BuildMockRegistry(mockProvider);
                    services.AddSingleton(mockRegistry);

                    // ── Replace individual IIdentityProviderPort registrations ────────
                    // Remove all existing IIdentityProviderPort registrations
                    List<ServiceDescriptor> providerDescs = services
                        .Where(d => d.ServiceType == typeof(IIdentityProviderPort))
                        .ToList();
                    foreach (ServiceDescriptor d in providerDescs)
                        services.Remove(d);

                    services.AddSingleton(mockProvider);

                    // ── Ensure RsaJwksProvider uses our test public key ───────────────
                    ServiceDescriptor? jwksDesc = services.SingleOrDefault(
                        d => d.ServiceType == typeof(IJwksProvider));
                    if (jwksDesc is not null) services.Remove(jwksDesc);
                    services.AddSingleton<IJwksProvider>(
                        _ => new RsaJwksProvider([(TestKey.PublicPem, TestKey.Kid)]));
                });
            });
    }

    private static IIdentityProviderPort BuildMockOidcProvider()
    {
        IIdentityProviderPort mock = Substitute.For<IIdentityProviderPort>();

        // BuildAuthorizationUrl → deterministic URL containing state and test marker
        mock.BuildAuthorizationUrl(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(callInfo =>
            {
                string state = callInfo.ArgAt<string>(2);
                return $"https://accounts.google.com/o/oauth2/v2/auth?state={state}&test=1";
            });

        // ExchangeCodeAsync — code-specific behavior
        mock.ExchangeCodeAsync(Arg.Any<string>(), ValidCode, Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult("raw-id-token-google"));

        mock.ExchangeCodeAsync(Arg.Any<string>(), ExpiredCode, Arg.Any<string>(), Arg.Any<string>())
            .ThrowsAsync(new InvalidOperationException("Code expired"));

        mock.ExchangeCodeAsync(Arg.Any<string>(), TamperedCode, Arg.Any<string>(), Arg.Any<string>())
            .ThrowsAsync(new InvalidOperationException("invalid_grant: code tampered"));

        mock.ExchangeCodeAsync(Arg.Any<string>(), PkceMismatchCode, Arg.Any<string>(), Arg.Any<string>())
            .ThrowsAsync(new InvalidOperationException("PKCE verification failed"));

        // ValidateIdTokenAsync — only the happy-path raw token is recognized
        mock.ValidateIdTokenAsync(Arg.Any<string>(), "raw-id-token-google")
            .Returns(Task.FromResult(new VerifiedIdentity(
                Subject: MockSubject,
                Email: MockEmail,
                EmailVerified: true,
                Name: MockDisplayName)));

        return mock;
    }

    private static IIdentityProviderRegistry BuildMockRegistry(IIdentityProviderPort provider)
    {
        IIdentityProviderRegistry mock = Substitute.For<IIdentityProviderRegistry>();

        // Known provider → the mock adapter
        mock.Resolve(MockProvider).Returns(provider);
        mock.Resolve("google").Returns(provider);

        // Unknown provider → throws UnsupportedProviderException
        mock.Resolve(Arg.Is<string>(n => n != "google" && n != MockProvider))
            .Throws(new UnsupportedProviderException("unknown-provider"));

        return mock;
    }

    /// <summary>
    /// Issues a valid RS256 access JWT for the given user, suitable for use as a Bearer token.
    /// Uses the same RsaTokenIssuerAdapter configuration as the factory.
    /// </summary>
    public static string IssueTestAccessToken(
        Guid userId,
        string email,
        string displayName,
        string provider = MockProvider)
    {
        using RSA rsa = RSA.Create();
        rsa.ImportFromPem(TestKey.PrivatePem);
        RsaTokenIssuerAdapter issuer = new(
            privatePem: TestKey.PrivatePem,
            issuer: "https://claudeforge.test",
            audience: "claudeforge-api-test",
            accessTokenMinutes: 15,
            kid: TestKey.Kid);

        return issuer.IssueAccessToken(new AccessTokenClaims(userId, email, displayName, provider));
    }

    /// <summary>
    /// Attaches a Bearer token to the client's default request headers.
    /// </summary>
    public static void SetBearerToken(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Removes any Authorization header (simulates unauthenticated request).
    /// </summary>
    public static void ClearBearerToken(HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization = null;
    }

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}
