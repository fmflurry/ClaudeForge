namespace ClaudeForge.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the <c>users</c> table.
/// Mutable to allow EF change tracking; immutability lives at the domain layer.
/// </summary>
public sealed class UserEntity
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Lower-cased email for case-insensitive duplicate detection (UNIQUE constraint).
    /// NULL for users whose email was not verified at sign-in time — a NULL value
    /// can never be a cross-provider link target and satisfies the UNIQUE index
    /// because Postgres permits multiple NULLs in a unique index.
    /// </summary>
    public string? EmailNormalized { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Soft-delete timestamp. Null means the user is active.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public ICollection<UserIdentityEntity> Identities { get; set; } = [];
    public ICollection<OrganizationMemberEntity> Memberships { get; set; } = [];
    public ICollection<OrganizationInvitationEntity> SentInvitations { get; set; } = [];
    public ICollection<RefreshTokenEntity> RefreshTokens { get; set; } = [];
}
