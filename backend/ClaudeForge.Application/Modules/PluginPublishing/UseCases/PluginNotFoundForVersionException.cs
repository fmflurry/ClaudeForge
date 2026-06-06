using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Application.Modules.PluginPublishing.UseCases;

/// <summary>
/// Thrown when attempting to publish a version to a plugin that does not exist.
/// </summary>
public sealed class PluginNotFoundForVersionException : ProblemDetailsException
{
    public override int StatusCode => 404;

    public PluginNotFoundForVersionException()
        : base("Plugin not found") { }
}
