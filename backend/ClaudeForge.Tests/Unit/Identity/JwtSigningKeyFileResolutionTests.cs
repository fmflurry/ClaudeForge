using System.Security.Cryptography;
using ClaudeForge.Core.Identity.Ports;
using ClaudeForge.Infrastructure.Identity;
using ClaudeForge.Infrastructure.Identity.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ClaudeForge.Tests.Unit.Identity;

/// <summary>
/// Tests for Docker-secret / file-based JWT key resolution.
///
/// Covers the two primary scenarios described in the deployment gap fix:
///   1. Inline key absent, file path set → signing key resolves; tokens issue and validate.
///   2. Neither inline nor file configured → validator fails fast, message mentions both
///      JWT__SIGNINGKEY__PRIVATEPEM and the _FILE alternative.
///
/// Existing inline-key tests live in <see cref="JwtSigningKeyValidatorTests"/>
/// and must not be broken by this change.
/// </summary>
public sealed class JwtSigningKeyFileResolutionTests : IDisposable
{
    // ─────────────────────────────────────────────────────────────────────────
    // Test fixtures
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly (string PrivatePem, string PublicPem) TestKey = GenerateTestKey();
    private readonly List<string> _tempFiles = new();

    private static (string PrivatePem, string PublicPem) GenerateTestKey()
    {
        using RSA rsa = RSA.Create(keySizeInBits: 2048);
        return (rsa.ExportPkcs8PrivateKeyPem(), rsa.ExportRSAPublicKeyPem());
    }

