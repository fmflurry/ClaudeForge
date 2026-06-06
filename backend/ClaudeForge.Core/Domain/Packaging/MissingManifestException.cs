using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Core.Domain.Packaging;

/// <summary>
/// Thrown when a valid archive does not contain a manifest file at root level.
/// Message matches the spec verbatim.
/// </summary>
public sealed class MissingManifestException : ProblemDetailsException
{
    public override int StatusCode => 400;

    public MissingManifestException()
        : base("Package must contain plugin.json or manifest.json at root level")
    {
    }
}
