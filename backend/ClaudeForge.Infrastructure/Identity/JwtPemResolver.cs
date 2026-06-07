using Microsoft.Extensions.Configuration;

namespace ClaudeForge.Infrastructure.Identity;

/// <summary>
/// Resolves RSA PEM content from configuration, supporting both inline values and
/// file-path fallbacks (for Docker secret mounts and similar environments).
///
/// Resolution order for the private PEM:
///   1. <c>Jwt:PrivatePem</c>            — inline value (used in tests and local dev).
///   2. <c>Jwt:PrivatePemFile</c>        — path to a file containing the PEM.
///   3. <c>JWT:SigningKey:PrivatePem_File</c> — Docker-style path
///      (env: <c>JWT__SIGNINGKEY__PRIVATEPEM_FILE</c>).
///
/// Resolution order for the public PEM:
///   1. <c>Jwt:PublicPem</c>             — inline value.
///   2. <c>Jwt:PublicPemFile</c>         — path to a file containing the PEM.
///   3. <c>JWT:SigningKey:PublicPem_File</c> — Docker-style path
///      (env: <c>JWT__SIGNINGKEY__PUBLICPEM_FILE</c>).
/// </summary>
public static class JwtPemResolver
{
    /// <summary>
    /// Resolves the RSA private key PEM from the supplied configuration.
    /// Returns <see langword="null"/> if no configured source yields a non-empty value.
    /// Throws <see cref="InvalidOperationException"/> if a file path is configured but
    /// the file cannot be read.
    /// </summary>
    public static string? ResolvePrivatePem(IConfiguration cfg)
    {
        return ResolveFromInlineOrFile(
            inline: cfg["Jwt:PrivatePem"],
            filePath: cfg["Jwt:PrivatePemFile"],
            dockerFilePath: cfg["JWT:SigningKey:PrivatePem_File"],
            keyDescription: "private PEM");
    }

    /// <summary>
    /// Resolves the RSA public key PEM from the supplied configuration.
    /// Returns <see langword="null"/> if no configured source yields a non-empty value.
    /// Throws <see cref="InvalidOperationException"/> if a file path is configured but
    /// the file cannot be read.
    /// </summary>
    public static string? ResolvePublicPem(IConfiguration cfg)
    {
        return ResolveFromInlineOrFile(
            inline: cfg["Jwt:PublicPem"],
            filePath: cfg["Jwt:PublicPemFile"],
            dockerFilePath: cfg["JWT:SigningKey:PublicPem_File"],
            keyDescription: "public PEM");
    }

    private static string? ResolveFromInlineOrFile(
        string? inline,
        string? filePath,
        string? dockerFilePath,
        string keyDescription)
    {
        if (!string.IsNullOrWhiteSpace(inline))
        {
            return inline;
        }

        string? effectiveFilePath = !string.IsNullOrWhiteSpace(filePath)
            ? filePath
            : !string.IsNullOrWhiteSpace(dockerFilePath)
                ? dockerFilePath
                : null;

        if (effectiveFilePath is null)
        {
            return null;
        }

        try
        {
            return File.ReadAllText(effectiveFilePath).Trim();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"JWT {keyDescription} file path '{effectiveFilePath}' is configured but could not be read: {ex.Message}",
                ex);
        }
    }
}
