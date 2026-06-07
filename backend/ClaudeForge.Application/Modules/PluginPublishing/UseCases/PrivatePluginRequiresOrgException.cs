using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Application.Modules.PluginPublishing.UseCases;

/// <summary>
/// Thrown when a caller attempts to publish a private plugin without specifying an ownerOrgId.
/// Maps to HTTP 400 Bad Request.
/// </summary>
public sealed class PrivatePluginRequiresOrgException : ProblemDetailsException
{
    public override int StatusCode => 400;

    public PrivatePluginRequiresOrgException()
        : base("Private plugins must be associated with an organization (ownerOrgId is required).") { }
}
