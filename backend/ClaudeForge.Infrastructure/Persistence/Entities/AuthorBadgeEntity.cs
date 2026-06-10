namespace ClaudeForge.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the <c>author_badges</c> table.
/// Join table linking authors to earned badges. UNIQUE(author_id, badge_id).
/// </summary>
public sealed class AuthorBadgeEntity
{
    public Guid Id { get; set; }

    /// <summary>FK → author_reputation(author_id) — not an explicit DB FK.</summary>
    public Guid AuthorId { get; set; }

    /// <summary>FK → badges ON DELETE CASCADE.</summary>
    public Guid BadgeId { get; set; }

    /// <summary>When the badge was awarded to the author.</summary>
    public DateTimeOffset AwardedAt { get; set; }

    // Navigation properties
    public BadgeEntity Badge { get; set; } = null!;
}
