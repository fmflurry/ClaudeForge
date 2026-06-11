using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Application.Modules.AddOnSearch.UseCases;

/// <summary>
/// Thrown when page or limit values are not greater than 0.
/// Maps to HTTP 400 Bad Request.
/// Spec verbatim: "Page and limit must be greater than 0"
/// </summary>
public sealed class InvalidPaginationException : ProblemDetailsException
{
    public override int StatusCode => 400;

    public InvalidPaginationException()
        : base("Page and limit must be greater than 0")
    {
    }
}
