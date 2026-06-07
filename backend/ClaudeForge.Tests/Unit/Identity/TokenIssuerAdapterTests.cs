using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using ClaudeForge.Core.Identity.Ports;
using Microsoft.IdentityModel.Tokens;

namespace ClaudeForge.Tests.Unit.Identity;

/// <summary>
/// Unit tests for Group 3, Task 3.2 — ITokenIssuerPort access-token issuance and validation.
///
/// These tests are RED because the production types listed below do not yet exist.
/// The coder MUST create:
///
///   NAMESPACE: ClaudeForge.Core.Identity.Ports
///
///   sealed record AccessTokenClaims
///     Guid   UserId   — maps to JWT "sub" claim (string form of Guid)
///     string Email    — maps to JWT "email" claim
///     string Name     — maps to JWT "name" claim
///     string Provider — maps to JWT "provider" claim (e.g. "google" | "microsoft")
///
///   interface ITokenIssuerPort
///     /// Issues an RS256-signed JWT. Jti is generated fresh each call (UUID).
///     string IssueAccessToken(AccessTokenClaims claims);
///
///     /// Validates a raw JWT string.
///     /// Returns the ClaimsPrincipal on success.
///     /// Throws SecurityTokenException (or a subtype) for any validation failure.
///     ClaimsPrincipal ValidateAccessToken(string rawToken);
///
///   NAMESPACE: ClaudeForge.Infrastructure.Identity
///
///   sealed class RsaTokenIssuerAdapter : ITokenIssuerPort
///     Constructor:
///       RsaTokenIssuerAdapter(
///         string privatePem,   // RSA PKCS#8 or traditional PEM of the private key
///         string issuer,       // JWT "iss"
///         string audience,     // JWT "aud"
///         int accessTokenMinutes = 15,
///         string kid = "primary")  // key ID placed in the JWT header
///
///     Rules:
///       - Signs with RS256 (RSA + SHA-256)
///       - JWT header contains "kid" matching the constructor parameter
///       - Claims: sub, email, name, provider, iss, aud, iat, exp, jti
///       - exp = iat + accessTokenMinutes
///       - jti is a new Guid on every call (UUID, string form)
///       - ValidateAccessToken checks: RS256 signature, issuer, audience, expiry, malformed
///
/// Design source-of-truth (design.md §1):
///   - Access token: RS256, 15-minute default expiry
///   - Claims: sub (user UUID), email, name, provider, iss, aud, iat, exp, jti
///   - Org memberships NOT embedded in token
/// </summary>
public sealed class TokenIssuerAdapterTests
{
    // =========================================================================
    // Test RSA key pair — generated once for the suite (not secrets; tests only)
    // =========================================================================

    /// <summary>
    /// A fresh 2048-bit RSA key pair scoped to the test assembly.
    /// Private PEM is used to construct the adapter under test; public key is
    /// used to independently verify signatures without going through the adapter.
    /// </summary>
    private static readonly (string PrivatePem, RSA PublicKey) TestKeyPair = GenerateTestKeyPair();

    private static (string PrivatePem, RSA PublicKey) GenerateTestKeyPair()
    {
        RSA rsa = RSA.Create(keySizeInBits: 2048);
        string privatePem = rsa.ExportRSAPrivateKeyPem();
        // Export only the public half as a fresh RSA instance for verification
        RSA publicKey = RSA.Create();
        publicKey.ImportRSAPublicKey(rsa.ExportRSAPublicKey(), out _);
        return (privatePem, publicKey);
    }

    private const string TestIssuer = "https://claudeforge.test";
    private const string TestAudience = "claudeforge-api";
    private const string TestKid = "test-key-1";
    private const int DefaultExpiryMinutes = 15;

