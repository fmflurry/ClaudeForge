using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using ClaudeForge.Core.Identity.Ports;
using ClaudeForge.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;

namespace ClaudeForge.Tests.Unit.Identity;

/// <summary>
/// Unit tests for Group 4, Task 4.3 — Google + Microsoft adapter behaviour.
///
/// These tests are RED because the following production types DO NOT YET EXIST.
/// The coder MUST create them to turn RED → GREEN.
///
/// ─── Infrastructure (ClaudeForge.Infrastructure.Identity) ────────────────────
///
///   sealed class GoogleIdentityProviderAdapter : IIdentityProviderPort, INamedIdentityProviderPort
///     const string ProviderName = "google"
///     GoogleIdentityProviderAdapter(IConfiguration configuration, HttpClient httpClient, IOpenIdConfigurationProvider openIdConfigProvider)
///
///     string BuildAuthorizationUrl(string provider, string codeChallenge, string state, string redirectUri)
///       → returns URL with:
///           - base: https://accounts.google.com/o/oauth2/v2/auth
///           - client_id from OIDC__GOOGLE__CLIENTID
///           - response_type=code
///           - scope=openid email profile
///           - redirect_uri (exact, URL-encoded)
///           - code_challenge (URL-encoded)
///           - code_challenge_method=S256
///           - state (URL-encoded)
///
///     Task&lt;string&gt; ExchangeCodeAsync(string provider, string code, string codeVerifier, string redirectUri, CancellationToken ct)
///       → POST https://oauth2.googleapis.com/token
///           form: grant_type=authorization_code, code, client_id, client_secret, redirect_uri, code_verifier
///       → returns id_token string from JSON response body
///       → on HTTP error → throws InvalidOperationException (or a dedicated TokenExchangeException)
///
///     Task&lt;VerifiedIdentity&gt; ValidateIdTokenAsync(string provider, string rawIdToken, CancellationToken ct)
///       → validates JWT signature via IOpenIdConfigurationProvider (mocked JWKS in tests)
///       → validates iss, aud (OIDC__GOOGLE__CLIENTID), exp
///       → returns VerifiedIdentity{subject=sub, email, emailVerified=email_verified, name}
///       → expired token → throws SecurityTokenExpiredException
///       → tampered payload (modified after signing) → throws SecurityTokenException
///       → PKCE verifier mismatch is caught upstream (adapter validates token, caller must verify code exchange succeeds)
///
///   sealed class MicrosoftIdentityProviderAdapter : IIdentityProviderPort, INamedIdentityProviderPort
///     const string ProviderName = "microsoft"
///     MicrosoftIdentityProviderAdapter(IConfiguration configuration, HttpClient httpClient, IOpenIdConfigurationProvider openIdConfigProvider)
///
///     string BuildAuthorizationUrl(string provider, string codeChallenge, string state, string redirectUri)
///       → returns URL with:
///           - base: https://login.microsoftonline.com/{tenant}/oauth2/v2.0/authorize
///               where tenant = OIDC__MICROSOFT__TENANT (default "common")
///           - client_id from OIDC__MICROSOFT__CLIENTID
///           - response_type=code
///           - scope=openid email profile
///           - redirect_uri (exact, URL-encoded)
///           - code_challenge (URL-encoded)
///           - code_challenge_method=S256
///           - state (URL-encoded)
///
///     Task&lt;string&gt; ExchangeCodeAsync(string provider, string code, string codeVerifier, string redirectUri, CancellationToken ct)
///       → POST https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token
///       → returns id_token string from JSON response
///
///     Task&lt;VerifiedIdentity&gt; ValidateIdTokenAsync(string provider, string rawIdToken, CancellationToken ct)
///       → same validation contract as Google adapter
///
///   interface IOpenIdConfigurationProvider
///     /// Returns the signing keys for validating id_tokens from the named provider.
///     /// In production: wraps ConfigurationManager&lt;OpenIdConnectConfiguration&gt; with 24h cache.
///     /// In tests: returns a fake JWKS based on a test RSA key.
///     Task&lt;IEnumerable&lt;SecurityKey&gt;&gt; GetSigningKeysAsync(string provider, CancellationToken ct = default)
///     string GetIssuer(string provider)
///
/// ─── Test strategy ───────────────────────────────────────────────────────────
///   - All tests are pure unit tests (no real network).
///   - HttpClient is backed by a TestHttpMessageHandler that returns canned responses.
///   - IOpenIdConfigurationProvider is mocked to return a test RSA key's JWKS.
///   - id_tokens are created with a test RSA key (TestRsaKey below).
///   - PKCE code_verifier → code_challenge computed with SHA-256 per RFC 7636.
/// </summary>
public sealed class OidcAdapterTests : IDisposable
{
    // =========================================================================
    // Test RSA key — used to sign test id_tokens and mock JWKS returns
    // =========================================================================

    private readonly RSA _testRsaKey;
    private readonly RsaSecurityKey _testSecurityKey;
    private readonly SigningCredentials _testSigningCredentials;

    public OidcAdapterTests()
    {
        _testRsaKey = RSA.Create(2048);
        _testSecurityKey = new RsaSecurityKey(_testRsaKey)
        {
            KeyId = "test-kid-001",
        };
        _testSigningCredentials = new SigningCredentials(
            _testSecurityKey,
            SecurityAlgorithms.RsaSha256);
    }

