using ClaudeForge.Infrastructure.Identity.Validation;
using Microsoft.Extensions.Options;

namespace ClaudeForge.Tests.Unit.Identity;

/// <summary>
/// Unit tests for Task 5.1 — startup secret/config validator.
///
/// Exercises <see cref="OidcConfigValidator"/> (implements IValidateOptions&lt;OidcOptions&gt;)
/// and <see cref="JwtSigningKeyValidator"/> (implements IValidateOptions&lt;JwtOptions&gt;)
/// directly — no host bootstrap required.  The validators must enforce fail-fast rules
/// that prevent the service from starting when required secrets are absent or malformed.
///
/// ═══════════════════════════════════════════════════════════════════════════════
/// Contracts the coder MUST create
///
///   NAMESPACE: ClaudeForge.Infrastructure.Identity.Validation
///
///   sealed class OidcOptions
///     string[]  EnabledProviders   — from OIDC__ENABLEDPROVIDERS (e.g. ["google","microsoft"])
///     ProviderConfig? Google       — populated when "google" in EnabledProviders
///     ProviderConfig? Microsoft    — populated when "microsoft" in EnabledProviders
///     string?   AllowedLoopbackRedirect  — from OIDC__ALLOWEDLOOPBACKREDIRECT
///
///   sealed class ProviderConfig
///     string? ClientId
///     string? ClientSecret
///     string? RedirectUri
///     string? Tenant              — only used by Microsoft
///
///   sealed class JwtOptions
///     string? Issuer
///     string? Audience
///     string? SigningKeyPrivatePem  — raw RS256 PEM (PKCS#8 "BEGIN PRIVATE KEY" or traditional)
///     int     AccessTokenMinutes   — default 15
///     int     RefreshTokenDays     — default 30
///
///   sealed class OidcConfigValidator : IValidateOptions&lt;OidcOptions&gt;
///     Validate(name, options) rules:
///       - For each name in EnabledProviders:
///           ▸ "google"    → Google must be non-null; ClientId, ClientSecret, RedirectUri non-null/whitespace
///           ▸ "microsoft" → Microsoft must be non-null; ClientId, ClientSecret, RedirectUri non-null/whitespace
///           ▸ Unknown provider name → failure mentioning the unknown provider
///       - EnabledProviders null/empty is accepted (no provider enabled = valid for dev)
///       - Failure messages contain the config key path (e.g. "OIDC__GOOGLE__CLIENTID")
///
///   sealed class JwtSigningKeyValidator : IValidateOptions&lt;JwtOptions&gt;
///     Validate(name, options) rules:
///       - SigningKeyPrivatePem null/whitespace → failure mentioning "JWT__SIGNINGKEY__PRIVATEPEM"
///       - SigningKeyPrivatePem non-parseable as RSA private key → failure mentioning "parseable" or "PEM"
///       - RedirectUri must be absolute HTTPS when ASPNETCORE_ENVIRONMENT == "Production"
///         (validator receives IHostEnvironment or checks environment via ctor)
///       - Valid PEM (any RSA private key, any size) → success
///
///   IMPORTANT: The config redirect URI Production-HTTPS check lives in OidcConfigValidator
///   (not JwtSigningKeyValidator). OidcConfigValidator receives a flag indicating whether
///   the current environment is Production. When Production=true:
///     - Each enabled provider's RedirectUri must be absolute and start with "https://"
///     - Non-absolute or non-HTTPS redirect URI → failure mentioning "RedirectUri" and "Production"
/// ═══════════════════════════════════════════════════════════════════════════════
/// </summary>
public sealed class OidcConfigValidatorTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Helper factories
    // ─────────────────────────────────────────────────────────────────────────

    private static IValidateOptions<OidcOptions> MakeOidcValidator(bool isProduction = false) =>
        new OidcConfigValidator(isProduction);

    private static ProviderConfig ValidGoogleConfig(string redirectUri = "https://app.example.com/auth/callback") =>
        new()
        {
            ClientId = "google-client-id",
            ClientSecret = "google-client-secret",
            RedirectUri = redirectUri,
        };

    private static ProviderConfig ValidMicrosoftConfig(string redirectUri = "https://app.example.com/auth/callback") =>
        new()
        {
            ClientId = "ms-client-id",
            ClientSecret = "ms-client-secret",
            RedirectUri = redirectUri,
            Tenant = "common",
        };

    // ─────────────────────────────────────────────────────────────────────────
    // Happy path — no enabled providers (dev scenario)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_NoEnabledProviders_ReturnsSuccess()
    {
        IValidateOptions<OidcOptions> validator = MakeOidcValidator();
        OidcOptions options = new() { EnabledProviders = [] };

        ValidateOptionsResult result = validator.Validate(null, options);

        Assert.True(result.Succeeded, $"Expected success but got: {result.FailureMessage}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Happy path — Google enabled with all required fields
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_GoogleEnabled_AllFieldsPresent_ReturnsSuccess()
    {
        IValidateOptions<OidcOptions> validator = MakeOidcValidator();
        OidcOptions options = new()
        {
            EnabledProviders = ["google"],
            Google = ValidGoogleConfig(),
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        Assert.True(result.Succeeded, $"Expected success but got: {result.FailureMessage}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Happy path — Microsoft enabled with all required fields
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_MicrosoftEnabled_AllFieldsPresent_ReturnsSuccess()
    {
        IValidateOptions<OidcOptions> validator = MakeOidcValidator();
        OidcOptions options = new()
        {
            EnabledProviders = ["microsoft"],
            Microsoft = ValidMicrosoftConfig(),
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        Assert.True(result.Succeeded, $"Expected success but got: {result.FailureMessage}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Happy path — both providers enabled with all required fields
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_BothProviders_AllFieldsPresent_ReturnsSuccess()
    {
        IValidateOptions<OidcOptions> validator = MakeOidcValidator();
        OidcOptions options = new()
        {
            EnabledProviders = ["google", "microsoft"],
            Google = ValidGoogleConfig(),
            Microsoft = ValidMicrosoftConfig(),
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        Assert.True(result.Succeeded, $"Expected success but got: {result.FailureMessage}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Failure — Google enabled but Google config section is null
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_GoogleEnabled_NullGoogleConfig_ReturnsFailure()
    {
        IValidateOptions<OidcOptions> validator = MakeOidcValidator();
        OidcOptions options = new()
        {
            EnabledProviders = ["google"],
            Google = null,
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.FailureMessage);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Failure — Google enabled but ClientId missing
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_GoogleEnabled_MissingClientId_ReturnsFailureMentioningClientId(string? clientId)
    {
        IValidateOptions<OidcOptions> validator = MakeOidcValidator();
        OidcOptions options = new()
        {
            EnabledProviders = ["google"],
            Google = new ProviderConfig
            {
                ClientId = clientId,
                ClientSecret = "secret",
                RedirectUri = "https://app.example.com/callback",
            },
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.FailureMessage);
        Assert.Contains("CLIENTID", result.FailureMessage!, StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Failure — Google enabled but ClientSecret missing
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_GoogleEnabled_MissingClientSecret_ReturnsFailureMentioningClientSecret(string? secret)
    {
        IValidateOptions<OidcOptions> validator = MakeOidcValidator();
        OidcOptions options = new()
        {
            EnabledProviders = ["google"],
            Google = new ProviderConfig
            {
                ClientId = "client-id",
                ClientSecret = secret,
                RedirectUri = "https://app.example.com/callback",
            },
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.FailureMessage);
        Assert.Contains("CLIENTSECRET", result.FailureMessage!, StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Failure — Google enabled but RedirectUri missing
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_GoogleEnabled_MissingRedirectUri_ReturnsFailureMentioningRedirectUri(string? uri)
    {
        IValidateOptions<OidcOptions> validator = MakeOidcValidator();
        OidcOptions options = new()
        {
            EnabledProviders = ["google"],
            Google = new ProviderConfig
            {
                ClientId = "client-id",
                ClientSecret = "secret",
                RedirectUri = uri,
            },
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.FailureMessage);
        Assert.Contains("REDIRECTURI", result.FailureMessage!, StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Failure — Microsoft enabled but ClientId missing
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_MicrosoftEnabled_MissingClientId_ReturnsFailure(string? clientId)
    {
        IValidateOptions<OidcOptions> validator = MakeOidcValidator();
        OidcOptions options = new()
        {
            EnabledProviders = ["microsoft"],
            Microsoft = new ProviderConfig
            {
                ClientId = clientId,
                ClientSecret = "secret",
                RedirectUri = "https://app.example.com/callback",
                Tenant = "common",
            },
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.FailureMessage);
        Assert.Contains("CLIENTID", result.FailureMessage!, StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Failure — unknown provider listed in EnabledProviders
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_UnknownProvider_ReturnsFailureMentioningProviderName()
    {
        IValidateOptions<OidcOptions> validator = MakeOidcValidator();
        OidcOptions options = new()
        {
            EnabledProviders = ["github"],  // not a supported provider
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.FailureMessage);
        Assert.Contains("github", result.FailureMessage!, StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Production environment — RedirectUri must be absolute HTTPS
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("http://app.example.com/callback")]  // HTTP, not HTTPS
    [InlineData("app.example.com/callback")]          // relative, not absolute
    [InlineData("/auth/callback")]                    // relative path
    public void Validate_Production_NonHttpsRedirectUri_ReturnsFailureMentioningProductionAndRedirectUri(
        string redirectUri)
    {
        IValidateOptions<OidcOptions> validator = MakeOidcValidator(isProduction: true);
        OidcOptions options = new()
        {
            EnabledProviders = ["google"],
            Google = ValidGoogleConfig(redirectUri: redirectUri),
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.FailureMessage);
        // Must mention both the redirect and the production constraint
        Assert.Contains("RedirectUri", result.FailureMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Production", result.FailureMessage!, StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Non-production — HTTP redirect is permitted (loopback/dev scenario)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_Development_HttpRedirectUri_ReturnsSuccess()
    {
        IValidateOptions<OidcOptions> validator = MakeOidcValidator(isProduction: false);
        OidcOptions options = new()
        {
            EnabledProviders = ["google"],
            Google = new ProviderConfig
            {
                ClientId = "client-id",
                ClientSecret = "secret",
                RedirectUri = "http://localhost:5173/auth/callback",  // HTTP loopback
            },
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        Assert.True(result.Succeeded, $"Expected success in non-Production but got: {result.FailureMessage}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Production — HTTPS redirect is accepted
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_Production_HttpsAbsoluteRedirectUri_ReturnsSuccess()
    {
        IValidateOptions<OidcOptions> validator = MakeOidcValidator(isProduction: true);
        OidcOptions options = new()
        {
            EnabledProviders = ["google"],
            Google = ValidGoogleConfig(redirectUri: "https://app.example.com/auth/callback"),
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        Assert.True(result.Succeeded, $"Expected success but got: {result.FailureMessage}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Multiple providers failing — all errors reported (not short-circuit)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_BothProviders_MissingKeys_AllErrorsReported()
    {
        IValidateOptions<OidcOptions> validator = MakeOidcValidator();
        OidcOptions options = new()
        {
            EnabledProviders = ["google", "microsoft"],
            Google = new ProviderConfig { ClientId = null, ClientSecret = null, RedirectUri = null },
            Microsoft = new ProviderConfig { ClientId = null, ClientSecret = null, RedirectUri = null },
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.FailureMessage);
        // Must mention both providers' problems (not just the first)
        Assert.Contains("google", result.FailureMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("microsoft", result.FailureMessage!, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Unit tests for Task 5.1 — JWT signing key validator.
///
/// Verifies that <see cref="JwtSigningKeyValidator"/> rejects missing or unparseable
/// RS256 private PEMs and accepts valid ones.
/// </summary>
public sealed class JwtSigningKeyValidatorTests
{
    private static IValidateOptions<JwtOptions> MakeValidator() => new JwtSigningKeyValidator();

    private static string GenerateValidPrivatePem()
    {
        using System.Security.Cryptography.RSA rsa =
            System.Security.Cryptography.RSA.Create(keySizeInBits: 2048);
        return rsa.ExportPkcs8PrivateKeyPem();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Happy path — valid RS256 private PEM
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_ValidPrivatePem_ReturnsSuccess()
    {
        IValidateOptions<JwtOptions> validator = MakeValidator();
        JwtOptions options = new()
        {
            Issuer = "https://claudeforge.io",
            Audience = "claudeforge-api",
            SigningKeyPrivatePem = GenerateValidPrivatePem(),
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        Assert.True(result.Succeeded, $"Expected success but got: {result.FailureMessage}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Failure — SigningKeyPrivatePem is null
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_NullSigningKeyPrivatePem_ReturnsFailureMentioningKeyConfig()
    {
        IValidateOptions<JwtOptions> validator = MakeValidator();
        JwtOptions options = new()
        {
            Issuer = "https://claudeforge.io",
            Audience = "claudeforge-api",
            SigningKeyPrivatePem = null,
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.FailureMessage);
        Assert.Contains("JWT__SIGNINGKEY__PRIVATEPEM", result.FailureMessage!, StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Failure — SigningKeyPrivatePem is whitespace
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhitespaceSigningKeyPrivatePem_ReturnsFailure(string pem)
    {
        IValidateOptions<JwtOptions> validator = MakeValidator();
        JwtOptions options = new()
        {
            Issuer = "https://claudeforge.io",
            Audience = "claudeforge-api",
            SigningKeyPrivatePem = pem,
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.FailureMessage);
        Assert.Contains("JWT__SIGNINGKEY__PRIVATEPEM", result.FailureMessage!, StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Failure — SigningKeyPrivatePem is not a valid PEM (garbage string)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_UnparseablePrivatePem_ReturnsFailureMentioningParseableOrPem()
    {
        IValidateOptions<JwtOptions> validator = MakeValidator();
        JwtOptions options = new()
        {
            Issuer = "https://claudeforge.io",
            Audience = "claudeforge-api",
            SigningKeyPrivatePem = "this-is-not-a-valid-pem-string-at-all",
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.FailureMessage);
        // Must mention that the key couldn't be parsed — "parseable", "PEM", "invalid", or similar
        bool mentionsParseProblem =
            result.FailureMessage!.Contains("parseable", StringComparison.OrdinalIgnoreCase) ||
            result.FailureMessage.Contains("PEM", StringComparison.OrdinalIgnoreCase) ||
            result.FailureMessage.Contains("invalid", StringComparison.OrdinalIgnoreCase);
        Assert.True(mentionsParseProblem,
            $"Failure message must indicate a parse problem but got: {result.FailureMessage}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Failure — PEM header present but body is corrupted
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_CorruptedPemBody_ReturnsFailure()
    {
        IValidateOptions<JwtOptions> validator = MakeValidator();
        JwtOptions options = new()
        {
            Issuer = "https://claudeforge.io",
            Audience = "claudeforge-api",
            // Valid header/footer but the base64 body is garbage
            SigningKeyPrivatePem =
                "-----BEGIN PRIVATE KEY-----\n" +
                "NOT_VALID_BASE64_!@#$%^&*()\n" +
                "-----END PRIVATE KEY-----",
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.FailureMessage);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Edge — a public-key PEM (not a private key) is rejected
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_PublicKeyPem_ReturnsFailure()
    {
        using System.Security.Cryptography.RSA rsa =
            System.Security.Cryptography.RSA.Create(keySizeInBits: 2048);
        string publicPem = rsa.ExportRSAPublicKeyPem();  // public, not private

        IValidateOptions<JwtOptions> validator = MakeValidator();
        JwtOptions options = new()
        {
            Issuer = "https://claudeforge.io",
            Audience = "claudeforge-api",
            SigningKeyPrivatePem = publicPem,
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        // A public key cannot sign — must fail validation
        Assert.False(result.Succeeded);
        Assert.NotNull(result.FailureMessage);
    }
}