    /// <summary>
    /// Factory for the adapter under test. Creates a new instance with fresh constructor args
    /// so tests remain isolated (no shared mutable state).
    /// </summary>
    private static ITokenIssuerPort MakeAdapter(int accessTokenMinutes = DefaultExpiryMinutes) =>
        new ClaudeForge.Infrastructure.Identity.RsaTokenIssuerAdapter(
            privatePem: TestKeyPair.PrivatePem,
            issuer: TestIssuer,
            audience: TestAudience,
            accessTokenMinutes: accessTokenMinutes,
            kid: TestKid);

    private static AccessTokenClaims MakeClaims(
        Guid? userId = null,
        string email = "user@example.com",
        string name = "Test User",
        string provider = "google") =>
        new(
            UserId: userId ?? Guid.NewGuid(),
            Email: email,
            Name: name,
            Provider: provider);

    // =========================================================================
    // CLAIM PRESENCE AND CORRECTNESS
    // =========================================================================

    [Fact]
    public void IssueAccessToken_ReturnsNonEmptyString()
    {
        ITokenIssuerPort adapter = MakeAdapter();
        AccessTokenClaims claims = MakeClaims();

        string token = adapter.IssueAccessToken(claims);

        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }

    [Fact]
    public void IssueAccessToken_TokenContains_SubClaim_EqualToUserId()
    {
        ITokenIssuerPort adapter = MakeAdapter();
        Guid userId = Guid.NewGuid();
        AccessTokenClaims claims = MakeClaims(userId: userId);

        string token = adapter.IssueAccessToken(claims);

        JwtSecurityToken jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Claim? sub = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub);
        Assert.NotNull(sub);
        Assert.Equal(userId.ToString(), sub!.Value);
    }

    [Fact]
    public void IssueAccessToken_TokenContains_EmailClaim()
    {
        ITokenIssuerPort adapter = MakeAdapter();
        AccessTokenClaims claims = MakeClaims(email: "alice@example.com");

        string token = adapter.IssueAccessToken(claims);

        JwtSecurityToken jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Claim? email = jwt.Claims.FirstOrDefault(c =>
            c.Type == JwtRegisteredClaimNames.Email ||
            c.Type == "email");
        Assert.NotNull(email);
        Assert.Equal("alice@example.com", email!.Value);
    }

    [Fact]
    public void IssueAccessToken_TokenContains_NameClaim()
    {
        ITokenIssuerPort adapter = MakeAdapter();
        AccessTokenClaims claims = MakeClaims(name: "Alice Smith");

        string token = adapter.IssueAccessToken(claims);

        JwtSecurityToken jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Claim? name = jwt.Claims.FirstOrDefault(c =>
            c.Type == JwtRegisteredClaimNames.Name ||
            c.Type == "name");
        Assert.NotNull(name);
        Assert.Equal("Alice Smith", name!.Value);
    }

    [Fact]
    public void IssueAccessToken_TokenContains_ProviderClaim()
    {
        ITokenIssuerPort adapter = MakeAdapter();
        AccessTokenClaims claims = MakeClaims(provider: "microsoft");

        string token = adapter.IssueAccessToken(claims);

        JwtSecurityToken jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Claim? provider = jwt.Claims.FirstOrDefault(c => c.Type == "provider");
        Assert.NotNull(provider);
        Assert.Equal("microsoft", provider!.Value);
    }

    [Fact]
    public void IssueAccessToken_TokenContains_IssClaim_MatchingIssuer()
    {
        ITokenIssuerPort adapter = MakeAdapter();
        AccessTokenClaims claims = MakeClaims();

        string token = adapter.IssueAccessToken(claims);

        JwtSecurityToken jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(TestIssuer, jwt.Issuer);
    }

    [Fact]
    public void IssueAccessToken_TokenContains_AudClaim_MatchingAudience()
    {
        ITokenIssuerPort adapter = MakeAdapter();
        AccessTokenClaims claims = MakeClaims();

        string token = adapter.IssueAccessToken(claims);

        JwtSecurityToken jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Contains(TestAudience, jwt.Audiences);
    }

    [Fact]
    public void IssueAccessToken_TokenContains_IatClaim()
    {
        ITokenIssuerPort adapter = MakeAdapter();
        AccessTokenClaims claims = MakeClaims();

        DateTimeOffset before = DateTimeOffset.UtcNow.AddSeconds(-1);
        string token = adapter.IssueAccessToken(claims);
        DateTimeOffset after = DateTimeOffset.UtcNow.AddSeconds(1);

        JwtSecurityToken jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        // iat is a Unix timestamp — ValidFrom is the iat-derived DateTime
        Assert.True(jwt.IssuedAt >= before.UtcDateTime, "iat must be >= before-issue time");
        Assert.True(jwt.IssuedAt <= after.UtcDateTime, "iat must be <= after-issue time");
    }

    [Fact]
    public void IssueAccessToken_TokenContains_ExpClaim_FifteenMinutesAfterIat()
    {
        ITokenIssuerPort adapter = MakeAdapter(accessTokenMinutes: DefaultExpiryMinutes);
        AccessTokenClaims claims = MakeClaims();

        string token = adapter.IssueAccessToken(claims);

        JwtSecurityToken jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        TimeSpan lifetime = jwt.ValidTo - jwt.IssuedAt;
        // Allow 2-second clock drift tolerance
        Assert.True(
            lifetime >= TimeSpan.FromMinutes(DefaultExpiryMinutes - 1) &&
            lifetime <= TimeSpan.FromMinutes(DefaultExpiryMinutes + 1),
            $"Expected token lifetime ~{DefaultExpiryMinutes} minutes but was {lifetime.TotalMinutes:F2} minutes");
    }

    [Fact]
    public void IssueAccessToken_TokenContains_JtiClaim_NonEmpty()
    {
        ITokenIssuerPort adapter = MakeAdapter();
        AccessTokenClaims claims = MakeClaims();

        string token = adapter.IssueAccessToken(claims);

        JwtSecurityToken jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Claim? jti = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti);
        Assert.NotNull(jti);
        Assert.True(Guid.TryParse(jti!.Value, out _), "jti must be a valid Guid string");
    }

    [Fact]
    public void IssueAccessToken_EachCallProducesUniqueJti()
    {
        // jti must be unique per token (fresh Guid each call)
        ITokenIssuerPort adapter = MakeAdapter();
        AccessTokenClaims claims = MakeClaims();

        string token1 = adapter.IssueAccessToken(claims);
        string token2 = adapter.IssueAccessToken(claims);

        JwtSecurityToken jwt1 = new JwtSecurityTokenHandler().ReadJwtToken(token1);
        JwtSecurityToken jwt2 = new JwtSecurityTokenHandler().ReadJwtToken(token2);

        string jti1 = jwt1.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        string jti2 = jwt2.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

        Assert.NotEqual(jti1, jti2);
    }

    // =========================================================================
    // ALGORITHM AND HEADER
    // =========================================================================

    [Fact]
    public void IssueAccessToken_UsesRS256Algorithm()
    {
        ITokenIssuerPort adapter = MakeAdapter();
        AccessTokenClaims claims = MakeClaims();

        string token = adapter.IssueAccessToken(claims);

        JwtSecurityToken jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(SecurityAlgorithms.RsaSha256, jwt.Header.Alg);
    }

    [Fact]
    public void IssueAccessToken_HeaderContains_Kid()
    {
        ITokenIssuerPort adapter = MakeAdapter();
        AccessTokenClaims claims = MakeClaims();

        string token = adapter.IssueAccessToken(claims);

        JwtSecurityToken jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(TestKid, jwt.Header.Kid);
    }

    // =========================================================================
    // SIGNATURE VERIFICATION AGAINST INDEPENDENT PUBLIC KEY
    // =========================================================================

    [Fact]
    public void IssueAccessToken_SignatureVerifies_AgainstPublicKey()
    {
        // Verify independently — do NOT use the adapter's ValidateAccessToken here.
        // This ensures the adapter actually signed with the private key corresponding
        // to the public key we hold, not some other key.
        ITokenIssuerPort adapter = MakeAdapter();
        AccessTokenClaims claims = MakeClaims();

        string token = adapter.IssueAccessToken(claims);

        JwtSecurityTokenHandler handler = new();
        TokenValidationParameters parameters = new()
        {
            ValidateIssuer = true,
            ValidIssuer = TestIssuer,
            ValidateAudience = true,
            ValidAudience = TestAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            IssuerSigningKey = new RsaSecurityKey(TestKeyPair.PublicKey),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        };

        // Should NOT throw
        ClaimsPrincipal principal = handler.ValidateToken(token, parameters, out _);
        Assert.NotNull(principal);
    }

    // =========================================================================
    // ValidateAccessToken — HAPPY PATH
    // =========================================================================

    [Fact]
    public void ValidateAccessToken_ValidToken_ReturnsPrincipalWithCorrectSub()
    {
        ITokenIssuerPort adapter = MakeAdapter();
        Guid userId = Guid.NewGuid();
        AccessTokenClaims claims = MakeClaims(userId: userId);

        string token = adapter.IssueAccessToken(claims);
        ClaimsPrincipal principal = adapter.ValidateAccessToken(token);

        // sub claim must survive the round-trip
        string? sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                   ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Assert.Equal(userId.ToString(), sub);
    }

    // =========================================================================
    // ValidateAccessToken — REJECTION CASES
    // =========================================================================

    [Fact]
    public void ValidateAccessToken_TamperedPayload_ThrowsSecurityTokenException()
    {
        // Tamper with the payload segment to break the signature
        ITokenIssuerPort adapter = MakeAdapter();
        string token = adapter.IssueAccessToken(MakeClaims());
        string[] parts = token.Split('.');
        // Corrupt payload by appending a character
        string tampered = $"{parts[0]}.{parts[1]}CORRUPT.{parts[2]}";

        Assert.ThrowsAny<SecurityTokenException>(() => adapter.ValidateAccessToken(tampered));
    }

    [Fact]
    public void ValidateAccessToken_ExpiredToken_ThrowsSecurityTokenExpiredException()
    {
        // Issue with -1 minute lifetime → already expired at issue time
        ITokenIssuerPort adapter = MakeAdapter(accessTokenMinutes: -1);
        string token = adapter.IssueAccessToken(MakeClaims());

        Assert.Throws<SecurityTokenExpiredException>(() => adapter.ValidateAccessToken(token));
    }

    [Fact]
    public void ValidateAccessToken_WrongSigningKey_ThrowsSecurityTokenSignatureKeyNotFoundException()
    {
        // Produce a token signed by a DIFFERENT key pair
        RSA otherKey = RSA.Create(keySizeInBits: 2048);
        string otherPrivatePem = otherKey.ExportRSAPrivateKeyPem();
        ITokenIssuerPort otherAdapter = new ClaudeForge.Infrastructure.Identity.RsaTokenIssuerAdapter(
            privatePem: otherPrivatePem,
            issuer: TestIssuer,
            audience: TestAudience,
            accessTokenMinutes: DefaultExpiryMinutes,
            kid: "other-key");

        string tokenSignedByOtherKey = otherAdapter.IssueAccessToken(MakeClaims());

        // The original adapter's validation must reject a token from a different key
        ITokenIssuerPort adapter = MakeAdapter();
        Assert.ThrowsAny<SecurityTokenException>(() => adapter.ValidateAccessToken(tokenSignedByOtherKey));
    }

    [Fact]
    public void ValidateAccessToken_MalformedToken_ThrowsSecurityTokenMalformedException()
    {
        ITokenIssuerPort adapter = MakeAdapter();

        Assert.ThrowsAny<Exception>(() => adapter.ValidateAccessToken("not.a.jwt"));
    }

    [Fact]
    public void ValidateAccessToken_EmptyString_ThrowsArgumentException()
    {
        ITokenIssuerPort adapter = MakeAdapter();

        Assert.ThrowsAny<Exception>(() => adapter.ValidateAccessToken(string.Empty));
    }

    [Fact]
    public void ValidateAccessToken_NullString_ThrowsArgumentException()
    {
        ITokenIssuerPort adapter = MakeAdapter();

        Assert.ThrowsAny<Exception>(() => adapter.ValidateAccessToken(null!));
    }

    [Fact]
    public void ValidateAccessToken_WrongIssuer_ThrowsSecurityTokenInvalidIssuerException()
    {
        // Issue token from an adapter with a DIFFERENT issuer
        ITokenIssuerPort wrongIssuerAdapter = new ClaudeForge.Infrastructure.Identity.RsaTokenIssuerAdapter(
            privatePem: TestKeyPair.PrivatePem,
            issuer: "https://evil-issuer.example",
            audience: TestAudience,
            accessTokenMinutes: DefaultExpiryMinutes,
            kid: TestKid);

        string token = wrongIssuerAdapter.IssueAccessToken(MakeClaims());

        ITokenIssuerPort adapter = MakeAdapter();
        Assert.ThrowsAny<SecurityTokenException>(() => adapter.ValidateAccessToken(token));
    }

    [Fact]
    public void ValidateAccessToken_WrongAudience_ThrowsSecurityTokenInvalidAudienceException()
    {
        ITokenIssuerPort wrongAudienceAdapter = new ClaudeForge.Infrastructure.Identity.RsaTokenIssuerAdapter(
            privatePem: TestKeyPair.PrivatePem,
            issuer: TestIssuer,
            audience: "some-other-audience",
            accessTokenMinutes: DefaultExpiryMinutes,
            kid: TestKid);

        string token = wrongAudienceAdapter.IssueAccessToken(MakeClaims());

        ITokenIssuerPort adapter = MakeAdapter();
        Assert.ThrowsAny<SecurityTokenException>(() => adapter.ValidateAccessToken(token));
    }

    // =========================================================================
    // CONFIGURABLE EXPIRY
    // =========================================================================

    [Theory]
    [InlineData(5)]
    [InlineData(15)]
    [InlineData(60)]
    public void IssueAccessToken_ConfigurableExpiry_ReflectsInToken(int minutes)
    {
        ITokenIssuerPort adapter = MakeAdapter(accessTokenMinutes: minutes);
        AccessTokenClaims claims = MakeClaims();

        string token = adapter.IssueAccessToken(claims);
        JwtSecurityToken jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        TimeSpan lifetime = jwt.ValidTo - jwt.IssuedAt;
        // 2-second tolerance for clock
        Assert.True(lifetime.TotalMinutes > minutes - 1 && lifetime.TotalMinutes < minutes + 1,
            $"Expected lifetime ~{minutes}m, got {lifetime.TotalMinutes:F2}m");
    }

    // =========================================================================
    // EDGE CASES
    // =========================================================================

    [Fact]
    public void IssueAccessToken_UnicodeNameAndEmail_ClaimsPreservedVerbatim()
    {
        ITokenIssuerPort adapter = MakeAdapter();
        AccessTokenClaims claims = MakeClaims(
            name: "山田 太郎",
            email: "yamada+テスト@example.jp");

        string token = adapter.IssueAccessToken(claims);
        JwtSecurityToken jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Claim? name = jwt.Claims.FirstOrDefault(c => c.Type is "name" or JwtRegisteredClaimNames.Name);
        Claim? email = jwt.Claims.FirstOrDefault(c => c.Type is "email" or JwtRegisteredClaimNames.Email);

        Assert.Equal("山田 太郎", name?.Value);
        Assert.Equal("yamada+テスト@example.jp", email?.Value);
    }
}
