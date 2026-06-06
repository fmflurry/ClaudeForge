namespace ClaudeForge.Infrastructure.Packaging;

/// <summary>
/// Thrown when a valid archive does not contain a manifest file at root level.
/// Message matches the spec verbatim.
/// </summary>
public sealed class MissingManifestException : Exception
{
    public MissingManifestException()
        : base("Package must contain plugin.json or manifest.json at root level")
    {
    }
}
