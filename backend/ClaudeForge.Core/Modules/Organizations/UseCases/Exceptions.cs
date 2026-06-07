using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Core.Modules.Organizations.UseCases;

/// <summary>Thrown when a request requires authentication but the caller is anonymous.</summary>
public sealed class UnauthenticatedException : ProblemDetailsException
{
    public override int StatusCode => 401;

    public UnauthenticatedException()
        : base("Authentication is required.")
    {
    }
}

/// <summary>Thrown when an organization with the same normalized name already exists.</summary>
public sealed class DuplicateOrgNameException : ProblemDetailsException
{
    public override int StatusCode => 409;

    public DuplicateOrgNameException()
        : base("An organization with this name already exists.")
    {
    }
}

/// <summary>Thrown when the caller lacks the required role to perform the action.</summary>
public sealed class ForbiddenException : ProblemDetailsException
{
    public override int StatusCode => 403;

    public ForbiddenException()
        : base("You do not have permission to perform this action.")
    {
    }
}

/// <summary>Thrown when the requested member does not exist in the organization.</summary>
public sealed class MemberNotFoundException : ProblemDetailsException
{
    public override int StatusCode => 404;

    public MemberNotFoundException()
        : base("Member not found.")
    {
    }
}

/// <summary>Thrown when the sole owner of an organization attempts to remove themselves.</summary>
public sealed class SoleOwnerRemovalException : ProblemDetailsException
{
    public override int StatusCode => 400;

    public SoleOwnerRemovalException()
        : base("Cannot remove the sole owner of an organization.")
    {
    }
}

/// <summary>Thrown when trying to invite a user who is already a member of the organization.</summary>
public sealed class AlreadyMemberException : ProblemDetailsException
{
    public override int StatusCode => 409;

    public AlreadyMemberException()
        : base("The user is already a member of this organization.")
    {
    }
}

/// <summary>Thrown when a pending invitation already exists for the given email and organization.</summary>
public sealed class DuplicateInvitationException : ProblemDetailsException
{
    public override int StatusCode => 409;

    public DuplicateInvitationException()
        : base("A pending invitation already exists for this email address.")
    {
    }
}

/// <summary>Thrown when the requested invitation does not exist (or does not belong to the user).</summary>
public sealed class InvitationNotFoundException : ProblemDetailsException
{
    public override int StatusCode => 404;

    public InvitationNotFoundException()
        : base("Invitation not found.")
    {
    }
}

/// <summary>Thrown when an invitation is found but its status is not "pending".</summary>
public sealed class InvitationGoneException : ProblemDetailsException
{
    public override int StatusCode => 410;

    public InvitationGoneException()
        : base("This invitation is no longer valid.")
    {
    }
}
