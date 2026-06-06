namespace ClaudeForge.Core.Shared.Exceptions;

/// <summary>
/// Base exception for all domain errors. The global exception handler converts these
/// to RFC 7807 ProblemDetails responses.
/// </summary>
public class ProblemDetailsException : Exception
{
    public ProblemDetailsException(string detail) : base(detail) { }
}
