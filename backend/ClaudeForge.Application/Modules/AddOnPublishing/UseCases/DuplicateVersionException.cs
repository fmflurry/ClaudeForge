using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Application.Modules.AddOnPublishing.UseCases;

/// <summary>
/// Thrown when the given version already exists for the plugin.
/// Message format: "Version {version} already exists"
/// </summary>
public sealed class DuplicateVersionException : ProblemDetailsException
{
    public override int StatusCode => 409;

    public DuplicateVersionException(string version)
        : base($"Version {version} already exists") { }
}
