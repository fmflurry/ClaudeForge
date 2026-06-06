using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Application.Modules.PluginCatalog.UseCases;

/// <summary>
/// Thrown when a category filter value is not part of the controlled vocabulary.
/// Maps to HTTP 400 Bad Request via the global exception handler.
/// </summary>
public sealed class InvalidCategoryException : ProblemDetailsException
{
    public InvalidCategoryException(string message) : base(message) { }
}
