using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Application.Modules.AddOnDistribution.UseCases;

/// <summary>
/// Thrown when the requested plugin version does not exist.
/// Maps to HTTP 404 Not Found via the global exception handler.
/// </summary>
public sealed class VersionNotFoundException : ProblemDetailsException
{
    public override int StatusCode => 404;

    public VersionNotFoundException(string version)
        : base($"Plugin version {version} not found") { }
}
