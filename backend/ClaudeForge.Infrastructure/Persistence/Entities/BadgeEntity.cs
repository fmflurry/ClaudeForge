namespace ClaudeForge.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the <c>badges</c> table.
/// Defines available badges that authors can earn.
/// </summary>
public sealed class BadgeEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>URL-friendly slug. UNIQUE constraint.</summary>
    public string Slug { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Optional URL to the badge icon.</summary>
    public string? IconUrl { get; set; }

    /// <summary>JSONB object defining requirements for earning this badge.</summary>
    public string Requirements { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; }

    // Navigation properties
    public ICollection<AuthorBadgeEntity> AuthorBadges { get; set; } = [];
}
