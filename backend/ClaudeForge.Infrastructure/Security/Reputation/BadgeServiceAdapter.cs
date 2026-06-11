using System.Globalization;
using System.Text.Json;
using ClaudeForge.Core.Modules.SecurityAnalysis.Ports;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClaudeForge.Infrastructure.Security.Reputation;

/// <summary>
/// EF Core adapter for <see cref="IBadgeServicePort"/>.
/// Backed by <see cref="MarketplaceDbContext"/>.
/// </summary>
public sealed class BadgeServiceAdapter : IBadgeServicePort
{
    private readonly MarketplaceDbContext _ctx;
    private readonly ILogger<BadgeServiceAdapter> _logger;

    public BadgeServiceAdapter(
        MarketplaceDbContext ctx,
        ILogger<BadgeServiceAdapter> logger)
    {
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ═════════════════════════════════════════════════════════════════════
    //  Badge seeding (only if badges table is empty)
    // ═════════════════════════════════════════════════════════════════════

    private static readonly BadgeSeedDef[] SeedBadges =
    [
        new("First Submission", "first-submission", "Submit your first plugin for analysis", 1, """{"submissionCount":1}"""),
        new("Quality Contributor", "quality-contributor", "Pass 5 security analyses", 2, """{"passedCount":5}"""),
        new("Security Expert", "security-expert", "Pass 10 security analyses", 3, """{"passedCount":10}"""),
        new("Appeal Winner", "appeal-winner", "Win 3 appeals", 2, """{"appealsWon":3}"""),
        new("Centurion", "centurion", "Submit 100 plugins for analysis", 4, """{"submissionCount":100}"""),
        new("Pioneer", "pioneer", "Be among the first submitters", 1, """{"isFirstSubmitter":true}"""),
        new("Bug Hunter", "bug-hunter", "Report 5 bugs", 2, """{"bugsReported":5}"""),
        new("Perfect Score", "perfect-score", "Achieve a perfect 100.00 analysis score", 3, """{"perfectScores":1}"""),
        new("Veteran", "veteran", "Be active for over a year", 3, """{"accountAgeDays":365}"""),
        new("Popular", "popular", "Plugin exceeds 1,000 downloads", 2, """{"downloads":1000}"""),
    ];

    private sealed record BadgeSeedDef(string Name, string Slug, string Description, int Tier, string Requirements);

    /// <summary>Author stats used for badge requirement checking.</summary>
    private sealed record AuthorStats(
        int SubmissionCount,
        int PassedCount,
        int FailedCount,
        int AppealsWon,
        int AppealsLost,
        int PerfectScores,
        int BugsReported,
        int Downloads,
        double AccountAgeDays,
        bool IsFirstSubmitter);

    /// <summary>Ensure badge definitions exist in the DB.</summary>
    private async Task EnsureBadgesSeededAsync(CancellationToken ct)
    {
        bool hasAny = await _ctx.Badges.AnyAsync(ct);
        if (hasAny)
            return;

        foreach (BadgeSeedDef def in SeedBadges)
        {
            _ctx.Badges.Add(new BadgeEntity
            {
                Id = Guid.NewGuid(),
                Name = def.Name,
                Slug = def.Slug,
                Description = def.Description,
                Requirements = def.Requirements,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }

        await _ctx.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} badge definitions", SeedBadges.Length);
    }

    public async Task<IReadOnlyList<string>> CheckAndAwardBadgesAsync(Guid authorId, CancellationToken ct = default)
    {
        await EnsureBadgesSeededAsync(ct);

        // Load all badge definitions
        List<BadgeEntity> allBadges = await _ctx.Badges.AsNoTracking().ToListAsync(ct);

        // Load author's current earned badge IDs
        HashSet<Guid> earnedBadgeIds = await _ctx.AuthorBadges
            .AsNoTracking()
            .Where(ab => ab.AuthorId == authorId)
            .Select(ab => ab.BadgeId)
            .ToHashSetAsync(ct);

        // Compute author stats from DB
        AuthorStats stats = await ComputeAuthorStatsAsync(authorId, ct);

        // Get or create author reputation row
        AuthorReputationEntity? rep = await _ctx.AuthorReputations
            .FirstOrDefaultAsync(ar => ar.AuthorId == authorId, ct);

        List<string> currentBadgeNames;
        try
        {
            currentBadgeNames = rep is not null
                ? JsonSerializer.Deserialize<List<string>>(rep.Badges) ?? []
                : [];
        }
        catch (JsonException)
        {
            currentBadgeNames = [];
        }

        bool changed = false;
        var awardedBadgeNames = new List<string>();

        foreach (BadgeEntity badge in allBadges)
        {
            if (earnedBadgeIds.Contains(badge.Id))
                continue; // Already earned

            if (!MeetsRequirements(badge.Requirements, stats))
                continue;

            // Award the badge
            _ctx.AuthorBadges.Add(new AuthorBadgeEntity
            {
                Id = Guid.NewGuid(),
                AuthorId = authorId,
                BadgeId = badge.Id,
                AwardedAt = DateTimeOffset.UtcNow,
            });

            string badgeName = badge.Name;
            if (!currentBadgeNames.Contains(badgeName))
                currentBadgeNames.Add(badgeName);

            changed = true;

            _logger.LogInformation(
                "Badge awarded: '{Badge}' to author {AuthorId}",
                badgeName, authorId);

            awardedBadgeNames.Add(badgeName);
        }

        if (changed && rep is not null)
        {
            // Persist badge change
            rep.Badges = JsonSerializer.Serialize(currentBadgeNames);
            rep.UpdatedAt = DateTimeOffset.UtcNow;

            await _ctx.SaveChangesAsync(ct);
        }
        else if (changed)
        {
            await _ctx.SaveChangesAsync(ct);
        }

        return awardedBadgeNames.AsReadOnly();
    }

    public async Task<IReadOnlyList<AuthorBadgeDto>> GetAuthorBadgesAsync(Guid authorId, CancellationToken ct = default)
    {
        var badges = await _ctx.AuthorBadges
            .AsNoTracking()
            .Where(ab => ab.AuthorId == authorId)
            .Join(_ctx.Badges.AsNoTracking(),
                ab => ab.BadgeId,
                b => b.Id,
                (ab, b) => new AuthorBadgeDto(
                    b.Id,
                    b.Name,
                    b.Slug,
                    b.Description,
                    b.IconUrl,
                    ab.AwardedAt))
            .ToListAsync(ct);

        return badges.AsReadOnly();
    }

    public async Task<IReadOnlyList<BadgeDefinitionDto>> GetAllBadgesAsync(CancellationToken ct = default)
    {
        await EnsureBadgesSeededAsync(ct);

        var badges = await _ctx.Badges
            .AsNoTracking()
            .Select(b => new BadgeDefinitionDto(
                b.Id,
                b.Name,
                b.Slug,
                b.Description,
                b.IconUrl,
                b.Requirements,
                0, // Tier not stored in current entity schema
                b.CreatedAt))
            .ToListAsync(ct);

        // The BadgeEntity currently has no Tier column. Match by slug from seed data.
        var slugToTier = SeedBadges.ToDictionary(s => s.Slug, s => s.Tier);

        var result = badges.Select(b => b with
        {
            // ReSharper disable once UsageOfDefaultStructEquality — Tier is 0 for non-matched
            Tier = slugToTier.GetValueOrDefault(b.Slug, 0)
        }).ToList();

        return result.AsReadOnly();
    }

    // ═════════════════════════════════════════════════════════════════════
    //  Helpers
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>Computes aggregate stats for an author from plugins + analysis + appeals.</summary>
    private async Task<AuthorStats> ComputeAuthorStatsAsync(Guid authorId, CancellationToken ct)
    {
        // Find all plugins owned by this author
        List<AddOnEntity> authorPlugins = await _ctx.Plugins
            .AsNoTracking()
            .Where(p => p.OwnerUserId == authorId)
            .ToListAsync(ct);

        List<Guid> pluginIds = authorPlugins.Select(p => p.Id).ToList();

        // Submission count = analysis_results for author's plugins
        int submissionCount = await _ctx.AnalysisResults
            .AsNoTracking()
            .CountAsync(ar => pluginIds.Contains(ar.PluginId), ct);

        // Passed / failed counts
        int passedCount = await _ctx.AnalysisResults
            .AsNoTracking()
            .CountAsync(ar => pluginIds.Contains(ar.PluginId) && ar.Status == "passed", ct);

        int failedCount = await _ctx.AnalysisResults
            .AsNoTracking()
            .CountAsync(ar => pluginIds.Contains(ar.PluginId) && ar.Status == "failed", ct);

        // Perfect scores
        int perfectScores = await _ctx.AnalysisResults
            .AsNoTracking()
            .CountAsync(ar => pluginIds.Contains(ar.PluginId) && ar.TotalScore == 100.00m, ct);

        // Total downloads across all plugins
        int downloads = authorPlugins.Sum(p => (int)p.DownloadCount);

        // Appeals won/lost
        int appealsWon = await _ctx.Appeals
            .AsNoTracking()
            .CountAsync(a => a.AuthorId == authorId && a.Status == "approved", ct);

        int appealsLost = await _ctx.Appeals
            .AsNoTracking()
            .CountAsync(a => a.AuthorId == authorId && a.Status == "rejected", ct);

        // Bugs reported — count failed analyses as "bugs reported" proxy
        int bugsReported = failedCount;

        // Account age — oldest analysis result for author
        DateTimeOffset? oldestResult = await _ctx.AnalysisResults
            .AsNoTracking()
            .Where(ar => pluginIds.Contains(ar.PluginId))
            .OrderBy(ar => ar.CreatedAt)
            .Select(ar => (DateTimeOffset?)ar.CreatedAt)
            .FirstOrDefaultAsync(ct);

        double accountAgeDays = oldestResult.HasValue
            ? (DateTimeOffset.UtcNow - oldestResult.Value).TotalDays
            : 0;

        // First submitter — only one analysis result exists for author overall
        int totalAuthorResults = await _ctx.AnalysisResults
            .AsNoTracking()
            .CountAsync(ar => pluginIds.Contains(ar.PluginId), ct);
        bool isFirstSubmitter = totalAuthorResults == 1;

        return new AuthorStats(
            submissionCount,
            passedCount,
            failedCount,
            appealsWon,
            appealsLost,
            perfectScores,
            bugsReported,
            downloads,
            accountAgeDays,
            isFirstSubmitter);
    }

    /// <summary>Checks if a JSON requirements object is satisfied by the author's stats.</summary>
    private static bool MeetsRequirements(string requirementsJson, AuthorStats stats)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(requirementsJson);
            JsonElement root = doc.RootElement;

            foreach (JsonProperty prop in root.EnumerateObject())
            {
                long requiredValue = prop.Value.GetInt64();

                bool meets = prop.Name switch
                {
                    "submissionCount" => stats.SubmissionCount >= requiredValue,
                    "passedCount" => stats.PassedCount >= requiredValue,
                    "failedCount" => stats.FailedCount >= requiredValue,
                    "appealsWon" => stats.AppealsWon >= requiredValue,
                    "appealsLost" => stats.AppealsLost >= requiredValue,
                    "perfectScores" => stats.PerfectScores >= requiredValue,
                    "bugsReported" => stats.BugsReported >= requiredValue,
                    "downloads" => stats.Downloads >= requiredValue,
                    "accountAgeDays" => stats.AccountAgeDays >= requiredValue,
                    "isFirstSubmitter" => stats.IsFirstSubmitter == (requiredValue != 0),
                    _ => false,
                };

                if (!meets)
                    return false;
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
