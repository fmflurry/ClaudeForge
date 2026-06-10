using System.Text.Json;
using ClaudeForge.Core.Modules.SecurityAnalysis.Ports;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using ClaudeForge.Infrastructure.Security.Reputation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace ClaudeForge.Tests.Unit.SecurityAnalysis;

/// <summary>
/// Tests for the reputation system: karma, levels, badges, leaderboard.
///
/// Uses InMemory EF Core for KarmaServiceAdapter/BadgeServiceAdapter/LeaderboardAdapter
/// with mocked cross-dependencies (IBadgeServicePort, IKarmaServicePort, ILogger).
/// </summary>
public sealed class ReputationTests : IDisposable
{
    private readonly MarketplaceDbContext _db;
    private readonly ILogger<KarmaServiceAdapter> _karmaLogger = Substitute.For<ILogger<KarmaServiceAdapter>>();
    private readonly ILogger<BadgeServiceAdapter> _badgeLogger = Substitute.For<ILogger<BadgeServiceAdapter>>();
    private readonly ILogger<LeaderboardAdapter> _leaderboardLogger = Substitute.For<ILogger<LeaderboardAdapter>>();

    public ReputationTests()
    {
        DbContextOptions<MarketplaceDbContext> options = new DbContextOptionsBuilder<MarketplaceDbContext>()
            .UseInMemoryDatabase($"ReputationTests_{Guid.NewGuid():N}")
            .Options;

        _db = new MarketplaceDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private KarmaServiceAdapter CreateKarmaService(IBadgeServicePort? badgeService = null)
    {
        badgeService ??= Substitute.For<IBadgeServicePort>();
        return new KarmaServiceAdapter(_db, badgeService, _karmaLogger);
    }

    private BadgeServiceAdapter CreateBadgeService()
    {
        return new BadgeServiceAdapter(_db, _badgeLogger);
    }

    private LeaderboardAdapter CreateLeaderboardAdapter() =>
        new(_db, _leaderboardLogger);

    /// <summary>Creates a plugin owned by a specific user, with analysis results.</summary>
    private PluginEntity MakeOwnedPlugin(Guid userId, long downloads = 0)
    {
        Guid id = Guid.NewGuid();
        string shortId = id.ToString("N")[..6];
        string slugId = id.ToString("N")[..8];
        var plugin = new PluginEntity
        {
            Id = id,
            Name = $"Plugin-{shortId}",
            NameNormalized = $"plugin-{shortId}",
            Slug = $"plugin-{slugId}",
            Description = "Test plugin",
            Author = userId.ToString(),
            OwnerUserId = userId,
            DownloadCount = downloads,
            SecurityStatus = "passed",
        };
        return plugin;
    }

    // ═════════════════════════════════════════════════════════════════════
    //  T.5: Karma minimum is 0 (can't go negative)
    // ═════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AddKarma_WhenNegativeWouldResultBelowZero_KarmaStaysAtZero()
    {
        // Arrange
        IBadgeServicePort badgeMock = Substitute.For<IBadgeServicePort>();
        KarmaServiceAdapter svc = CreateKarmaService(badgeMock);
        Guid authorId = Guid.NewGuid();

        // Act — add small positive first, then large negative
        await svc.AddKarmaAsync(authorId, 20, "analysis_passed", "Passed analysis");
        await svc.AddKarmaAsync(authorId, -50, "analysis_failed", "Failed analysis");

        // Assert — karma = max(0, 20 + (-50)) = max(0, -30) = 0
        KarmaSummary summary = await svc.GetKarmaAsync(authorId);
        Assert.Equal(0, summary.KarmaPoints);
    }

    [Fact]
    public async Task AddKarma_NegativeWhenAlreadyZero_StaysAtZero()
    {
        // Arrange
        KarmaServiceAdapter svc = CreateKarmaService();
        Guid authorId = Guid.NewGuid();

        // Act — add negative to zero-balance
        await svc.AddKarmaAsync(authorId, -10, "appeal_lost", "Lost appeal");

        // Assert — stays at 0
        KarmaSummary summary = await svc.GetKarmaAsync(authorId);
        Assert.Equal(0, summary.KarmaPoints);
    }

    [Fact]
    public async Task AddKarma_PositiveFromZero_KarmaIncreases()
    {
        KarmaServiceAdapter svc = CreateKarmaService();
        Guid authorId = Guid.NewGuid();

        await svc.AddKarmaAsync(authorId, 50, "analysis_passed", "Passed");

        KarmaSummary summary = await svc.GetKarmaAsync(authorId);
        Assert.Equal(50, summary.KarmaPoints);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  T.5: Level is floor(karma / 100) + 1
    // ═════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(0, 1)]
    [InlineData(50, 1)]
    [InlineData(99, 1)]
    [InlineData(100, 2)]
    [InlineData(150, 2)]
    [InlineData(199, 2)]
    [InlineData(200, 3)]
    [InlineData(999, 10)]
    [InlineData(1000, 11)]
    public async Task AddKarma_KarmaLevel_CalculatesCorrectly(int finalKarma, int expectedLevel)
    {
        // Arrange
        KarmaServiceAdapter svc = CreateKarmaService();
        Guid authorId = Guid.NewGuid();

        // Build up karma in steps
        int remaining = finalKarma;
        while (remaining > 0)
        {
            int step = Math.Min(remaining, 50);
            await svc.AddKarmaAsync(authorId, step, "analysis_passed", "Step");
            remaining -= step;
        }

        // Act
        KarmaSummary summary = await svc.GetKarmaAsync(authorId);

        // Assert
        Assert.Equal(expectedLevel, summary.Level);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  T.5: Sufficient karma triggers auto-approval (karma >= 200)
    // ═════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetKarma_At200Karma_Level3AndSufficientForAutoApproval()
    {
        // Arrange
        KarmaServiceAdapter svc = CreateKarmaService();
        Guid authorId = Guid.NewGuid();

        // Build to exactly 200 karma
        for (int i = 0; i < 4; i++)
            await svc.AddKarmaAsync(authorId, 50, "analysis_passed", "Passed analysis");

        // Act
        KarmaSummary summary = await svc.GetKarmaAsync(authorId);

        // Assert — 200 karma = level 3, sufficient for auto-approval
        const int autoApproveThreshold = 200;
        Assert.Equal(200, summary.KarmaPoints);
        Assert.Equal(3, summary.Level);
        Assert.True(summary.KarmaPoints >= autoApproveThreshold);
    }

    [Fact]
    public async Task GetKarma_Below200_NotEnoughForAutoApproval()
    {
        KarmaServiceAdapter svc = CreateKarmaService();
        Guid authorId = Guid.NewGuid();

        await svc.AddKarmaAsync(authorId, 150, "analysis_passed", "Passed analysis");

        KarmaSummary summary = await svc.GetKarmaAsync(authorId);
        const int autoApproveThreshold = 200;
        Assert.False(summary.KarmaPoints >= autoApproveThreshold);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  T.5: Badge not awarded multiple times for same criteria
    // ═════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CheckAndAwardBadges_BadgeAlreadyEarned_NotAwardedAgain()
    {
        // Arrange
        BadgeServiceAdapter svc = CreateBadgeService();
        Guid authorId = Guid.NewGuid();

        // Seed a badge definition
        _db.Badges.Add(new BadgeEntity
        {
            Id = Guid.NewGuid(),
            Name = "First Submission",
            Slug = "first-submission",
            Description = "Submit your first plugin",
            Requirements = """{"submissionCount":1}""",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();

        // Create a plugin owned by the author with at least 1 analysis result
        PluginEntity plugin = MakeOwnedPlugin(authorId);
        _db.Plugins.Add(plugin);
        _db.AnalysisResults.Add(new AnalysisResultEntity
        {
            Id = Guid.NewGuid(),
            PluginId = plugin.Id,
            PluginVersion = "1.0.0",
            TotalScore = 90m,
            Status = "passed",
        });
        await _db.SaveChangesAsync();

        // Manually award the badge already
        BadgeEntity badge = await _db.Badges.FirstAsync();
        _db.AuthorBadges.Add(new AuthorBadgeEntity
        {
            Id = Guid.NewGuid(),
            AuthorId = authorId,
            BadgeId = badge.Id,
            AwardedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();

        // Act — check badges again
        await svc.CheckAndAwardBadgesAsync(authorId);

        // Assert — still only 1 AuthorBadge entry (no duplicate)
        int count = await _db.AuthorBadges.CountAsync(ab => ab.AuthorId == authorId);
        Assert.Equal(1, count);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  T.5: Badge_earned event fires karma +10
    // ═════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AddKarmaAsync_WhenBadgeEarned_Awards10Karma()
    {
        // Arrange — use real adapters so badge_earned karma flows through
        BadgeServiceAdapter badgeSvc = CreateBadgeService();
        KarmaServiceAdapter karmaSvc = new(_db, badgeSvc, _karmaLogger);
        Guid authorId = Guid.NewGuid();

        // Seed badge definition
        _db.Badges.Add(new BadgeEntity
        {
            Id = Guid.NewGuid(),
            Name = "First Submission",
            Slug = "first-submission",
            Description = "Submit your first plugin",
            Requirements = """{"submissionCount":1}""",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();

        // Create a plugin owned by author with 1 analysis result
        PluginEntity plugin = MakeOwnedPlugin(authorId);
        _db.Plugins.Add(plugin);
        _db.AnalysisResults.Add(new AnalysisResultEntity
        {
            Id = Guid.NewGuid(),
            PluginId = plugin.Id,
            PluginVersion = "1.0.0",
            TotalScore = 90m,
            Status = "passed",
        });
        await _db.SaveChangesAsync();

        // Act — add karma which triggers badge check and badge_earned karma
        await karmaSvc.AddKarmaAsync(authorId, 50, "analysis_passed", "Passed analysis");

        // Assert — 50 (main event) + 10 (badge_earned) = 60 karma
        KarmaSummary summary = await karmaSvc.GetKarmaAsync(authorId);
        Assert.Equal(60, summary.KarmaPoints);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  T.5: Leaderboard ordering (highest karma first)
    // ═════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Leaderboard_AllTime_HighestKarmaFirst()
    {
        // Arrange
        LeaderboardAdapter lb = CreateLeaderboardAdapter();

        // Create authors with different karma
        var authorHigh = Guid.NewGuid();
        var authorMid = Guid.NewGuid();
        var authorLow = Guid.NewGuid();

        _db.AuthorReputations.AddRange(
            new AuthorReputationEntity { AuthorId = authorHigh, KarmaPoints = 300, Level = 4, Badges = "[]", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new AuthorReputationEntity { AuthorId = authorMid, KarmaPoints = 150, Level = 2, Badges = "[]", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new AuthorReputationEntity { AuthorId = authorLow, KarmaPoints = 50, Level = 1, Badges = "[]", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync();

        // Act
        IReadOnlyList<LeaderboardEntryDto> result = await lb.GetLeaderboardAsync("all_time", null, limit: 20);

        // Assert — highest karma first
        Assert.NotEmpty(result);
        Assert.Equal(authorHigh, result[0].AuthorId);
        Assert.Equal(300, result[0].KarmaPoints);
        Assert.Equal(1, result[0].Rank);

        Assert.Equal(authorMid, result[1].AuthorId);
        Assert.Equal(150, result[1].KarmaPoints);
        Assert.Equal(2, result[1].Rank);

        Assert.Equal(authorLow, result[2].AuthorId);
        Assert.Equal(50, result[2].KarmaPoints);
        Assert.Equal(3, result[2].Rank);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  T.5: Leaderboard org scoping
    // ═════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Leaderboard_OrgScoped_OnlyShowsOrgMembers()
    {
        // Arrange
        LeaderboardAdapter lb = CreateLeaderboardAdapter();
        Guid orgId = Guid.NewGuid();
        Guid otherOrgId = Guid.NewGuid();

        var authorInOrg = Guid.NewGuid();
        var authorOtherOrg = Guid.NewGuid();
        var authorNoOrg = Guid.NewGuid();

        // Create orgs
        _db.Organizations.AddRange(
            new OrganizationEntity { Id = orgId, Name = "Org1", NameNormalized = "org1", Slug = "org1", CreatedBy = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow },
            new OrganizationEntity { Id = otherOrgId, Name = "Org2", NameNormalized = "org2", Slug = "org2", CreatedBy = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync();

        // Add members to orgs
        _db.OrganizationMembers.AddRange(
            new OrganizationMemberEntity { Id = Guid.NewGuid(), OrgId = orgId, UserId = authorInOrg, Role = "member", CreatedAt = DateTimeOffset.UtcNow },
            new OrganizationMemberEntity { Id = Guid.NewGuid(), OrgId = otherOrgId, UserId = authorOtherOrg, Role = "member", CreatedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync();

        // Create reputations
        _db.AuthorReputations.AddRange(
            new AuthorReputationEntity { AuthorId = authorInOrg, KarmaPoints = 200, Level = 3, Badges = "[]", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new AuthorReputationEntity { AuthorId = authorOtherOrg, KarmaPoints = 300, Level = 4, Badges = "[]", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new AuthorReputationEntity { AuthorId = authorNoOrg, KarmaPoints = 100, Level = 2, Badges = "[]", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync();

        // Act — leaderboard scoped to org1
        IReadOnlyList<LeaderboardEntryDto> result = await lb.GetLeaderboardAsync("all_time", orgId, limit: 20);

        // Assert — only authorInOrg appears
        Assert.Single(result);
        Assert.Equal(authorInOrg, result[0].AuthorId);
        Assert.Equal(200, result[0].KarmaPoints);

        // Verify scoped to other org
        IReadOnlyList<LeaderboardEntryDto> otherResult = await lb.GetLeaderboardAsync("all_time", otherOrgId, limit: 20);
        Assert.Single(otherResult);
        Assert.Equal(authorOtherOrg, otherResult[0].AuthorId);
    }

    [Fact]
    public async Task Leaderboard_NullOrg_ShowsAllAuthors()
    {
        // Arrange
        LeaderboardAdapter lb = CreateLeaderboardAdapter();

        var author1 = Guid.NewGuid();
        var author2 = Guid.NewGuid();

        _db.AuthorReputations.AddRange(
            new AuthorReputationEntity { AuthorId = author1, KarmaPoints = 100, Level = 2, Badges = "[]", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new AuthorReputationEntity { AuthorId = author2, KarmaPoints = 50, Level = 1, Badges = "[]", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync();

        // Act
        IReadOnlyList<LeaderboardEntryDto> result = await lb.GetLeaderboardAsync("all_time", null, limit: 20);

        // Assert — all authors, ranked
        Assert.Equal(2, result.Count);
        Assert.Equal(author1, result[0].AuthorId);
        Assert.Equal(author2, result[1].AuthorId);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  Additional: Karma event recording
    // ═════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AddKarma_EventRecorded_HasCorrectDetails()
    {
        KarmaServiceAdapter svc = CreateKarmaService();
        Guid authorId = Guid.NewGuid();

        await svc.AddKarmaAsync(authorId, 50, "analysis_passed", "Plugin passed security analysis");

        IReadOnlyList<KarmaEventDto> history = await svc.GetKarmaHistoryAsync(authorId);
        Assert.Single(history);
        Assert.Equal("analysis_passed", history[0].EventType);
        Assert.Equal(50, history[0].Points);
        Assert.Equal("Plugin passed security analysis", history[0].Description);
    }
}
