using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Application.Modules.AddOnPublishing.UseCases;

/// <summary>
/// Thrown when a plugin with the same name (case-insensitive) already exists.
/// Message format: "A plugin with name '{name}' already exists"
/// </summary>
public sealed class DuplicateAddOnNameException : ProblemDetailsException
{
    public override int StatusCode => 409;

    public DuplicateAddOnNameException(string name)
        : base($"A plugin with name '{name}' already exists") { }
}
