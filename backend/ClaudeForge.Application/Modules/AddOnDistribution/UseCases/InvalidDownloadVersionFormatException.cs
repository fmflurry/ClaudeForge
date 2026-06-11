using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Application.Modules.AddOnDistribution.UseCases;

/// <summary>
/// Thrown when the provided version string is not a valid semantic version.
/// Maps to HTTP 400 Bad Request via the global exception handler.
/// </summary>
public sealed class InvalidDownloadVersionFormatException : ProblemDetailsException
{
    public override int StatusCode => 400;

    public InvalidDownloadVersionFormatException()
        : base("Invalid version format. Expected semver (e.g., 1.0.0)") { }
}
