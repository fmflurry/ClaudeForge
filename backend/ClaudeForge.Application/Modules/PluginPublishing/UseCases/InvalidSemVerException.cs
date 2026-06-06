using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Application.Modules.PluginPublishing.UseCases;

/// <summary>
/// Thrown when the initial version in the manifest is not a valid semantic version.
/// </summary>
public sealed class InvalidSemVerException : ProblemDetailsException
{
    public InvalidSemVerException()
        : base("initialVersion must be a valid semantic version (e.g., 1.0.0)") { }
}
