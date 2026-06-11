using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Application.Modules.AddOnPublishing.UseCases;

/// <summary>
/// Thrown when a caller attempts to publish a private plugin without specifying an ownerOrgId.
/// Maps to HTTP 400 Bad Request.
/// </summary>
public sealed class PrivateAddOnRequiresOrgException : ProblemDetailsException
{
    public override int StatusCode => 400;

    public PrivateAddOnRequiresOrgException()
        : base("Private plugins must be associated with an organization (ownerOrgId is required).") { }
}
