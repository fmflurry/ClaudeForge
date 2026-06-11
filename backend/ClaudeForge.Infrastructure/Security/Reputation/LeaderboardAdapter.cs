using ClaudeForge.Core.Modules.SecurityAnalysis.Ports;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClaudeForge.Infrastructure.Security.Reputation;

/// <summary>
/// EF Core adapter for <see cref="ILeaderboardPort"/>.
/// Backed by <see cref="MarketplaceDbContext"/>.
/// </summary>
public sealed class LeaderboardAdapter : ILeaderboardPort
{
    private readonly MarketplaceDbContext _ctx;
    private readonly ILogger<LeaderboardAdapter> _logger;

    /// <summary>Cache staleness threshold: 15 minutes.</summary>
    private static readonly TimeSpan CacheMaxAge = TimeSpan.FromMinutes(15);

    public LeaderboardAdapter(MarketplaceDbContext ctx, ILogger<LeaderboardAdapter> logger)
    {
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<LeaderboardEntryDto>> GetLeaderboardAsync(
        string period, Guid? orgId, int limit = 20, CancellationToken ct = default)
    {
        // Validate period
        period = NormalizePeriod(period);

        // Check cache freshness
        bool cacheStale = await IsCacheStaleAsync(period, orgId, ct);

        if (cacheStale)
        {
            await RecalculateLeaderboardAsync(period, orgId, ct);
        }

        var entries = await _ctx.LeaderboardCache
            .AsNoTracking()
            .Where(lc => lc.Period == period && lc.OrgId == orgId)
            .OrderBy(lc => lc.Rank)
            .Take(limit)
            .Select(lc => new LeaderboardEntryDto(
                lc.Rank,
                lc.AuthorId,
                lc.KarmaPoints,
                // Compute level: floor(karma/100) + 1, min 1
                lc.KarmaPoints > 0 ? (int)Math.Floor(lc.KarmaPoints / 100.0) + 1 : 1,
                lc.BadgeCount,
                lc.Period,
                lc.OrgId))
            .ToListAsync(ct);

        return entries.AsReadOnly();
    }

    public async Task RecalculateLeaderboardAsync(string period, Guid? orgId, CancellationToken ct = default)
    {
        period = NormalizePeriod(period);

        // Remove existing cache entries for this period + org
        var existing = await _ctx.LeaderboardCache
            .Where(lc => lc.Period == period && lc.OrgId == orgId)
            .ToListAsync(ct);

        _ctx.LeaderboardCache.RemoveRange(existing);

        // Get author reputations
        IQueryable<AuthorReputationEntity> repQuery = _ctx.AuthorReputations.AsNoTracking();

        if (period is "weekly" or "monthly")
        {
            // For time-based leaderboards, filter karma events within the window
            DateTimeOffset cutoff = period switch
            {
                "weekly" => DateTimeOffset.UtcNow.AddDays(-7),
                "monthly" => DateTimeOffset.UtcNow.AddMonths(-1),
                _ => DateTimeOffset.MinValue,
            };

            // Get total karma gained per author within the window
            var karmaInWindow = await _ctx.KarmaEvents
                .AsNoTracking()
                .Where(e => e.CreatedAt >= cutoff)
                .GroupBy(e => e.AuthorId)
                .Select(g => new
                {
                    AuthorId = g.Key,
                    WindowKarma = g.Sum(e => (int?)e.Points) ?? 0,
                })
                .ToListAsync(ct);

            // Also need badge count per author
            var badgeCounts = await _ctx.AuthorBadges
                .AsNoTracking()
                .GroupBy(ab => ab.AuthorId)
                .Select(g => new { AuthorId = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var badgeCountMap = badgeCounts.ToDictionary(b => b.AuthorId, b => b.Count);

            // Sort by window karma DESC and assign ranks
            var ranked = karmaInWindow
                .OrderByDescending(k => k.WindowKarma)
                .Select((k, index) => new LeaderboardCacheEntity
                {
                    Id = Guid.NewGuid(),
                    AuthorId = k.AuthorId,
                    KarmaPoints = Math.Max(0, k.WindowKarma),
                    BadgeCount = badgeCountMap.GetValueOrDefault(k.AuthorId, 0),
                    Rank = index + 1,
                    Period = period,
                    OrgId = orgId,
                    CalculatedAt = DateTimeOffset.UtcNow,
                })
                .ToList();

            _ctx.LeaderboardCache.AddRange(ranked);
        }
        else
        {
            // "all_time" — use total karma points from author_reputation
            var badgeCounts = await _ctx.AuthorBadges
                .AsNoTracking()
                .GroupBy(ab => ab.AuthorId)
                .Select(g => new { AuthorId = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var badgeCountMap = badgeCounts.ToDictionary(b => b.AuthorId, b => b.Count);

            // Apply org filter if specified — join org_members
            IQueryable<AuthorReputationEntity> filteredReps = repQuery;

            if (orgId.HasValue && orgId.Value != Guid.Empty)
            {
                filteredReps = from rep in repQuery
                               join member in _ctx.OrganizationMembers.AsNoTracking()
                                   on rep.AuthorId equals member.UserId
                               where member.OrgId == orgId.Value
                               select rep;
            }

            var allReps = await filteredReps
                .OrderByDescending(ar => ar.KarmaPoints)
                .ToListAsync(ct);

            var ranked = allReps
                .Select((rep, index) => new LeaderboardCacheEntity
                {
                    Id = Guid.NewGuid(),
                    AuthorId = rep.AuthorId,
                    KarmaPoints = rep.KarmaPoints,
                    BadgeCount = badgeCountMap.GetValueOrDefault(rep.AuthorId, 0),
                    Rank = index + 1,
                    Period = period,
                    OrgId = orgId,
                    CalculatedAt = DateTimeOffset.UtcNow,
                })
                .ToList();

            _ctx.LeaderboardCache.AddRange(ranked);
        }

        await _ctx.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Leaderboard recalculated: period={Period}, orgId={OrgId}",
            period, orgId);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  Helpers
    // ═════════════════════════════════════════════════════════════════════

    private static string NormalizePeriod(string period) => period?.ToLowerInvariant() switch
    {
        "weekly" => "weekly",
        "monthly" => "monthly",
        "all_time" or "all-time" or "alltime" => "all_time",
        _ => "all_time",
    };

    private async Task<bool> IsCacheStaleAsync(string period, Guid? orgId, CancellationToken ct)
    {
        DateTimeOffset? latest = await _ctx.LeaderboardCache
            .AsNoTracking()
            .Where(lc => lc.Period == period && lc.OrgId == orgId)
            .MaxAsync(lc => (DateTimeOffset?)lc.CalculatedAt, ct);

        if (latest is null)
            return true; // No cache exists

        return DateTimeOffset.UtcNow - latest.Value > CacheMaxAge;
    }
}
