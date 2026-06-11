using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Application.Modules.AddOnPublishing.UseCases;

/// <summary>
/// Thrown when a version string does not follow the MAJOR.MINOR.PATCH semver format.
/// </summary>
public sealed class InvalidVersionFormatException : ProblemDetailsException
{
    public InvalidVersionFormatException()
        : base("Version must be in format MAJOR.MINOR.PATCH (e.g., 1.2.3)") { }
}
