namespace ClaudeForge.Infrastructure.Packaging;

/// <summary>
/// Thrown when an archive that should be valid cannot be read (truncated, malformed magic bytes, etc.).
/// Message matches the spec verbatim.
/// </summary>
public sealed class CorruptedArchiveException : Exception
{
    public CorruptedArchiveException()
        : base("Package file is corrupted or not a valid archive")
    {
    }

    public CorruptedArchiveException(Exception inner)
        : base("Package file is corrupted or not a valid archive", inner)
    {
    }
}
