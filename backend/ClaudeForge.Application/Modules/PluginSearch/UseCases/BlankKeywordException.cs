using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Application.Modules.PluginSearch.UseCases;

/// <summary>
/// Thrown when the discovery keyword is null, empty, or whitespace-only.
/// Maps to HTTP 400 Bad Request.
/// Spec verbatim: "Keyword cannot be empty"
/// </summary>
public sealed class BlankKeywordException : ProblemDetailsException
{
    public override int StatusCode => 400;

    public BlankKeywordException()
        : base("Keyword cannot be empty")
    {
    }
}
