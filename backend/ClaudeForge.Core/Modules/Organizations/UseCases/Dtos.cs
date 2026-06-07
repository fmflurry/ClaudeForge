using ClaudeForge.Core.Identity;

namespace ClaudeForge.Core.Modules.Organizations.UseCases;

/// <summary>
/// Immutable DTO representing a full organization record.
/// </summary>
public sealed record OrganizationDto(
    Guid Id,
    string Name,
    string NameNormalized,
    string Slug,
    Guid CreatedBy,
    DateTimeOffset CreatedAt);

/// <summary>
/// Immutable DTO for a user's view of an organization (list context).
/// </summary>
public sealed record OrgSummaryDto(
    Guid Id,
    string Name,
    string Slug,
    OrgRole UserRole);

/// <summary>
/// Immutable DTO for a member of an organization.
/// </summary>
public sealed record MemberDto(
    Guid UserId,
    string Email,
    string DisplayName,
    OrgRole Role,
    DateTimeOffset JoinedAt);

/// <summary>
/// Immutable DTO for an organization invitation.
/// </summary>
public sealed record InvitationDto(
    Guid Id,
    Guid OrgId,
    string EmailNormalized,
    Guid InvitedBy,
    OrgRole Role,
    string Status,
    string Token,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset? RevokedAt);

/// <summary>
/// Command to create a new organization.
/// </summary>
public sealed record CreateOrganizationCommand(
    string Name,
    string? Slug);

/// <summary>
/// Record passed to the organization store to persist a new organization.
/// </summary>
public sealed record CreateOrganizationRecord(
    Guid Id,
    string Name,
    string NameNormalized,
    string Slug,
    Guid CreatedBy,
    DateTimeOffset CreatedAt);

/// <summary>
/// Record passed to the invitation store to persist a new invitation.
/// </summary>
public sealed record CreateInvitationRecord(
    Guid Id,
    Guid OrgId,
    string EmailNormalized,
    Guid InvitedBy,
    OrgRole Role,
    string Token,
    DateTimeOffset ExpiresAt);
