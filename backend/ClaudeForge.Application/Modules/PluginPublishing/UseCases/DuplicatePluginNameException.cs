using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Application.Modules.PluginPublishing.UseCases;

/// <summary>
/// Thrown when a plugin with the same name (case-insensitive) already exists.
/// Message format: "A plugin with name '{name}' already exists"
/// </summary>
public sealed class DuplicatePluginNameException : ProblemDetailsException
{
    public override int StatusCode => 409;

    public DuplicatePluginNameException(string name)
        : base($"A plugin with name '{name}' already exists") { }
}
