namespace ClaudeForge.Core.Shared.Exceptions;

/// <summary>
/// Base exception for all domain errors. The global exception handler converts these
/// to RFC 7807 ProblemDetails responses.
/// </summary>
public class ProblemDetailsException : Exception
{
    /// <summary>
    /// HTTP status code for this exception. Defaults to 400 Bad Request.
    /// Subclasses may override to map to other status codes (e.g. 404 Not Found).
    /// </summary>
    public virtual int StatusCode => 400;

    public ProblemDetailsException(string detail) : base(detail) { }

    protected ProblemDetailsException(string detail, Exception inner) : base(detail, inner) { }
}
