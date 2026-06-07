using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace ClaudeForge.Infrastructure.Identity.Validation;

/// <summary>
/// Validates <see cref="JwtOptions"/> at startup.
/// Ensures the configured RSA private PEM is present and parseable.
/// </summary>
public sealed class JwtSigningKeyValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SigningKeyPrivatePem))
        {
            return ValidateOptionsResult.Fail(
                "JWT__SIGNINGKEY__PRIVATEPEM is required. " +
                "Please set the RSA private key PEM value in the JWT__SIGNINGKEY__PRIVATEPEM " +
                "environment variable or configuration entry.");
        }

        try
        {
            using RSA rsa = RSA.Create();

            // ImportFromPem accepts both PKCS#8 ("BEGIN PRIVATE KEY") and
            // traditional ("BEGIN RSA PRIVATE KEY") formats.
            // It throws CryptographicException for invalid/public-only PEMs.
            rsa.ImportFromPem(options.SigningKeyPrivatePem);

            // Verify the key actually contains private key material by attempting to sign.
            // ExportParameters(true) throws CryptographicException for public-only keys.
            _ = rsa.ExportParameters(includePrivateParameters: true);
        }
        catch (CryptographicException ex)
        {
            return ValidateOptionsResult.Fail(
                $"JWT__SIGNINGKEY__PRIVATEPEM contains an invalid or non-parseable PEM. " +
                $"The value must be a valid RSA private key (PKCS#8 or traditional PEM format). " +
                $"Parse error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ValidateOptionsResult.Fail(
                $"JWT__SIGNINGKEY__PRIVATEPEM is invalid. " +
                $"Failed to parse the PEM value: {ex.Message}");
        }

        return ValidateOptionsResult.Success;
    }
}
