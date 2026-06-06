using ClaudeForge.Core.Shared.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ClaudeForge.Api.Infrastructure;

/// <summary>
/// Global exception handler that converts domain exceptions to RFC 7807 ProblemDetails responses.
/// Domain exceptions (ProblemDetailsException) → 400 Bad Request.
/// Unhandled exceptions → 500 Internal Server Error.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ProblemDetails problemDetails = exception switch
        {
            ProblemDetailsException domainEx => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = domainEx.Message,
            },
            _ => CreateUnexpectedError(exception),
        };

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }

    private ProblemDetails CreateUnexpectedError(Exception exception)
    {
        _logger.LogError(exception, "Unexpected error: {Message}", exception.Message);
        return new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Internal Server Error",
            Detail = "An unexpected error occurred. Please try again later.",
        };
    }
}