    public void Dispose() => _testRsaKey.Dispose();

    // =========================================================================
    // Helpers
    // =========================================================================

    private static string MakeCodeVerifier() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string ComputeCodeChallenge(string verifier)
    {
        byte[] hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>
    /// Mints a signed test JWT id_token using the test RSA key.
    /// When <paramref name="expiry"/> is in the past the helper shifts notBefore
    /// to two hours before expiry so that expires &gt; notBefore (required by the
    /// JwtSecurityToken constructor) while the token is still genuinely expired
    /// at the moment the production adapter validates it.
    /// </summary>
    private string MintIdToken(
        string issuer,
        string audience,
        string subject,
        string email,
        bool emailVerified,
        string name,
        DateTimeOffset? expiry = null)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset effectiveExpiry = expiry ?? now.AddMinutes(5);

        // For expired tokens (expiry in the past), set notBefore two hours before
        // expiry so that the constructor constraint expires > notBefore is satisfied
        // while the resulting token is still past its expiry when validated.
        DateTime notBefore = effectiveExpiry < now
            ? effectiveExpiry.AddHours(-2).UtcDateTime
            : now.UtcDateTime;

        JwtSecurityToken token = new(
            issuer: issuer,
            audience: audience,
            claims: new[]
            {
                new Claim("sub", subject),
                new Claim("email", email),
                new Claim("email_verified", emailVerified.ToString().ToLowerInvariant()),
                new Claim("name", name),
            },
            notBefore: notBefore,
            expires: effectiveExpiry.UtcDateTime,
            signingCredentials: _testSigningCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private IOpenIdConfigurationProvider MakeOpenIdConfigProvider(
        string issuer, string providerName = "google") =>
        MakeOpenIdConfigProviderWithKey(issuer, _testSecurityKey, providerName);

    private static IOpenIdConfigurationProvider MakeOpenIdConfigProviderWithKey(
        string issuer,
        SecurityKey key,
        string providerName = "google")
    {
        IOpenIdConfigurationProvider mock = Substitute.For<IOpenIdConfigurationProvider>();
        mock.GetSigningKeysAsync(providerName, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<SecurityKey>>(new[] { key }));
        mock.GetIssuer(providerName).Returns(issuer);
        return mock;
    }

    private static IConfiguration MakeGoogleConfig(
        string clientId = "google-client-id",
        string clientSecret = "google-client-secret",
        string redirectUri = "https://app.example.com/auth/callback") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OIDC__GOOGLE__CLIENTID"] = clientId,
                ["OIDC__GOOGLE__CLIENTSECRET"] = clientSecret,
                ["OIDC__GOOGLE__REDIRECTURI"] = redirectUri,
            })
            .Build();