    private string WriteTempPemFile(string content)
    {
        string path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (string path in _tempFiles)
        {
            try { File.Delete(path); }
            catch { /* best-effort cleanup */ }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Validator: file fallback path
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validator_InlineEmpty_FilePathSet_ValidPem_ReturnsSuccess()
    {
        string pemFile = WriteTempPemFile(TestKey.PrivatePem);

        IValidateOptions<JwtOptions> validator = new JwtSigningKeyValidator();
        JwtOptions options = new()
        {
            Issuer = "https://claudeforge.io",
            Audience = "claudeforge-api",
            SigningKeyPrivatePem = null,           // inline absent
            SigningKeyPrivatePemFile = pemFile,    // file path set
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        Assert.True(result.Succeeded, $"Expected success but got: {result.FailureMessage}");
    }

    [Fact]
    public void Validator_InlineWhitespace_FilePathSet_ValidPem_ReturnsSuccess()
    {
        string pemFile = WriteTempPemFile(TestKey.PrivatePem);

        IValidateOptions<JwtOptions> validator = new JwtSigningKeyValidator();
        JwtOptions options = new()
        {
            Issuer = "https://claudeforge.io",
            Audience = "claudeforge-api",
            SigningKeyPrivatePem = "   ",          // inline whitespace → treated as absent
            SigningKeyPrivatePemFile = pemFile,
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        Assert.True(result.Succeeded, $"Expected success but got: {result.FailureMessage}");
    }

    [Fact]
    public void Validator_FileContainsWhitespaceAroundPem_StripsAndSucceeds()
    {
        // Docker secrets often have a trailing newline; trimming must handle it.
        string pemFile = WriteTempPemFile($"\n  {TestKey.PrivatePem}\n\n");

        IValidateOptions<JwtOptions> validator = new JwtSigningKeyValidator();
        JwtOptions options = new()
        {
            Issuer = "https://claudeforge.io",
            Audience = "claudeforge-api",
            SigningKeyPrivatePem = null,
            SigningKeyPrivatePemFile = pemFile,
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        Assert.True(result.Succeeded, $"Expected success but got: {result.FailureMessage}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Validator: neither key configured → fail fast with both alternatives mentioned
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validator_NeitherInlineNorFile_ReturnsFailureMentioningBothAlternatives()
    {
        IValidateOptions<JwtOptions> validator = new JwtSigningKeyValidator();
        JwtOptions options = new()
        {
            Issuer = "https://claudeforge.io",
            Audience = "claudeforge-api",
            SigningKeyPrivatePem = null,
            SigningKeyPrivatePemFile = null,
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.FailureMessage);
        // Must mention the inline env var
        Assert.Contains("JWT__SIGNINGKEY__PRIVATEPEM", result.FailureMessage!, StringComparison.OrdinalIgnoreCase);
        // Must mention the file alternative
        Assert.Contains("FILE", result.FailureMessage!, StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Validator: file path set but file does not exist
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validator_FilePathSetButFileNotFound_ReturnsFailure()
    {
        IValidateOptions<JwtOptions> validator = new JwtSigningKeyValidator();
        JwtOptions options = new()
        {
            Issuer = "https://claudeforge.io",
            Audience = "claudeforge-api",
            SigningKeyPrivatePem = null,
            SigningKeyPrivatePemFile = "/nonexistent/path/jwt_private_pem",
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.FailureMessage);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // JwtPemResolver: resolves private PEM from Docker-style config key
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void PemResolver_DockerFileKey_ReturnsPrivatePem()
    {
        string pemFile = WriteTempPemFile(TestKey.PrivatePem);

        IConfiguration cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Matches env JWT__SIGNINGKEY__PRIVATEPEM_FILE → config JWT:SigningKey:PrivatePem_File
                ["JWT:SigningKey:PrivatePem_File"] = pemFile,
            })
            .Build();

        string? resolved = JwtPemResolver.ResolvePrivatePem(cfg);

        Assert.NotNull(resolved);
        Assert.False(string.IsNullOrWhiteSpace(resolved));
        // Trimmed content must match original PEM (modulo surrounding whitespace)
        Assert.Equal(TestKey.PrivatePem.Trim(), resolved!.Trim());
    }

    [Fact]
    public void PemResolver_InlineKey_TakesPrecedenceOverFile()
    {
        string differentPem;
        using (RSA other = RSA.Create(keySizeInBits: 2048))
        {
            differentPem = other.ExportPkcs8PrivateKeyPem();
        }

        string pemFile = WriteTempPemFile(differentPem);

        IConfiguration cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:PrivatePem"] = TestKey.PrivatePem,    // inline wins
                ["JWT:SigningKey:PrivatePem_File"] = pemFile,
            })
            .Build();

        string? resolved = JwtPemResolver.ResolvePrivatePem(cfg);

        Assert.Equal(TestKey.PrivatePem, resolved);
    }

    [Fact]
    public void PemResolver_NeitherKeyConfigured_ReturnsNull()
    {
        IConfiguration cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        string? resolved = JwtPemResolver.ResolvePrivatePem(cfg);

        Assert.Null(resolved);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // End-to-end: file-only config → token issues and validates
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EndToEnd_PrivatePemFromFile_TokenIssuesAndValidates()
    {
        // Arrange: write key to a temp file, simulate Docker secret mount.
        string pemFile = WriteTempPemFile(TestKey.PrivatePem);

        IConfiguration cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Inline absent; only the Docker file key is set.
                ["JWT:SigningKey:PrivatePem_File"] = pemFile,
                ["Jwt:Issuer"] = "https://claudeforge.test",
                ["Jwt:Audience"] = "claudeforge-api-test",
            })
            .Build();

        string? pem = JwtPemResolver.ResolvePrivatePem(cfg);
        Assert.False(string.IsNullOrWhiteSpace(pem), "PEM must be resolved from file");

        // Act: build the adapter and issue a token.
        RsaTokenIssuerAdapter adapter = new(
            privatePem: pem!,
            issuer: "https://claudeforge.test",
            audience: "claudeforge-api-test",
            accessTokenMinutes: 15,
            kid: "test-kid");

        AccessTokenClaims claims = new(
            UserId: Guid.NewGuid(),
            Email: "alice@example.com",
            Name: "Alice",
            Provider: "test");

        string token = adapter.IssueAccessToken(claims);

        // Assert: token is non-empty and self-validates.
        Assert.False(string.IsNullOrWhiteSpace(token));

        System.Security.Claims.ClaimsPrincipal principal = adapter.ValidateAccessToken(token);
        Assert.NotNull(principal);
        string? sub = principal.FindFirst("sub")?.Value;
        Assert.Equal(claims.UserId.ToString(), sub);
    }

    [Fact]
    public void EndToEnd_FileContainsTrailingNewline_TokenIssuesAndValidates()
    {
        // Realistic: most secret management tools append a newline to files.
        string pemFile = WriteTempPemFile(TestKey.PrivatePem + "\n");

        string? pem = JwtPemResolver.ResolvePrivatePem(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["JWT:SigningKey:PrivatePem_File"] = pemFile,
                })
                .Build());

        Assert.False(string.IsNullOrWhiteSpace(pem));

        RsaTokenIssuerAdapter adapter = new(
            privatePem: pem!,
            issuer: "https://claudeforge.test",
            audience: "claudeforge-api-test",
            accessTokenMinutes: 5,
            kid: "newline-test-kid");

        string token = adapter.IssueAccessToken(new AccessTokenClaims(
            Guid.NewGuid(), "bob@example.com", "Bob", "test"));

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.NotNull(adapter.ValidateAccessToken(token));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ResolvePem internal helper tests (white-box)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ResolvePem_InlinePresent_FileIgnored()
    {
        JwtOptions options = new()
        {
            SigningKeyPrivatePem = TestKey.PrivatePem,
            SigningKeyPrivatePemFile = "/should/not/be/read",
        };

        string? pem = JwtSigningKeyValidator.ResolvePem(options, out string? err);

        Assert.Equal(TestKey.PrivatePem, pem);
        Assert.Null(err);
    }

    [Fact]
    public void ResolvePem_FilePathPresent_ReturnsFileContent()
    {
        string pemFile = WriteTempPemFile(TestKey.PrivatePem);

        JwtOptions options = new()
        {
            SigningKeyPrivatePem = null,
            SigningKeyPrivatePemFile = pemFile,
        };

        string? pem = JwtSigningKeyValidator.ResolvePem(options, out string? err);

        Assert.Equal(TestKey.PrivatePem, pem);
        Assert.Null(err);
    }

    [Fact]
    public void ResolvePem_FileNotFound_SetsErrorOutput()
    {
        JwtOptions options = new()
        {
            SigningKeyPrivatePem = null,
            SigningKeyPrivatePemFile = "/does/not/exist/jwt.pem",
        };

        string? pem = JwtSigningKeyValidator.ResolvePem(options, out string? err);

        Assert.Null(pem);
        Assert.NotNull(err);
        Assert.Contains("could not be read", err!, StringComparison.OrdinalIgnoreCase);
    }
}
