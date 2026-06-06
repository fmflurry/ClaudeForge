namespace ClaudeForge.Core.Ports;

/// <summary>
/// Thrown when a caller attempts to overwrite an existing immutable package artifact.
/// </summary>
public sealed class PackageAlreadyExistsException : Exception
{
    public PackageAlreadyExistsException(string key)
        : base($"A package already exists at key '{key}' and cannot be overwritten (immutability invariant).")
    {
    }
}