    private static IConfiguration MakeMicrosoftConfig(
        string clientId = "ms-client-id",
        string clientSecret = "ms-client-secret",
        string redirectUri = "https://app.example.com/auth/callback",
        string tenant = "common") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OIDC__MICROSOFT__CLIENTID"] = clientId,
                ["OIDC__MICROSOFT__CLIENTSECRET"] = clientSecret,
                ["OIDC__MICROSOFT__REDIRECTURI"] = redirectUri,
                ["OIDC__MICROSOFT__TENANT"] = tenant,
            })
            .Build();

    private static HttpClient MakeHttpClient(HttpMessageHandler handler) =>
        new(handler);

    // =========================================================================
    // GOOGLE — BuildAuthorizationUrl
    // =========================================================================

    [Fact]
    public void Google_BuildAuthorizationUrl_ContainsCorrectBaseUrl()
    {
        // Arrange
        IConfiguration config = MakeGoogleConfig(clientId: "gcid-test");
        IOpenIdConfigurationProvider openIdProvider = MakeOpenIdConfigProvider("https://accounts.google.com");
        GoogleIdentityProviderAdapter adapter = new(config, MakeHttpClient(new TestHttpMessageHandler()), openIdProvider);
        string verifier = MakeCodeVerifier();
        string challenge = ComputeCodeChallenge(verifier);

        // Act
        string url = adapter.BuildAuthorizationUrl(
            "google", challenge, "state-abc", "https://app.example.com/auth/callback");

        // Assert — base domain + path
        Assert.StartsWith("https://accounts.google.com/o/oauth2/v2/auth", url);
    }

    [Fact]
    public void Google_BuildAuthorizationUrl_HasClientId()
    {
        // Arrange
        IConfiguration config = MakeGoogleConfig(clientId: "gcid-xyz");
        IOpenIdConfigurationProvider openIdProvider = MakeOpenIdConfigProvider("https://accounts.google.com");
        GoogleIdentityProviderAdapter adapter = new(config, MakeHttpClient(new TestHttpMessageHandler()), openIdProvider);
        string verifier = MakeCodeVerifier();
        string challenge = ComputeCodeChallenge(verifier);

        // Act
        string url = adapter.BuildAuthorizationUrl(
            "google", challenge, "state-abc", "https://app.example.com/auth/callback");

        // Assert — client_id must appear in query string
        Uri uri = new(url);
        string query = HttpUtility.UrlDecode(uri.Query);
        Assert.Contains("client_id=gcid-xyz", query);
    }

    [Fact]
    public void Google_BuildAuthorizationUrl_HasPkceCodeChallenge()
    {
        // Arrange
        IConfiguration config = MakeGoogleConfig();
        IOpenIdConfigurationProvider openIdProvider = MakeOpenIdConfigProvider("https://accounts.google.com");
        GoogleIdentityProviderAdapter adapter = new(config, MakeHttpClient(new TestHttpMessageHandler()), openIdProvider);
        string verifier = MakeCodeVerifier();
        string challenge = ComputeCodeChallenge(verifier);

        // Act
        string url = adapter.BuildAuthorizationUrl(
            "google", challenge, "state-xyz", "https://app.example.com/auth/callback");

        // Assert — PKCE challenge and method must be present
        Uri uri = new(url);
        string query = HttpUtility.UrlDecode(uri.Query);
        Assert.Contains($"code_challenge={challenge}", query);
        Assert.Contains("code_challenge_method=S256", query);
    }

    [Fact]
    public void Google_BuildAuthorizationUrl_HasStateParameter()
    {
        // Arrange
        IConfiguration config = MakeGoogleConfig();
        IOpenIdConfigurationProvider openIdProvider = MakeOpenIdConfigProvider("https://accounts.google.com");
        GoogleIdentityProviderAdapter adapter = new(config, MakeHttpClient(new TestHttpMessageHandler()), openIdProvider);
        string verifier = MakeCodeVerifier();
        string challenge = ComputeCodeChallenge(verifier);
        string state = "csrf-state-token-12345";

        // Act
        string url = adapter.BuildAuthorizationUrl(
            "google", challenge, state, "https://app.example.com/auth/callback");

        // Assert
        Uri uri = new(url);
        string query = HttpUtility.UrlDecode(uri.Query);
        Assert.Contains($"state={state}", query);
    }

    [Fact]
    public void Google_BuildAuthorizationUrl_HasRedirectUri()
    {
        // Arrange
        IConfiguration config = MakeGoogleConfig();
        IOpenIdConfigurationProvider openIdProvider = MakeOpenIdConfigProvider("https://accounts.google.com");
        GoogleIdentityProviderAdapter adapter = new(config, MakeHttpClient(new TestHttpMessageHandler()), openIdProvider);
        string verifier = MakeCodeVerifier();
        string challenge = ComputeCodeChallenge(verifier);
        string redirectUri = "https://app.example.com/auth/callback";

        // Act
        string url = adapter.BuildAuthorizationUrl("google", challenge, "state", redirectUri);

        // Assert
        Uri uri = new(url);
        string query = HttpUtility.UrlDecode(uri.Query);
        Assert.Contains($"redirect_uri={redirectUri}", query);
    }

    [Fact]
    public void Google_BuildAuthorizationUrl_HasResponseTypeCode()
    {
        // Arrange
        IConfiguration config = MakeGoogleConfig();
        IOpenIdConfigurationProvider openIdProvider = MakeOpenIdConfigProvider("https://accounts.google.com");
        GoogleIdentityProviderAdapter adapter = new(config, MakeHttpClient(new TestHttpMessageHandler()), openIdProvider);
        string challenge = ComputeCodeChallenge(MakeCodeVerifier());

        // Act
        string url = adapter.BuildAuthorizationUrl(
            "google", challenge, "state", "https://app.example.com/auth/callback");

        // Assert
        Uri uri = new(url);
        string query = HttpUtility.UrlDecode(uri.Query);
        Assert.Contains("response_type=code", query);
    }

    [Fact]
    public void Google_BuildAuthorizationUrl_HasOpenIdScope()
    {
        // Arrange
        IConfiguration config = MakeGoogleConfig();
        IOpenIdConfigurationProvider openIdProvider = MakeOpenIdConfigProvider("https://accounts.google.com");
        GoogleIdentityProviderAdapter adapter = new(config, MakeHttpClient(new TestHttpMessageHandler()), openIdProvider);
        string challenge = ComputeCodeChallenge(MakeCodeVerifier());

        // Act
        string url = adapter.BuildAuthorizationUrl(
            "google", challenge, "state", "https://app.example.com/auth/callback");

        // Assert — must include openid, email, profile scopes
        Uri uri = new(url);
        string query = HttpUtility.UrlDecode(uri.Query);
        Assert.Contains("openid", query);
        Assert.Contains("email", query);
        Assert.Contains("profile", query);
    }

    // =========================================================================
    // GOOGLE — ExchangeCodeAsync
    // =========================================================================

    [Fact]
    public async Task Google_ExchangeCode_OnSuccess_ReturnsIdToken()
    {
        // Arrange — HttpClient returns a canned token endpoint response
        string rawIdToken = MintIdToken(
            "https://accounts.google.com",
            "google-client-id",
            "sub-123", "alice@example.com", true, "Alice");

        TestHttpMessageHandler handler = new(new Dictionary<string, string>
        {
            ["https://oauth2.googleapis.com/token"] =
                $$"""{"id_token":"{{rawIdToken}}","access_token":"at-xyz","token_type":"Bearer","expires_in":3600}""",
        });

        IConfiguration config = MakeGoogleConfig(clientId: "google-client-id");
        IOpenIdConfigurationProvider openIdProvider = MakeOpenIdConfigProvider("https://accounts.google.com");
        GoogleIdentityProviderAdapter adapter = new(config, MakeHttpClient(handler), openIdProvider);
        string verifier = MakeCodeVerifier();

        // Act
        string result = await adapter.ExchangeCodeAsync(
            "google", "auth-code-abc", verifier, "https://app.example.com/auth/callback");

        // Assert — must return the raw id_token string
        Assert.Equal(rawIdToken, result);
    }

    [Fact]
    public async Task Google_ExchangeCode_OnHttpError_ThrowsException()
    {
        // Arrange — token endpoint returns 400
        TestHttpMessageHandler handler = new(statusCode: System.Net.HttpStatusCode.BadRequest);
        IConfiguration config = MakeGoogleConfig();
        IOpenIdConfigurationProvider openIdProvider = MakeOpenIdConfigProvider("https://accounts.google.com");
        GoogleIdentityProviderAdapter adapter = new(config, MakeHttpClient(handler), openIdProvider);

        // Act & Assert — any exchange failure must throw (not silently return null/empty)
        await Assert.ThrowsAnyAsync<Exception>(() =>
            adapter.ExchangeCodeAsync("google", "bad-code", MakeCodeVerifier(), "https://app.example.com/auth/callback"));
    }

    [Fact]
    public async Task Google_ExchangeCode_PostsToCorrectTokenEndpoint()
    {
        // Arrange — capture which URL was called
        string rawIdToken = MintIdToken(
            "https://accounts.google.com", "google-client-id",
            "sub-456", "bob@example.com", true, "Bob");

        CapturingHttpMessageHandler handler = new(
            $$"""{"id_token":"{{rawIdToken}}","access_token":"at","token_type":"Bearer","expires_in":3600}""");
        IConfiguration config = MakeGoogleConfig(clientId: "google-client-id");
        IOpenIdConfigurationProvider openIdProvider = MakeOpenIdConfigProvider("https://accounts.google.com");
        GoogleIdentityProviderAdapter adapter = new(config, MakeHttpClient(handler), openIdProvider);
        string verifier = MakeCodeVerifier();

        // Act
        await adapter.ExchangeCodeAsync(
            "google", "code-xyz", verifier, "https://app.example.com/auth/callback");

        // Assert — correct endpoint must have been called
        Assert.Equal("https://oauth2.googleapis.com/token", handler.LastRequestUrl);
    }

    // =========================================================================
    // GOOGLE — ValidateIdTokenAsync (valid, expired, tampered)
    // =========================================================================

    [Fact]
    public async Task Google_ValidateIdToken_ValidToken_ReturnsVerifiedIdentity()
    {
        // Arrange
        const string ClientId = "google-client-id";
        const string Subject = "google-sub-001";
        const string Email = "carol@example.com";
        const string Name = "Carol";

        string rawToken = MintIdToken(
            "https://accounts.google.com", ClientId, Subject, Email, true, Name);

        IConfiguration config = MakeGoogleConfig(clientId: ClientId);
        IOpenIdConfigurationProvider openIdProvider = MakeOpenIdConfigProvider(
            "https://accounts.google.com", "google");
        GoogleIdentityProviderAdapter adapter = new(config, MakeHttpClient(new TestHttpMessageHandler()), openIdProvider);

        // Act
        VerifiedIdentity identity = await adapter.ValidateIdTokenAsync("google", rawToken);

        // Assert — all claims must be extracted correctly
        Assert.Equal(Subject, identity.Subject);
        Assert.Equal(Email, identity.Email);
        Assert.True(identity.EmailVerified);
        Assert.Equal(Name, identity.Name);
    }

    [Fact]
    public async Task Google_ValidateIdToken_ExpiredToken_ThrowsSecurityTokenExpiredException()
    {
        // Arrange — token expired 1 hour ago
        const string ClientId = "google-client-id";
        string expiredToken = MintIdToken(
            "https://accounts.google.com", ClientId,
            "sub-expired", "expired@example.com", true, "Expired",
            expiry: DateTimeOffset.UtcNow.AddHours(-1));

        IConfiguration config = MakeGoogleConfig(clientId: ClientId);
        IOpenIdConfigurationProvider openIdProvider = MakeOpenIdConfigProvider(
            "https://accounts.google.com", "google");
        GoogleIdentityProviderAdapter adapter = new(config, MakeHttpClient(new TestHttpMessageHandler()), openIdProvider);

        // Act & Assert
        await Assert.ThrowsAnyAsync<SecurityTokenExpiredException>(() =>
            adapter.ValidateIdTokenAsync("google", expiredToken));
    }

    [Fact]
    public async Task Google_ValidateIdToken_TamperedPayload_ThrowsSecurityTokenException()
    {
        // Arrange — take a valid token and corrupt the payload segment
        const string ClientId = "google-client-id";
        string validToken = MintIdToken(
            "https://accounts.google.com", ClientId,
            "sub-tamper", "tamper@example.com", true, "Tamper");

        // Tamper: split header.payload.signature, modify payload, re-join
        string[] parts = validToken.Split('.');
        string corruptPayload = parts[1] + "CORRUPTION";
        string tamperedToken = $"{parts[0]}.{corruptPayload}.{parts[2]}";

        IConfiguration config = MakeGoogleConfig(clientId: ClientId);
        IOpenIdConfigurationProvider openIdProvider = MakeOpenIdConfigProvider(
            "https://accounts.google.com", "google");
        GoogleIdentityProviderAdapter adapter = new(config, MakeHttpClient(new TestHttpMessageHandler()), openIdProvider);

        // Act & Assert — signature verification must fail
        await Assert.ThrowsAnyAsync<SecurityTokenException>(() =>
            adapter.ValidateIdTokenAsync("google", tamperedToken));
    }

    [Fact]
    public async Task Google_ValidateIdToken_WrongAudience_ThrowsSecurityTokenException()
    {
        // Arrange — token issued for a different client_id
        string tokenForOtherClient = MintIdToken(
            "https://accounts.google.com",
            audience: "other-client-id",
            "sub-wrongaud", "wrongaud@example.com", true, "WrongAud");

        IConfiguration config = MakeGoogleConfig(clientId: "google-client-id");
        IOpenIdConfigurationProvider openIdProvider = MakeOpenIdConfigProvider(
            "https://accounts.google.com", "google");
        GoogleIdentityProviderAdapter adapter = new(config, MakeHttpClient(new TestHttpMessageHandler()), openIdProvider);

        // Act & Assert — audience mismatch must fail validation
        await Assert.ThrowsAnyAsync<SecurityTokenException>(() =>
            adapter.ValidateIdTokenAsync("google", tokenForOtherClient));
    }

    [Fact]
    public async Task Google_ValidateIdToken_WrongIssuer_ThrowsSecurityTokenException()
    {
        // Arrange — token from a different issuer (not Google)
        const string ClientId = "google-client-id";
        string tokenWrongIssuer = MintIdToken(
            "https://evil.example.com",
            ClientId,
            "sub-wrongiss", "wrongiss@example.com", true, "WrongIss");

        IConfiguration config = MakeGoogleConfig(clientId: ClientId);
        IOpenIdConfigurationProvider openIdProvider = MakeOpenIdConfigProvider(
            "https://accounts.google.com", "google");
        GoogleIdentityProviderAdapter adapter = new(config, MakeHttpClient(new TestHttpMessageHandler()), openIdProvider);

        // Act & Assert — issuer mismatch must fail
        await Assert.ThrowsAnyAsync<SecurityTokenException>(() =>
            adapter.ValidateIdTokenAsync("google", tokenWrongIssuer));
    }

    [Fact]
    public async Task Google_ValidateIdToken_TokenSignedWithWrongKey_ThrowsSecurityTokenException()
    {
        // Arrange — token signed by a different (attacker) RSA key
        const string ClientId = "google-client-id";
        using RSA attackerKey = RSA.Create(2048);
        RsaSecurityKey attackerSecurityKey = new(attackerKey) { KeyId = "attacker-kid" };
        SigningCredentials attackerCreds = new(attackerSecurityKey, SecurityAlgorithms.RsaSha256);

        JwtSecurityToken attackerToken = new(
            issuer: "https://accounts.google.com",
            audience: ClientId,
            claims: new[] { new Claim("sub", "sub-attacker"), new Claim("email", "att@evil.com") },
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: attackerCreds);
        string tokenSignedByAttacker = new JwtSecurityTokenHandler().WriteToken(attackerToken);

        IConfiguration config = MakeGoogleConfig(clientId: ClientId);
        // JWKS returns the legitimate test key, not attacker's key
        IOpenIdConfigurationProvider openIdProvider = MakeOpenIdConfigProvider(
            "https://accounts.google.com", "google");
        GoogleIdentityProviderAdapter adapter = new(config, MakeHttpClient(new TestHttpMessageHandler()), openIdProvider);

        // Act & Assert — must reject token signed by wrong key
        await Assert.ThrowsAnyAsync<SecurityTokenException>(() =>
            adapter.ValidateIdTokenAsync("google", tokenSignedByAttacker));
    }

    [Fact]
    public async Task Google_ValidateIdToken_MalformedToken_ThrowsException()
    {
        // Arrange — not a valid JWT at all
        IConfiguration config = MakeGoogleConfig();
        IOpenIdConfigurationProvider openIdProvider = MakeOpenIdConfigProvider("https://accounts.google.com");
        GoogleIdentityProviderAdapter adapter = new(config, MakeHttpClient(new TestHttpMessageHandler()), openIdProvider);

        // Act & Assert — must throw on non-JWT input
        await Assert.ThrowsAnyAsync<Exception>(() =>
            adapter.ValidateIdTokenAsync("google", "not.a.jwt"));
    }

    // =========================================================================
    // GOOGLE — PKCE verifier mismatch (rejected at token exchange level)
    // The adapter's ExchangeCodeAsync sends the verifier to Google's token endpoint.
    // When the verifier doesn't match the original challenge, Google returns 400.
    // This ensures the adapter surfaces that error.
    // =========================================================================

    [Fact]
    public async Task Google_ExchangeCode_PkceVerifierMismatch_ThrowsOnHttpError()
    {
        // Arrange — simulate Google rejecting due to PKCE mismatch (HTTP 400)
        TestHttpMessageHandler handler = new(statusCode: System.Net.HttpStatusCode.BadRequest,
            body: """{"error":"invalid_grant","error_description":"Code verifier does not match code challenge."}""");
        IConfiguration config = MakeGoogleConfig();
        IOpenIdConfigurationProvider openIdProvider = MakeOpenIdConfigProvider("https://accounts.google.com");
        GoogleIdentityProviderAdapter adapter = new(config, MakeHttpClient(handler), openIdProvider);

        // Use a verifier that does NOT match the challenge used during authorize
        string wrongVerifier = MakeCodeVerifier(); // different from what was used to compute challenge

        // Act & Assert — must throw (not silently return null/empty)
        await Assert.ThrowsAnyAsync<Exception>(() =>
            adapter.ExchangeCodeAsync("google", "valid-code", wrongVerifier, "https://app.example.com/auth/callback"));
    }

    // =========================================================================
    // MICROSOFT — BuildAuthorizationUrl
    // =========================================================================

    [Fact]
    public void Microsoft_BuildAuthorizationUrl_ContainsCorrectBaseUrl_WithCommonTenant()
    {
        // Arrange
        IConfiguration config = MakeMicrosoftConfig(tenant: "common");
        IOpenIdConfigurationProvider openIdProvider = MakeOpenIdConfigProviderWithKey(
            "https://login.microsoftonline.com/common/v2.0", _testSecurityKey, "microsoft");
        MicrosoftIdentityProviderAdapter adapter = new(config, MakeHttpClient(new TestHttpMessageHandler()), openIdProvider);
        string challenge = ComputeCodeChallenge(MakeCodeVerifier());

        // Act
        string url = adapter.BuildAuthorizationUrl(
            "microsoft", challenge, "state-ms", "https://app.example.com/auth/callback");

        // Assert — must use the Microsoft authorize URL with the configured tenant
        Assert.StartsWith(
            "https://login.microsoftonline.com/common/oauth2/v2.0/authorize",
            url);
    }

    [Fact]
    public void Microsoft_BuildAuthorizationUrl_WithCustomTenant_UsesConfiguredTenant()
    {
        // Arrange
        const string TenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";
        IConfiguration config = MakeMicrosoftConfig(tenant: TenantId);
        IOpenIdConfigurationProvider openIdProvider = MakeOpenIdConfigProviderWithKey(
            $"https://login.microsoftonline.com/{TenantId}/v2.0", _testSecurityKey, "microsoft");
        MicrosoftIdentityProviderAdapter adapter = new(config, MakeHttpClient(new TestHttpMessageHandler()), openIdProvider);
        string challenge = ComputeCodeChallenge(MakeCodeVerifier());

        // Act
        string url = adapter.BuildAuthorizationUrl(
            "microsoft", challenge, "state-tenant", "https://app.example.com/auth/callback");

        // Assert — tenant UUID must appear in the URL
        Assert.Contains(TenantId, url);
    }

    [Fact]
    public void Microsoft_BuildAuthorizationUrl_HasPkceCodeChallenge()
    {
        // Arrange
        IConfiguration config = MakeMicrosoftConfig();
        IOpenIdConfigurationProvider openIdProvider = MakeOpenIdConfigProviderWithKey(
            "https://login.microsoftonline.com/common/v2.0", _testSecurityKey, "microsoft");
        MicrosoftIdentityProviderAdapter adapter = new(config, MakeHttpClient(new TestHttpMessageHandler()), openIdProvider);
        string verifier = MakeCodeVerifier();
        string challenge = ComputeCodeChallenge(verifier);

        // Act
        string url = adapter.BuildAuthorizationUrl(
            "microsoft", challenge, "state-pkce", "https://app.example.com/auth/callback");

        // Assert
        Uri uri = new(url);
        string query = HttpUtility.UrlDecode(uri.Query);
        Assert.Contains($"code_challenge={challenge}", query);
        Assert.Contains("code_challenge_method=S256", query);
    }

    [Fact]
    public void Microsoft_BuildAuthorizationUrl_HasClientId()
    {
        // Arrange
        IConfiguration config = MakeMicrosoftConfig(clientId: "ms-cid-test");
        IOpenIdConfigurationProvider openIdProvider = MakeOpenIdConfigProviderWithKey(
            "https://login.microsoftonline.com/common/v2.0", _testSecurityKey, "microsoft");
        MicrosoftIdentityProviderAdapter adapter = new(config, MakeHttpClient(new TestHttpMessageHandler()), openIdProvider);
        string challenge = ComputeCodeChallenge(MakeCodeVerifier());

        // Act
        string url = adapter.BuildAuthorizationUrl(
            "microsoft", challenge, "state", "https://app.example.com/auth/callback");

        // Assert
        Uri uri = new(url);
        string query = HttpUtility.UrlDecode(uri.Query);
        Assert.Contains("client_id=ms-cid-test", query);
    }

    // =========================================================================
    // MICROSOFT — ExchangeCodeAsync
    // =========================================================================

    [Fact]
    public async Task Microsoft_ExchangeCode_OnSuccess_ReturnsIdToken()
    {
        // Arrange
        const string ClientId = "ms-client-id";
        string rawIdToken = MintIdToken(
            "https://login.microsoftonline.com/common/v2.0",
            ClientId,
            "ms-sub-001", "dave@example.com", true, "Dave");

        TestHttpMessageHandler handler = new(new Dictionary<string, string>
        {
            ["https://login.microsoftonline.com/common/oauth2/v2.0/token"] =
                $$"""{"id_token":"{{rawIdToken}}","access_token":"ms-at","token_type":"Bearer","expires_in":3600}""",
        });
        IConfiguration config = MakeMicrosoftConfig(clientId: ClientId, tenant: "common");
        IOpenIdConfigurationProvider openIdProvider = MakeOpenIdConfigProviderWithKey(
            "https://login.microsoftonline.com/common/v2.0", _testSecurityKey, "microsoft");
        MicrosoftIdentityProviderAdapter adapter = new(config, MakeHttpClient(handler), openIdProvider);

        // Act
        string result = await adapter.ExchangeCodeAsync(
            "microsoft", "ms-code", MakeCodeVerifier(), "https://app.example.com/auth/callback");

        // Assert
        Assert.Equal(rawIdToken, result);
    }

    [Fact]
    public async Task Microsoft_ExchangeCode_OnHttpError_ThrowsException()
    {
        // Arrange
        TestHttpMessageHandler handler = new(statusCode: System.Net.HttpStatusCode.BadRequest);
        IConfiguration config = MakeMicrosoftConfig();
        IOpenIdConfigurationProvider openIdProvider = MakeOpenIdConfigProviderWithKey(
            "https://login.microsoftonline.com/common/v2.0", _testSecurityKey, "microsoft");
        MicrosoftIdentityProviderAdapter adapter = new(config, MakeHttpClient(handler), openIdProvider);

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(() =>
            adapter.ExchangeCodeAsync("microsoft", "bad-code", MakeCodeVerifier(), "https://app.example.com/auth/callback"));
    }

    // =========================================================================
    // MICROSOFT — ValidateIdTokenAsync (valid, expired, tampered)
    // =========================================================================

    [Fact]
    public async Task Microsoft_ValidateIdToken_ValidToken_ReturnsVerifiedIdentity()
    {
        // Arrange
        const string ClientId = "ms-client-id";
        const string Tenant = "common";
        const string Issuer = "https://login.microsoftonline.com/common/v2.0";
        const string Subject = "ms-sub-001";
        const string Email = "eve@example.com";
        const string Name = "Eve";

        string rawToken = MintIdToken(Issuer, ClientId, Subject, Email, true, Name);

        IConfiguration config = MakeMicrosoftConfig(clientId: ClientId, tenant: Tenant);
        IOpenIdConfigurationProvider openIdProvider = MakeOpenIdConfigProviderWithKey(
            Issuer, _testSecurityKey, "microsoft");
        MicrosoftIdentityProviderAdapter adapter = new(config, MakeHttpClient(new TestHttpMessageHandler()), openIdProvider);

        // Act
        VerifiedIdentity identity = await adapter.ValidateIdTokenAsync("microsoft", rawToken);

        // Assert
        Assert.Equal(Subject, identity.Subject);
        Assert.Equal(Email, identity.Email);
        Assert.True(identity.EmailVerified);
        Assert.Equal(Name, identity.Name);
    }

    [Fact]
    public async Task Microsoft_ValidateIdToken_ExpiredToken_ThrowsSecurityTokenExpiredException()
    {
        // Arrange
        const string ClientId = "ms-client-id";
        const string Issuer = "https://login.microsoftonline.com/common/v2.0";
        string expiredToken = MintIdToken(
            Issuer, ClientId, "sub-exp", "exp@example.com", true, "Expired",
            expiry: DateTimeOffset.UtcNow.AddHours(-1));

        IConfiguration config = MakeMicrosoftConfig(clientId: ClientId);
        IOpenIdConfigurationProvider openIdProvider = MakeOpenIdConfigProviderWithKey(
            Issuer, _testSecurityKey, "microsoft");
        MicrosoftIdentityProviderAdapter adapter = new(config, MakeHttpClient(new TestHttpMessageHandler()), openIdProvider);

        // Act & Assert
        await Assert.ThrowsAnyAsync<SecurityTokenExpiredException>(() =>
            adapter.ValidateIdTokenAsync("microsoft", expiredToken));
    }

    [Fact]
    public async Task Microsoft_ValidateIdToken_TamperedPayload_ThrowsSecurityTokenException()
    {
        // Arrange
        const string ClientId = "ms-client-id";
        const string Issuer = "https://login.microsoftonline.com/common/v2.0";
        string validToken = MintIdToken(
            Issuer, ClientId, "sub-tamper", "tamper@example.com", true, "Tamper");

        string[] parts = validToken.Split('.');
        string tamperedToken = $"{parts[0]}.{parts[1] + "XXX"}.{parts[2]}";

        IConfiguration config = MakeMicrosoftConfig(clientId: ClientId);
        IOpenIdConfigurationProvider openIdProvider = MakeOpenIdConfigProviderWithKey(
            Issuer, _testSecurityKey, "microsoft");
        MicrosoftIdentityProviderAdapter adapter = new(config, MakeHttpClient(new TestHttpMessageHandler()), openIdProvider);

        // Act & Assert
        await Assert.ThrowsAnyAsync<SecurityTokenException>(() =>
            adapter.ValidateIdTokenAsync("microsoft", tamperedToken));
    }

    [Fact]
    public async Task Microsoft_ValidateIdToken_TokenSignedWithWrongKey_ThrowsSecurityTokenException()
    {
        // Arrange
        const string ClientId = "ms-client-id";
        const string Issuer = "https://login.microsoftonline.com/common/v2.0";

        using RSA attackerRsa = RSA.Create(2048);
        RsaSecurityKey attackerKey = new(attackerRsa) { KeyId = "attacker" };
        SigningCredentials attackerCreds = new(attackerKey, SecurityAlgorithms.RsaSha256);

        JwtSecurityToken fakeToken = new(
            issuer: Issuer,
            audience: ClientId,
            claims: new[] { new Claim("sub", "fake-sub") },
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: attackerCreds);
        string fakeRaw = new JwtSecurityTokenHandler().WriteToken(fakeToken);

        IConfiguration config = MakeMicrosoftConfig(clientId: ClientId);
        // JWKS returns legitimate key, not attacker's
        IOpenIdConfigurationProvider openIdProvider = MakeOpenIdConfigProviderWithKey(
            Issuer, _testSecurityKey, "microsoft");
        MicrosoftIdentityProviderAdapter adapter = new(config, MakeHttpClient(new TestHttpMessageHandler()), openIdProvider);

        // Act & Assert
        await Assert.ThrowsAnyAsync<SecurityTokenException>(() =>
            adapter.ValidateIdTokenAsync("microsoft", fakeRaw));
    }

    // =========================================================================
    // VerifiedIdentity — record contract
    // =========================================================================

    [Fact]
    public void VerifiedIdentity_IsImmutableRecord()
    {
        // Arrange & Act
        VerifiedIdentity identity = new("sub-001", "test@example.com", true, "Test User");

        // Assert — property values match constructor
        Assert.Equal("sub-001", identity.Subject);
        Assert.Equal("test@example.com", identity.Email);
        Assert.True(identity.EmailVerified);
        Assert.Equal("Test User", identity.Name);
    }

    [Fact]
    public void VerifiedIdentity_Equality_BasedOnValues()
    {
        // Records must support value equality
        VerifiedIdentity a = new("sub", "a@b.com", true, "A");
        VerifiedIdentity b = new("sub", "a@b.com", true, "A");
        Assert.Equal(a, b);
    }

    [Fact]
    public void VerifiedIdentity_UnverifiedEmail_FlagFalse()
    {
        // Unverified email must be expressible
        VerifiedIdentity unverified = new("sub-unv", "unv@example.com", false, "Unverified");
        Assert.False(unverified.EmailVerified);
    }

    // =========================================================================
    // INamedIdentityProviderPort — contract check
    // =========================================================================

    [Fact]
    public void GoogleAdapter_ImplementsINamedIdentityProviderPort()
    {
        // The registry needs to read provider names from adapters.
        // Google adapter must implement INamedIdentityProviderPort.
        Assert.True(
            typeof(INamedIdentityProviderPort).IsAssignableFrom(typeof(GoogleIdentityProviderAdapter)),
            "GoogleIdentityProviderAdapter must implement INamedIdentityProviderPort");
    }

    [Fact]
    public void MicrosoftAdapter_ImplementsINamedIdentityProviderPort()
    {
        Assert.True(
            typeof(INamedIdentityProviderPort).IsAssignableFrom(typeof(MicrosoftIdentityProviderAdapter)),
            "MicrosoftIdentityProviderAdapter must implement INamedIdentityProviderPort");
    }

    [Fact]
    public void GoogleAdapter_ProviderName_IsGoogle()
    {
        // The canonical name must be "google" (lowercase)
        Assert.Equal("google", GoogleIdentityProviderAdapter.ProviderName);
    }

    [Fact]
    public void MicrosoftAdapter_ProviderName_IsMicrosoft()
    {
        Assert.Equal("microsoft", MicrosoftIdentityProviderAdapter.ProviderName);
    }
}

