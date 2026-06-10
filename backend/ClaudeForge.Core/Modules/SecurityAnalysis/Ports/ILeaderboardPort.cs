namespace ClaudeForge.Core.Modules.SecurityAnalysis.Ports;

/// <summary>Entry in a leaderboard.</summary>
public sealed record LeaderboardEntryDto(
    int Rank,
    Guid AuthorId,
    int KarmaPoints,
    int Level,
    int BadgeCount,
    string Period,
    Guid? OrgId);

/// <summary>
/// Port for leaderboard operations.
/// Implemented by an infrastructure adapter using EF Core.
/// </summary>
public interface ILeaderboardPort
{
    /// <summary>
    /// Returns the leaderboard for the given period and optional org scope.
    /// If no cached data exists, recalculates on demand.
    /// Default limit is 20.
    /// </summary>
    Task<IReadOnlyList<LeaderboardEntryDto>> GetLeaderboardAsync(
        string period, Guid? orgId, int limit = 20, CancellationToken ct = default);

    /// <summary>Recalculates and caches the leaderboard for the given period and org scope.</summary>
    Task RecalculateLeaderboardAsync(string period, Guid? orgId, CancellationToken ct = default);
}
