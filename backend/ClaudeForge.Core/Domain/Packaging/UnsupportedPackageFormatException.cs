using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Core.Domain.Packaging;

/// <summary>
/// Thrown when the uploaded file extension is not in the allowed set (tar.gz, zip).
/// Message matches the spec verbatim.
/// </summary>
public sealed class UnsupportedPackageFormatException : ProblemDetailsException
{
    public override int StatusCode => 400;

    public UnsupportedPackageFormatException()
        : base("Unsupported package format. Allowed: tar.gz, zip")
    {
    }
}