// =============================================================================
// Test infrastructure — HTTP message handlers
// =============================================================================

/// <summary>
/// Minimal stub handler that returns canned responses for specific URLs,
/// or a fixed status code for all requests.
/// </summary>
internal sealed class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, string> _urlResponseMap;
    private readonly System.Net.HttpStatusCode _defaultStatus;
    private readonly string _defaultBody;

    public TestHttpMessageHandler(
        Dictionary<string, string>? urlResponseMap = null,
        System.Net.HttpStatusCode statusCode = System.Net.HttpStatusCode.OK,
        string body = "")
    {
        _urlResponseMap = urlResponseMap ?? new Dictionary<string, string>();
        _defaultStatus = statusCode;
        _defaultBody = body;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string url = request.RequestUri?.ToString() ?? string.Empty;

        if (_urlResponseMap.TryGetValue(url, out string? responseBody))
        {
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            });
        }

        return Task.FromResult(new HttpResponseMessage(_defaultStatus)
        {
            Content = new StringContent(_defaultBody, Encoding.UTF8, "application/json"),
        });
    }
}

/// <summary>
/// Handler that captures the last request URL for assertion.
/// </summary>
internal sealed class CapturingHttpMessageHandler : HttpMessageHandler
{
    private readonly string _responseBody;

    public string? LastRequestUrl { get; private set; }

    public CapturingHttpMessageHandler(string responseBody)
    {
        _responseBody = responseBody;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequestUrl = request.RequestUri?.ToString();
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(_responseBody, Encoding.UTF8, "application/json"),
        });
    }
}
