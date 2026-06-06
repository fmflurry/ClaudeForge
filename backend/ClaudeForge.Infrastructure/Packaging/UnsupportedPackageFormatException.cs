namespace ClaudeForge.Infrastructure.Packaging;

/// <summary>
/// Thrown when the uploaded file extension is not in the allowed set (tar.gz, zip).
/// Message matches the spec verbatim.
/// </summary>
public sealed class UnsupportedPackageFormatException : Exception
{
    public UnsupportedPackageFormatException()
        : base("Unsupported package format. Allowed: tar.gz, zip")
    {
    }
}
