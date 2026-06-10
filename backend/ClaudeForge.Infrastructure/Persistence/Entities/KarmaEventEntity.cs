namespace ClaudeForge.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the <c>karma_events</c> table.
/// Records reputation events that adjust an author's karma points.
/// </summary>
public sealed class KarmaEventEntity
{
    public Guid Id { get; set; }

    /// <summary>FK → author_reputation(author_id) ON DELETE CASCADE.</summary>
    public Guid AuthorId { get; set; }

    /// <summary>Event type, e.g. "plugin_passed", "plugin_failed", "appeal_approved".</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Number of karma points this event awards (positive or negative).</summary>
    public int Points { get; set; }

    /// <summary>Optional human-readable description of the event.</summary>
    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    // Navigation properties
    public AuthorReputationEntity Author { get; set; } = null!;
}
