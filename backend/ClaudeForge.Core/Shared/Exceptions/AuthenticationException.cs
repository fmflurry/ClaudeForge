namespace ClaudeForge.Core.Shared.Exceptions;

/// <summary>
/// Thrown when an authentication or authorization operation fails.
/// Maps to HTTP 401 Unauthorized in the global exception handler.
/// </summary>
public sealed class AuthenticationException : ProblemDetailsException
{
    public override int StatusCode => 401;

    public AuthenticationException(string detail) : base(detail) { }

    public AuthenticationException(string detail, Exception inner) : base(detail, inner) { }
}
