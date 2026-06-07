using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ClaudeForge.Core.Identity.Ports;
using ClaudeForge.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;

namespace ClaudeForge.Tests.Unit.Identity;

/// <summary>
/// Additional branch-coverage tests for Google and Microsoft OIDC adapters:
///   - HTTP-error branch of ExchangeCodeAsync (non-2xx → InvalidOperationException)
///   - Missing 'sub' claim branch of ValidateIdTokenAsync
///   - Missing 'email' claim branch of ValidateIdTokenAsync
///   - Nonce claim is included when non-empty in BuildAuthorizationUrl
/// </summary>
public sealed class OidcAdapterCoverageTests : IDisposable
{
    // =========================================================================
    // Shared test RSA key
    // =========================================================================

    private readonly RSA _testRsa;
    private readonly RsaSecurityKey _testKey;
    private readonly SigningCredentials _testCreds;

    public OidcAdapterCoverageTests()
    {
        _testRsa = RSA.Create(2048);
        _testKey = new RsaSecurityKey(_testRsa) { KeyId = "cov-kid" };
        _testCreds = new SigningCredentials(_testKey, SecurityAlgorithms.RsaSha256);
    }

    public void Dispose() => _testRsa.Dispose();

    // =========================================================================
    // Helpers
    // =========================================================================

    private IOpenIdConfigurationProvider MockProvider(string issuer, string providerName)
    {
        IOpenIdConfigurationProvider mock = Substitute.For<IOpenIdConfigurationProvider>();
        mock.GetSigningKeysAsync(providerName, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<SecurityKey>>(new[] { (SecurityKey)_testKey }));
        mock.GetIssuer(providerName).Returns(issuer);
        return mock;
    }

