namespace ClaudeForge.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the <c>leaderboard_cache</c> table.
/// Pre-computed leaderboard rankings by period, optionally scoped to an org.
/// </summary>
public sealed class LeaderboardCacheEntity
{
    public Guid Id { get; set; }

    /// <summary>Author identifier (maps to user GUID in the application layer).</summary>
    public Guid AuthorId { get; set; }

    public int KarmaPoints { get; set; }
    public int BadgeCount { get; set; }
    public int Rank { get; set; }

    /// <summary>Leaderboard period: "weekly" | "monthly" | "all_time".</summary>
    public string Period { get; set; } = "all_time";

    /// <summary>Optional org scope — null for global leaderboard.</summary>
    public Guid? OrgId { get; set; }

    public DateTimeOffset CalculatedAt { get; set; }
}
