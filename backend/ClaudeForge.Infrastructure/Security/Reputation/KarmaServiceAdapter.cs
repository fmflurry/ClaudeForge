using System.Text.Json;
using ClaudeForge.Core.Modules.SecurityAnalysis.Ports;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClaudeForge.Infrastructure.Security.Reputation;

/// <summary>
/// EF Core adapter for <see cref="IKarmaServicePort"/>.
/// Backed by <see cref="MarketplaceDbContext"/>.
/// </summary>
public sealed class KarmaServiceAdapter : IKarmaServicePort
{
    private readonly MarketplaceDbContext _ctx;
    private readonly IBadgeServicePort _badgeService;
    private readonly ILogger<KarmaServiceAdapter> _logger;

    public KarmaServiceAdapter(
        MarketplaceDbContext ctx,
        IBadgeServicePort badgeService,
        ILogger<KarmaServiceAdapter> logger)
    {
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        _badgeService = badgeService ?? throw new ArgumentNullException(nameof(badgeService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Karma values per event type.
    /// </summary>
    private static class KarmaValues
    {
        public const int PluginSubmitted = 10;
        public const int AnalysisPassed = 50;
        public const int AnalysisFailed = -20;
        public const int AnalysisReview = 5;
        public const int AppealWon = 30;
        public const int AppealLost = -10;
        public const int AppealApproved = 20;
        public const int AutoApproved = 5;
        public const int BugBounty = 100;
    }

    /// <summary>Lookup karma value by event type string.</summary>
    private static int GetKarmaValue(string eventType) => eventType switch
    {
        "plugin_submitted" => KarmaValues.PluginSubmitted,
        "analysis_passed" => KarmaValues.AnalysisPassed,
        "analysis_failed" => KarmaValues.AnalysisFailed,
        "analysis_review" => KarmaValues.AnalysisReview,
        "appeal_won" => KarmaValues.AppealWon,
        "appeal_lost" => KarmaValues.AppealLost,
        "appeal_approved" => KarmaValues.AppealApproved,
        "auto_approved" => KarmaValues.AutoApproved,
        "bug_bounty" => KarmaValues.BugBounty,
        _ => 0,
    };

    public async Task AddKarmaAsync(Guid authorId, int points, string eventType, string description, CancellationToken ct = default)
    {
        // Load or create author reputation row
        AuthorReputationEntity? rep = await _ctx.AuthorReputations
            .FirstOrDefaultAsync(ar => ar.AuthorId == authorId, ct);

        if (rep is null)
        {
            rep = new AuthorReputationEntity
            {
                AuthorId = authorId,
                KarmaPoints = 0,
                Level = 1,
                Badges = "[]",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            _ctx.AuthorReputations.Add(rep);
        }

        // Apply points with minimum enforcement (>= 0)
        int newKarma = Math.Max(0, rep.KarmaPoints + points);
        rep.KarmaPoints = newKarma;

        // Calculate new level: floor(karma / 100) + 1, min 1
        int newLevel = Math.Max(1, (int)Math.Floor(newKarma / 100.0) + 1);
        rep.Level = newLevel;
        rep.UpdatedAt = DateTimeOffset.UtcNow;

        // Insert karma_event row
        var ev = new KarmaEventEntity
        {
            Id = Guid.NewGuid(),
            AuthorId = authorId,
            EventType = eventType,
            Points = points,
            Description = description,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _ctx.KarmaEvents.Add(ev);

        await _ctx.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Karma {EventType}: {Points} → author {AuthorId} (total: {Total}, level: {Level})",
            eventType, points, authorId, newKarma, newLevel);

        // Fire badge check after every karma event and handle badge-earned karma
        var newlyAwarded = await _badgeService.CheckAndAwardBadgesAsync(authorId, ct);
        foreach (string badgeName in newlyAwarded)
        {
            // Create karma event for badge earned using the same pattern as the main event
            var badgeEv = new KarmaEventEntity
            {
                Id = Guid.NewGuid(),
                AuthorId = authorId,
                EventType = "badge_earned",
                Points = 10,
                Description = $"Earned badge: {badgeName}",
                CreatedAt = DateTimeOffset.UtcNow,
            };
            _ctx.KarmaEvents.Add(badgeEv);

            // Update reputation points for badge karma too
            newKarma += 10;
        }
        // Recalculate level after badge karma additions
        newLevel = Math.Max(1, (int)Math.Floor(newKarma / 100.0) + 1);
        rep.Level = newLevel;
        rep.KarmaPoints = newKarma;
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task<KarmaSummary> GetKarmaAsync(Guid authorId, CancellationToken ct = default)
    {
        AuthorReputationEntity? rep = await _ctx.AuthorReputations
            .AsNoTracking()
            .FirstOrDefaultAsync(ar => ar.AuthorId == authorId, ct);

        if (rep is null)
            return new KarmaSummary(0, 1, []);

        List<string> badges;
        try
        {
            badges = JsonSerializer.Deserialize<List<string>>(rep.Badges) ?? [];
        }
        catch (JsonException)
        {
            badges = [];
        }

        return new KarmaSummary(rep.KarmaPoints, rep.Level, badges.AsReadOnly());
    }

    public async Task<IReadOnlyList<KarmaEventDto>> GetKarmaHistoryAsync(Guid authorId, CancellationToken ct = default)
    {
        var events = await _ctx.KarmaEvents
            .AsNoTracking()
            .Where(e => e.AuthorId == authorId)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new KarmaEventDto(
                e.EventType,
                e.Points,
                e.Description,
                e.CreatedAt))
            .ToListAsync(ct);

        return events.AsReadOnly();
    }
}
