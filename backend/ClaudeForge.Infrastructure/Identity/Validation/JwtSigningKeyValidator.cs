using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace ClaudeForge.Infrastructure.Identity.Validation;

/// <summary>
/// Validates <see cref="JwtOptions"/> at startup.
/// Ensures the configured RSA private PEM is present and parseable.
///
/// Resolution order for the private key:
///   1. <see cref="JwtOptions.SigningKeyPrivatePem"/> — inline PEM value (env: JWT__SIGNINGKEYP RIVATEPEM).
///   2. <see cref="JwtOptions.SigningKeyPrivatePemFile"/> — path to a file containing the PEM
///      (env: JWT__SIGNINGKEYP RIVATEPEMFILE). The file is read at validation time, its
///      content is trimmed and used as the PEM value. Typical use: Docker secret mounts.
///
/// If both are absent or empty the validator fails fast with a clear message that mentions
/// both configuration alternatives.
/// </summary>
public sealed class JwtSigningKeyValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        string? pem = ResolvePem(options, out string? fileReadError);

        if (fileReadError is not null)
        {
            return ValidateOptionsResult.Fail(fileReadError);
        }

        if (string.IsNullOrWhiteSpace(pem))
        {
            return ValidateOptionsResult.Fail(
                "JWT signing key is required but was not found. " +
                "Provide it via one of the following: " +
                "(1) JWT__SIGNINGKEY__PRIVATEPEM — set the RSA private key PEM value directly, or " +
                "(2) JWT__SIGNINGKEY__PRIVATEPEM_FILE (env) / JWT__SIGNINGKEYP RIVATEPEMFILE (flat) " +
                "— set the path to a file containing the PEM (e.g. a Docker secret mount).");
        }

        try
        {
            using RSA rsa = RSA.Create();

            // ImportFromPem accepts both PKCS#8 ("BEGIN PRIVATE KEY") and
            // traditional ("BEGIN RSA PRIVATE KEY") formats.
            // It throws CryptographicException for invalid/public-only PEMs.
            rsa.ImportFromPem(pem);

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

    /// <summary>
    /// Resolves the private PEM from inline config or file fallback.
    /// Returns <see langword="null"/> for both <paramref name="pem"/> and
    /// <paramref name="fileReadError"/> when neither source is configured.
    /// Returns a non-null <paramref name="fileReadError"/> if the file path is set but
    /// the file cannot be read.
    /// </summary>
    public static string? ResolvePem(JwtOptions options, out string? fileReadError)
    {
        fileReadError = null;

        if (!string.IsNullOrWhiteSpace(options.SigningKeyPrivatePem))
        {
            return options.SigningKeyPrivatePem;
        }

        if (!string.IsNullOrWhiteSpace(options.SigningKeyPrivatePemFile))
        {
            try
            {
                string content = File.ReadAllText(options.SigningKeyPrivatePemFile).Trim();
                return content;
            }
            catch (Exception ex)
            {
                fileReadError =
                    $"JWT__SIGNINGKEYP RIVATEPEMFILE points to '{options.SigningKeyPrivatePemFile}' " +
                    $"but the file could not be read: {ex.Message}";
                return null;
            }
        }

        return null;
    }
}
