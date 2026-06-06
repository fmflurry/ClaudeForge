using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Application.Modules.PluginCatalog.UseCases;

/// <summary>
/// Thrown when a plugin cannot be found by its identifier.
/// Maps to HTTP 404 Not Found via the global exception handler.
/// </summary>
public sealed class PluginNotFoundException : ProblemDetailsException
{
    public override int StatusCode => 404;

    public PluginNotFoundException() : base("Plugin not found") { }
}
