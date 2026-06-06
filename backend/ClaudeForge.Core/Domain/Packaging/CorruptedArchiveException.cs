using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Core.Domain.Packaging;

/// <summary>
/// Thrown when an archive that should be valid cannot be read (truncated, malformed magic bytes, etc.).
/// Message matches the spec verbatim.
/// </summary>
public sealed class CorruptedArchiveException : ProblemDetailsException
{
    public override int StatusCode => 400;

    public CorruptedArchiveException()
        : base("Package file is corrupted or not a valid archive")
    {
    }

    public CorruptedArchiveException(Exception inner)
        : base("Package file is corrupted or not a valid archive", inner)
    {
    }
}
