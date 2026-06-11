using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Application.Modules.AddOnPublishing.UseCases;

/// <summary>
/// Thrown when attempting to publish a version to a plugin that does not exist.
/// </summary>
public sealed class AddOnNotFoundForVersionException : ProblemDetailsException
{
    public override int StatusCode => 404;

    public AddOnNotFoundForVersionException()
        : base("Plugin not found") { }
}