    private static IConfiguration GoogleConfig(string clientId = "gcid") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OIDC__GOOGLE__CLIENTID"] = clientId,
                ["OIDC__GOOGLE__CLIENTSECRET"] = "secret",
            })
            .Build();

    private static IConfiguration MicrosoftConfig(
        string clientId = "msid",
        string tenant = "common") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OIDC__MICROSOFT__CLIENTID"] = clientId,
                ["OIDC__MICROSOFT__CLIENTSECRET"] = "secret",
                ["OIDC__MICROSOFT__TENANT"] = tenant,
            })
            .Build();

    private static HttpClient MakeClient(HttpMessageHandler handler) => new(handler);

    /// <summary>
    /// Mints a token that is valid for JWT parsing but may be missing claims.
    /// </summary>
    private string MintTokenWithClaims(
        string issuer,
        string audience,
        IEnumerable<Claim> claims)
    {
        JwtSecurityToken token = new(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: _testCreds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // =========================================================================
    // Google — ExchangeCodeAsync HTTP error branch
    // =========================================================================

    [Theory]
    [InlineData(System.Net.HttpStatusCode.Unauthorized)]
    [InlineData(System.Net.HttpStatusCode.Forbidden)]
    [InlineData(System.Net.HttpStatusCode.InternalServerError)]
    [InlineData(System.Net.HttpStatusCode.ServiceUnavailable)]
    public async Task Google_ExchangeCode_NonSuccessStatus_ThrowsInvalidOperationException(
        System.Net.HttpStatusCode statusCode)
    {
        TestHttpMessageHandler handler = new(statusCode: statusCode, body: "upstream error");
        GoogleIdentityProviderAdapter adapter = new(
            GoogleConfig(),
            MakeClient(handler),
            MockProvider("https://accounts.google.com", "google"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => adapter.ExchangeCodeAsync("google", "code", "verifier", "https://app/callback"));
    }

    // =========================================================================
    // Google — ValidateIdTokenAsync missing-claim branches
    // =========================================================================

    [Fact]
    public async Task Google_ValidateIdToken_MissingSub_ThrowsInvalidOperationException()
    {
        const string ClientId = "gcid";
        // Token with email but no 'sub' claim
        string token = MintTokenWithClaims(
            "https://accounts.google.com",
            ClientId,
            new[] { new Claim("email", "test@example.com") });

        GoogleIdentityProviderAdapter adapter = new(
            GoogleConfig(clientId: ClientId),
            MakeClient(new TestHttpMessageHandler()),
            MockProvider("https://accounts.google.com", "google"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => adapter.ValidateIdTokenAsync("google", token));
    }

    [Fact]
    public async Task Google_ValidateIdToken_MissingEmail_ThrowsInvalidOperationException()
    {
        const string ClientId = "gcid";
        // Token with 'sub' but no 'email' claim
        string token = MintTokenWithClaims(
            "https://accounts.google.com",
            ClientId,
            new[] { new Claim("sub", "sub-noemail") });

        GoogleIdentityProviderAdapter adapter = new(
            GoogleConfig(clientId: ClientId),
            MakeClient(new TestHttpMessageHandler()),
            MockProvider("https://accounts.google.com", "google"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => adapter.ValidateIdTokenAsync("google", token));
    }

    [Fact]
    public async Task Google_ValidateIdToken_NoEmailVerifiedClaim_TreatedAsFalse()
    {
        const string ClientId = "gcid";
        // Token without email_verified → should default to false, not throw
        string token = MintTokenWithClaims(
            "https://accounts.google.com",
            ClientId,
            new[]
            {
                new Claim("sub", "sub-noverify"),
                new Claim("email", "noverify@example.com"),
            });

        GoogleIdentityProviderAdapter adapter = new(
            GoogleConfig(clientId: ClientId),
            MakeClient(new TestHttpMessageHandler()),
            MockProvider("https://accounts.google.com", "google"));

        VerifiedIdentity identity = await adapter.ValidateIdTokenAsync("google", token);

        Assert.False(identity.EmailVerified);
    }

    [Fact]
    public async Task Google_ValidateIdToken_EmailVerifiedFalse_ReturnsEmailVerifiedFalse()
    {
        const string ClientId = "gcid";
        string token = MintTokenWithClaims(
            "https://accounts.google.com",
            ClientId,
            new[]
            {
                new Claim("sub", "sub-unverified"),
                new Claim("email", "unverified@example.com"),
                new Claim("email_verified", "false"),
            });

        GoogleIdentityProviderAdapter adapter = new(
            GoogleConfig(clientId: ClientId),
            MakeClient(new TestHttpMessageHandler()),
            MockProvider("https://accounts.google.com", "google"));

        VerifiedIdentity identity = await adapter.ValidateIdTokenAsync("google", token);

        Assert.False(identity.EmailVerified);
        Assert.Equal("sub-unverified", identity.Subject);
    }

    [Fact]
    public async Task Google_ValidateIdToken_WithNonceClaim_ExtractsNonce()
    {
        const string ClientId = "gcid";
        string token = MintTokenWithClaims(
            "https://accounts.google.com",
            ClientId,
            new[]
            {
                new Claim("sub", "sub-nonce"),
                new Claim("email", "nonce@example.com"),
                new Claim("nonce", "my-nonce-value"),
            });

        GoogleIdentityProviderAdapter adapter = new(
            GoogleConfig(clientId: ClientId),
            MakeClient(new TestHttpMessageHandler()),
            MockProvider("https://accounts.google.com", "google"));

        VerifiedIdentity identity = await adapter.ValidateIdTokenAsync("google", token);

        Assert.Equal("my-nonce-value", identity.Nonce);
    }

    // =========================================================================
    // Google — BuildAuthorizationUrl with nonce
    // =========================================================================

    [Fact]
    public void Google_BuildAuthorizationUrl_WithNonce_IncludesNonceInUrl()
    {
        GoogleIdentityProviderAdapter adapter = new(
            GoogleConfig(),
            MakeClient(new TestHttpMessageHandler()),
            MockProvider("https://accounts.google.com", "google"));

        string url = adapter.BuildAuthorizationUrl(
            "google", "challenge", "state", "https://app/callback", nonce: "test-nonce");

        Assert.Contains("nonce=test-nonce", System.Web.HttpUtility.UrlDecode(url));
    }

    [Fact]
    public void Google_BuildAuthorizationUrl_EmptyNonce_DoesNotIncludeNonceParam()
    {
        GoogleIdentityProviderAdapter adapter = new(
            GoogleConfig(),
            MakeClient(new TestHttpMessageHandler()),
            MockProvider("https://accounts.google.com", "google"));

        string url = adapter.BuildAuthorizationUrl(
            "google", "challenge", "state", "https://app/callback", nonce: "");

        // Empty nonce must not add the parameter at all
        Assert.DoesNotContain("nonce=", url);
    }

    // =========================================================================
    // Microsoft — ExchangeCodeAsync HTTP error branch
    // =========================================================================

    [Theory]
    [InlineData(System.Net.HttpStatusCode.BadRequest)]
    [InlineData(System.Net.HttpStatusCode.Unauthorized)]
    [InlineData(System.Net.HttpStatusCode.InternalServerError)]
    public async Task Microsoft_ExchangeCode_NonSuccessStatus_ThrowsInvalidOperationException(
        System.Net.HttpStatusCode statusCode)
    {
        TestHttpMessageHandler handler = new(statusCode: statusCode, body: "upstream error");
        MicrosoftIdentityProviderAdapter adapter = new(
            MicrosoftConfig(),
            MakeClient(handler),
            MockProvider("https://login.microsoftonline.com/common/v2.0", "microsoft"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => adapter.ExchangeCodeAsync("microsoft", "code", "verifier", "https://app/callback"));
    }

    // =========================================================================
    // Microsoft — ValidateIdTokenAsync missing-claim branches
    // =========================================================================

    [Fact]
    public async Task Microsoft_ValidateIdToken_MissingSub_ThrowsInvalidOperationException()
    {
        const string ClientId = "msid";
        const string Issuer = "https://login.microsoftonline.com/common/v2.0";
        string token = MintTokenWithClaims(
            Issuer,
            ClientId,
            new[] { new Claim("email", "ms@example.com") });

        MicrosoftIdentityProviderAdapter adapter = new(
            MicrosoftConfig(clientId: ClientId),
            MakeClient(new TestHttpMessageHandler()),
            MockProvider(Issuer, "microsoft"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => adapter.ValidateIdTokenAsync("microsoft", token));
    }

    [Fact]
    public async Task Microsoft_ValidateIdToken_MissingEmail_ThrowsInvalidOperationException()
    {
        const string ClientId = "msid";
        const string Issuer = "https://login.microsoftonline.com/common/v2.0";
        string token = MintTokenWithClaims(
            Issuer,
            ClientId,
            new[] { new Claim("sub", "ms-sub-001") });

        MicrosoftIdentityProviderAdapter adapter = new(
            MicrosoftConfig(clientId: ClientId),
            MakeClient(new TestHttpMessageHandler()),
            MockProvider(Issuer, "microsoft"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => adapter.ValidateIdTokenAsync("microsoft", token));
    }

    [Fact]
    public async Task Microsoft_ValidateIdToken_WithNonceClaim_ExtractsNonce()
    {
        const string ClientId = "msid";
        const string Issuer = "https://login.microsoftonline.com/common/v2.0";
        string token = MintTokenWithClaims(
            Issuer,
            ClientId,
            new[]
            {
                new Claim("sub", "ms-sub-nonce"),
                new Claim("email", "ms-nonce@example.com"),
                new Claim("nonce", "ms-nonce-value"),
            });

        MicrosoftIdentityProviderAdapter adapter = new(
            MicrosoftConfig(clientId: ClientId),
            MakeClient(new TestHttpMessageHandler()),
            MockProvider(Issuer, "microsoft"));

        VerifiedIdentity identity = await adapter.ValidateIdTokenAsync("microsoft", token);

        Assert.Equal("ms-nonce-value", identity.Nonce);
    }

    // =========================================================================
    // Microsoft — BuildAuthorizationUrl with nonce
    // =========================================================================

    [Fact]
    public void Microsoft_BuildAuthorizationUrl_WithNonce_IncludesNonceInUrl()
    {
        MicrosoftIdentityProviderAdapter adapter = new(
            MicrosoftConfig(),
            MakeClient(new TestHttpMessageHandler()),
            MockProvider("https://login.microsoftonline.com/common/v2.0", "microsoft"));

        string url = adapter.BuildAuthorizationUrl(
            "microsoft", "challenge", "state", "https://app/callback", nonce: "ms-nonce-123");

        Assert.Contains("nonce=ms-nonce-123", System.Web.HttpUtility.UrlDecode(url));
    }
}
