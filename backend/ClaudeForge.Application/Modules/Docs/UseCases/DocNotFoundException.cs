using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Application.Modules.Docs.UseCases;

/// <summary>
/// Thrown when a documentation page cannot be found by slug.
/// Maps to 404 Not Found via the global exception handler.
/// Spec verbatim detail: "Documentation page not found"
/// </summary>
public sealed class DocNotFoundException : ProblemDetailsException
{
    public override int StatusCode => 404;

    public DocNotFoundException()
        : base("Documentation page not found")
    {
    }
}
