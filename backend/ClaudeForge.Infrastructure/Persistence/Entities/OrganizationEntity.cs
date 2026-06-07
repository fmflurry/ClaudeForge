namespace ClaudeForge.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the <c>organizations</c> table.
/// </summary>
public sealed class OrganizationEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Lower-cased name for case-insensitive duplicate detection (UNIQUE constraint).
    /// Must equal <c>Name.ToLowerInvariant()</c>.
    /// </summary>
    public string NameNormalized { get; set; } = string.Empty;

    /// <summary>URL-friendly slug (UNIQUE constraint).</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>FK → users. The user who created this organization.</summary>
    public Guid CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    // Navigation properties
    public ICollection<OrganizationMemberEntity> Members { get; set; } = [];
    public ICollection<OrganizationInvitationEntity> Invitations { get; set; } = [];
    public ICollection<OrgAuditEntryEntity> AuditLog { get; set; } = [];
}
