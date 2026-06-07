namespace ClaudeForge.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the <c>organization_members</c> table.
/// EF tracks by <see cref="Id"/> (surrogate UUID) while the database enforces
/// the semantic uniqueness constraint via UNIQUE(org_id, user_id).
/// This avoids EF's change-tracker identity-map conflict when two instances
/// with the same (OrgId, UserId) are added to the same context before SaveChanges.
/// </summary>
public sealed class OrganizationMemberEntity
{
    /// <summary>
    /// Surrogate PK for EF tracking purposes (auto-generated client-side).
    /// The semantic uniqueness is enforced by the DB UNIQUE(org_id, user_id) constraint.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>FK → organizations ON DELETE CASCADE.</summary>
    public Guid OrgId { get; set; }

    /// <summary>FK → users ON DELETE CASCADE.</summary>
    public Guid UserId { get; set; }

    /// <summary>Member role: "owner" | "admin" | "member".</summary>
    public string Role { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    // Navigation properties
    public OrganizationEntity Organization { get; set; } = null!;
    public UserEntity User { get; set; } = null!;
}
