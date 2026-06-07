namespace ClaudeForge.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the <c>org_audit_log</c> table.
/// Append-only audit trail for organization actions.
/// </summary>
public sealed class OrgAuditEntryEntity
{
    public Guid Id { get; set; }

    /// <summary>FK → organizations ON DELETE CASCADE.</summary>
    public Guid OrgId { get; set; }

    /// <summary>FK → users. The user who performed the action.</summary>
    public Guid ActorUserId { get; set; }

    /// <summary>Action identifier, e.g. "member.added", "invite.sent".</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Target descriptor, e.g. "user:alice@example.com".</summary>
    public string Target { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    // Navigation properties
    public OrganizationEntity Organization { get; set; } = null!;
    public UserEntity ActorUser { get; set; } = null!;
}
