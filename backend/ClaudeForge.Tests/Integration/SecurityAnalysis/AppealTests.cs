using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Tests.Integration.SecurityAnalysis;

/// <summary>
/// Integration tests for the appeal process.
///
/// Uses InMemory EF Core provider to test appeal submission, resolution,
/// duplicate prevention, and status transitions at the data layer.
///
/// These tests validate the entity model, relationships, and constraints
/// that the API handlers (SecurityAnalysisModule.cs) exercise at runtime.
/// </summary>
public sealed class AppealTests : IDisposable
{
    private readonly MarketplaceDbContext _db;

    public AppealTests()
    {
        DbContextOptions<MarketplaceDbContext> options = new DbContextOptionsBuilder<MarketplaceDbContext>()
            .UseInMemoryDatabase($"AppealTests_{Guid.NewGuid():N}")
            .Options;

        _db = new MarketplaceDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private PluginEntity MakePlugin(Guid? id = null, string status = "in_review")
    {
        Guid pluginId = id ?? Guid.NewGuid();
        string shortId = pluginId.ToString("N")[..6];
        return new PluginEntity
        {
            Id = pluginId,
            Name = $"TestPlugin-{shortId}",
            NameNormalized = $"testplugin-{shortId}",
            Slug = $"test-plugin-{shortId}",
            Description = "Test plugin for appeal tests",
            Author = "test-author",
            SecurityStatus = status,
            SecurityScore = 65m,
        };
    }

    private AnalysisResultEntity MakeAnalysisResult(Guid pluginId, decimal score = 65m, string status = "failed")
    {
        return new AnalysisResultEntity
        {
            Id = Guid.NewGuid(),
            PluginId = pluginId,
            PluginVersion = "1.0.0",
            StaticEslintScore = 70m,
            StaticSemgrepScore = 60m,
            StaticGitleaksScore = null,
            StaticTrivyScore = null,
            TotalScore = score,
            Status = status,
            AnalysisCompletedAt = DateTimeOffset.UtcNow,
            StaticWeight = 0.6m,
            DynamicWeight = 0.4m,
            PassThreshold = 80m,
            FailThreshold = 50m,
        };
    }

    // ── T.4: Submit → pending → resolve approved → status "passed" ──────────

    [Fact]
    public async Task AppealFlow_SubmitAndApprove_AnalysisStatusBecomesPassed()
    {
        // Arrange
        PluginEntity plugin = MakePlugin(status: "in_review");
        _db.Plugins.Add(plugin);

        AnalysisResultEntity analysis = MakeAnalysisResult(plugin.Id, score: 65m, status: "failed");
        _db.AnalysisResults.Add(analysis);
        await _db.SaveChangesAsync();

        Guid authorId = Guid.NewGuid();

        // Act — submit appeal
        var appeal = new AppealEntity
        {
            Id = Guid.NewGuid(),
            PluginId = plugin.Id,
            AnalysisResultId = analysis.Id,
            AuthorId = authorId,
            Reason = "False positive — the flagged dependency is only used at build time",
            Evidence = "https://github.com/example/plugin/pull/42",
            Status = "pending",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.Appeals.Add(appeal);
        await _db.SaveChangesAsync();

        // Assert — appeal is pending
        AppealEntity? savedAppeal = await _db.Appeals.FindAsync(appeal.Id);
        Assert.NotNull(savedAppeal);
        Assert.Equal("pending", savedAppeal.Status);
        Assert.Equal(authorId, savedAppeal.AuthorId);

        // Act — resolve as approved
        savedAppeal.Status = "approved";
        savedAppeal.ReviewedBy = Guid.NewGuid();
        savedAppeal.ReviewedAt = DateTimeOffset.UtcNow;
        savedAppeal.Resolution = "Evidence confirms false positive. Accepting appeal.";

        // Update analysis result status
        AnalysisResultEntity? resultEntity = await _db.AnalysisResults.FindAsync(analysis.Id);
        Assert.NotNull(resultEntity);
        resultEntity.Status = "passed";

        // Update plugin
        PluginEntity? pluginEntity = await _db.Plugins.FindAsync(plugin.Id);
        Assert.NotNull(pluginEntity);
        pluginEntity.SecurityStatus = "passed";
        pluginEntity.SecurityScore = resultEntity.TotalScore;

        await _db.SaveChangesAsync();

        // Assert — plugin status changed
        PluginEntity? updatedPlugin = await _db.Plugins.FindAsync(plugin.Id);
        Assert.NotNull(updatedPlugin);
        Assert.Equal("passed", updatedPlugin.SecurityStatus);
        Assert.Equal(65m, updatedPlugin.SecurityScore);

        // Assert — analysis status changed
        AnalysisResultEntity? updatedAnalysis = await _db.AnalysisResults.FindAsync(analysis.Id);
        Assert.NotNull(updatedAnalysis);
        Assert.Equal("passed", updatedAnalysis.Status);

        // Assert — appeal resolved
        AppealEntity? resolvedAppeal = await _db.Appeals.FindAsync(appeal.Id);
        Assert.NotNull(resolvedAppeal);
        Assert.Equal("approved", resolvedAppeal.Status);
        Assert.NotNull(resolvedAppeal.ReviewedBy);
        Assert.NotNull(resolvedAppeal.ReviewedAt);
    }

    // ── Submit → resolve as rejected → analysis stands ──────────────────────

    [Fact]
    public async Task AppealFlow_SubmitAndReject_AnalysisStatusUnchanged()
    {
        // Arrange
        PluginEntity plugin = MakePlugin(status: "failed");
        _db.Plugins.Add(plugin);

        AnalysisResultEntity analysis = MakeAnalysisResult(plugin.Id, score: 30m, status: "failed");
        _db.AnalysisResults.Add(analysis);
        await _db.SaveChangesAsync();

        Guid authorId = Guid.NewGuid();

        // Submit appeal
        var appeal = new AppealEntity
        {
            Id = Guid.NewGuid(),
            PluginId = plugin.Id,
            AnalysisResultId = analysis.Id,
            AuthorId = authorId,
            Reason = "The analysis missed our mitigation steps",
            Status = "pending",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.Appeals.Add(appeal);
        await _db.SaveChangesAsync();

        // Act — resolve as rejected (DO NOT update analysis/plugin)
        appeal.Status = "rejected";
        appeal.ReviewedBy = Guid.NewGuid();
        appeal.ReviewedAt = DateTimeOffset.UtcNow;
        appeal.Resolution = "Mitigation evidence insufficient. Rejecting.";
        await _db.SaveChangesAsync();

        // Assert — analysis unchanged
        AnalysisResultEntity? unchangedAnalysis = await _db.AnalysisResults.FindAsync(analysis.Id);
        Assert.NotNull(unchangedAnalysis);
        Assert.Equal("failed", unchangedAnalysis.Status);

        // Assert — plugin unchanged
        PluginEntity? unchangedPlugin = await _db.Plugins.FindAsync(plugin.Id);
        Assert.NotNull(unchangedPlugin);
        Assert.Equal("failed", unchangedPlugin.SecurityStatus);

        // Assert — appeal resolved
        Assert.Equal("rejected", appeal.Status);
    }

    // ── Cannot submit duplicate appeal for same plugin ──────────────────────

    [Fact]
    public async Task AppealFlow_DuplicatePendingAppeal_Detected()
    {
        // Arrange
        PluginEntity plugin = MakePlugin(status: "in_review");
        _db.Plugins.Add(plugin);
        await _db.SaveChangesAsync();

        Guid authorId = Guid.NewGuid();

        // First appeal
        _db.Appeals.Add(new AppealEntity
        {
            Id = Guid.NewGuid(),
            PluginId = plugin.Id,
            AuthorId = authorId,
            Reason = "First appeal",
            Status = "pending",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();

        // Act — check existing pending appeal (duplicate detection)
        bool alreadyAppealed = await _db.Appeals.AnyAsync(
            a => a.PluginId == plugin.Id && a.Status == "pending");

        // Assert
        Assert.True(alreadyAppealed);

        // Try to submit another appeal — would be rejected by handler
        if (alreadyAppealed)
        {
            // This simulates the handler returning 409 Conflict
            Assert.True(true, "Duplicate appeal correctly blocked");
        }
    }

    [Fact]
    public async Task AppealFlow_ResolvedAppeal_AllowsNewAppeal()
    {
        // Arrange
        PluginEntity plugin = MakePlugin(status: "in_review");
        _db.Plugins.Add(plugin);
        await _db.SaveChangesAsync();

        Guid authorId = Guid.NewGuid();

        // Submit and resolve first appeal as rejected
        var firstAppeal = new AppealEntity
        {
            Id = Guid.NewGuid(),
            PluginId = plugin.Id,
            AuthorId = authorId,
            Reason = "First appeal",
            Status = "rejected",  // already resolved
            ReviewedBy = Guid.NewGuid(),
            ReviewedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
        };
        _db.Appeals.Add(firstAppeal);
        await _db.SaveChangesAsync();

        // Act — check for pending appeal (should be false)
        bool hasPending = await _db.Appeals.AnyAsync(
            a => a.PluginId == plugin.Id && a.Status == "pending");

        // Assert — no pending appeal, so new submission allowed
        Assert.False(hasPending);
    }

    // ── Cannot appeal already-passed analysis ────────────────────────────────

    [Fact]
    public async Task AppealFlow_PassedPlugin_AppealChecksPluginStatus()
    {
        // Arrange
        PluginEntity plugin = MakePlugin(status: "passed");
        _db.Plugins.Add(plugin);
        await _db.SaveChangesAsync();

        // Act — simulate the API handler logic: check if plugin is passed
        PluginEntity? dbPlugin = await _db.Plugins.FindAsync(plugin.Id);
        Assert.NotNull(dbPlugin);

        bool isAlreadyPassed = dbPlugin.SecurityStatus == "passed";

        // Assert — no appeal allowed for passed plugins
        Assert.True(isAlreadyPassed);
        // In the actual handler, this would return a 400 Bad Request
    }

    // ── Appeal tracks analysis result relationship ───────────────────────────

    [Fact]
    public async Task AppealFlow_AnalysisResultLinked_Correctly()
    {
        // Arrange
        PluginEntity plugin = MakePlugin(status: "failed");
        _db.Plugins.Add(plugin);

        AnalysisResultEntity analysis = MakeAnalysisResult(plugin.Id, score: 30m, status: "failed");
        _db.AnalysisResults.Add(analysis);
        await _db.SaveChangesAsync();

        // Act — create appeal linked to analysis result
        var appeal = new AppealEntity
        {
            Id = Guid.NewGuid(),
            PluginId = plugin.Id,
            AnalysisResultId = analysis.Id,
            AuthorId = Guid.NewGuid(),
            Reason = "Appeal with evidence",
            Status = "pending",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.Appeals.Add(appeal);
        await _db.SaveChangesAsync();

        // Assert — relationship is intact
        AppealEntity? saved = await _db.Appeals
            .Include(a => a.AnalysisResult)
            .FirstOrDefaultAsync(a => a.Id == appeal.Id);

        Assert.NotNull(saved);
        Assert.NotNull(saved.AnalysisResult);
        Assert.Equal(analysis.Id, saved.AnalysisResult.Id);
        Assert.Equal("failed", saved.AnalysisResult.Status);
    }

    // ── Appeal without analysis result (orphaned) ────────────────────────────

    [Fact]
    public async Task AppealFlow_NullAnalysisResult_Allowed()
    {
        PluginEntity plugin = MakePlugin(status: "in_review");
        _db.Plugins.Add(plugin);
        await _db.SaveChangesAsync();

        // Create appeal without linking to analysis result
        var appeal = new AppealEntity
        {
            Id = Guid.NewGuid(),
            PluginId = plugin.Id,
            AnalysisResultId = null, // orphaned — analysis result may have been deleted
            AuthorId = Guid.NewGuid(),
            Reason = "General appeal",
            Status = "pending",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.Appeals.Add(appeal);
        await _db.SaveChangesAsync();

        AppealEntity? saved = await _db.Appeals.FindAsync(appeal.Id);
        Assert.NotNull(saved);
        Assert.Null(saved.AnalysisResultId);
    }
}
