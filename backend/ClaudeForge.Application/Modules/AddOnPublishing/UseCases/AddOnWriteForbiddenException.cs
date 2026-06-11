using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Application.Modules.AddOnPublishing.UseCases;

/// <summary>
/// Thrown when an authenticated caller attempts to publish/modify a plugin for an organization
/// they are not a member of. Maps to HTTP 403 Forbidden.
/// </summary>
public sealed class AddOnWriteForbiddenException : ProblemDetailsException
{
    public override int StatusCode => 403;

    public AddOnWriteForbiddenException()
        : base("You do not have permission to publish a plugin for this organization.") { }
}
