namespace ClaudeForge.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the <c>organization_invitations</c> table.
/// Implements the invitation state machine: pending → accepted | revoked | expired.
/// A partial UNIQUE index enforces at most one pending invitation per (org, email).
/// </summary>
public sealed class OrganizationInvitationEntity
{
    public Guid Id { get; set; }

    /// <summary>FK → organizations ON DELETE CASCADE.</summary>
    public Guid OrgId { get; set; }

    /// <summary>Lower-cased email of the invited person.</summary>
    public string EmailNormalized { get; set; } = string.Empty;

    /// <summary>FK → users. The user who sent the invitation.</summary>
    public Guid InvitedBy { get; set; }

    /// <summary>Role to assign on accept: "owner" | "admin" | "member". Default "member".</summary>
    public string Role { get; set; } = "member";

    /// <summary>
    /// Current invitation status: "pending" | "accepted" | "revoked" | "expired".
    /// Default "pending".
    /// </summary>
    public string Status { get; set; } = "pending";

    /// <summary>Opaque one-time-use token (UNIQUE).</summary>
    public string Token { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    // Navigation properties
    public OrganizationEntity Organization { get; set; } = null!;
    public UserEntity InvitedByUser { get; set; } = null!;
}
