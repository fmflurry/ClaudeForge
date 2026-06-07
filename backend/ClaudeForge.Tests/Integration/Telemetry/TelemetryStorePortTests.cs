using ClaudeForge.Application.Modules.Telemetry.Ports;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using ClaudeForge.Infrastructure.Telemetry;
using ClaudeForge.Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Tests.Integration.Telemetry;

/// <summary>
/// Store integration tests for Group 8 (tasks 8.1, 8.3, 8.5):
/// TelemetryStoreAdapter (implements ITelemetryStorePort) against a real Postgres container.
///
/// Expected production types (coder MUST match these names exactly):
///
///   Namespace: ClaudeForge.Infrastructure.Telemetry
///     TelemetryStoreAdapter : ITelemetryStorePort
///       TelemetryStoreAdapter(MarketplaceDbContext context)
///
///   Namespace: ClaudeForge.Application.Modules.Telemetry.Ports
///     ITelemetryStorePort
///       Task RecordEventAsync(TelemetryEvent ev, CancellationToken ct = default)
///         — inserts raw telemetry_events row AND atomically upserts
///           telemetry_aggregates (pluginId, version, eventType, TODAY) count += 1
///       Task&lt;TelemetrySummaryDto&gt; GetSummaryAsync(Guid pluginId, CancellationToken ct = default)
///         — reads aggregates only (telemetry_aggregates): total downloads + installs
///           across all versions/windows + last-7-day DailyActivityDto breakdown
///       Task&lt;int&gt; PurgeRawEventsOlderThanAsync(int days, CancellationToken ct = default)
///         — deletes raw telemetry_events rows where occurred_at &lt; NOW() - 'days' days
///           DOES NOT touch telemetry_aggregates
///
/// Atomic aggregate bump (RecordEventAsync must do both in one transaction):
///   1. INSERT INTO telemetry_events (...)
///   2. INSERT INTO telemetry_aggregates (...) ON CONFLICT ... DO UPDATE SET count = count + 1
///
/// Retention semantics (PurgeRawEventsOlderThanAsync):
///   — old raw events (> N days) deleted
///   — recent raw events (≤ N days) kept
///   — aggregates row counts unchanged
/// </summary>
[Collection(PostgresFixture.CollectionName)]
public sealed class TelemetryStorePortTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public TelemetryStorePortTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // -------------------------------------------------------------------------
    // Per-test isolation: TRUNCATE all marketplace tables before each test.
    // -------------------------------------------------------------------------

    public async Task InitializeAsync()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        await ctx.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE TABLE
                telemetry_aggregates,
                telemetry_events,
                plugin_categories,
                plugin_versions,
                plugins,
                categories
            RESTART IDENTITY CASCADE
            """);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static readonly string ValidAnonClientId = new('b', 64);  // 64-hex characters

    /// <summary>Seeds a plugin row so FK constraints on telemetry tables are satisfied.</summary>
    private static async Task<PluginEntity> SeedPluginAsync(MarketplaceDbContext ctx)
    {
        PluginEntity plugin = new()
        {
            Id = Guid.NewGuid(),
            Name = "TelemetryTestPlugin-" + Guid.NewGuid().ToString("N")[..8],
            NameNormalized = "telemetrytestplugin-" + Guid.NewGuid().ToString("N")[..8],
            Slug = "telemetry-test-" + Guid.NewGuid().ToString("N")[..8],
            Description = "Plugin used in telemetry store tests",
            Author = "test-author",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();
        return plugin;
    }

    private static TelemetryEvent MakeEvent(
        Guid pluginId,
        string eventType = "download",
        string? version = "1.0.0") =>
        new()
        {
            EventType = eventType,
            PluginId = pluginId,
            Version = version,
            AnonClientId = ValidAnonClientId,
            ClientOs = "linux",
            ClientArch = "x64",
            OccurredAt = DateTimeOffset.UtcNow,
        };

    // -------------------------------------------------------------------------
    // RecordEventAsync: inserts raw row
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RecordEventAsync_ValidDownloadEvent_InsertsRawTelemetryRow()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        PluginEntity plugin = await SeedPluginAsync(ctx);
        TelemetryStoreAdapter adapter = new(ctx);
        TelemetryEvent ev = MakeEvent(plugin.Id, "download");

        // Act
        await adapter.RecordEventAsync(ev);

        // Assert — raw event row exists
        await using MarketplaceDbContext verifyCtx = _fixture.CreateContext();
        int rawCount = await verifyCtx.TelemetryEvents
            .CountAsync(e => e.PluginId == plugin.Id && e.EventType == "download");

        Assert.Equal(1, rawCount);
    }

    [Fact]
    public async Task RecordEventAsync_ValidInstallEvent_InsertsRawTelemetryRow()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        PluginEntity plugin = await SeedPluginAsync(ctx);
        TelemetryStoreAdapter adapter = new(ctx);
        TelemetryEvent ev = MakeEvent(plugin.Id, "install");

        // Act
        await adapter.RecordEventAsync(ev);

        // Assert
        await using MarketplaceDbContext verifyCtx = _fixture.CreateContext();
        int rawCount = await verifyCtx.TelemetryEvents
            .CountAsync(e => e.PluginId == plugin.Id && e.EventType == "install");

        Assert.Equal(1, rawCount);
    }

    // -------------------------------------------------------------------------
    // RecordEventAsync: ATOMICALLY bumps telemetry_aggregates daily bucket
    // Design §3 Counter Integrity:
    //   "Atomically writes telemetry_events row + increments telemetry_aggregates counter"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RecordEventAsync_DownloadEvent_BumpsDailyAggregateCountByOne()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        PluginEntity plugin = await SeedPluginAsync(ctx);
        TelemetryStoreAdapter adapter = new(ctx);
        TelemetryEvent ev = MakeEvent(plugin.Id, "download", "1.0.0");

        // Act
        await adapter.RecordEventAsync(ev);

        // Assert — aggregate row exists with count=1
        await using MarketplaceDbContext verifyCtx = _fixture.CreateContext();
        TelemetryAggregateEntity? agg = await verifyCtx.TelemetryAggregates
            .FirstOrDefaultAsync(a =>
                a.PluginId == plugin.Id &&
                a.EventType == "download" &&
                a.WindowStart == DateOnly.FromDateTime(DateTime.UtcNow));

        Assert.NotNull(agg);
        Assert.Equal(1, agg!.Count);
    }

    [Fact]
    public async Task RecordEventAsync_TwoDownloadEvents_AggregateCountIsTwo()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        PluginEntity plugin = await SeedPluginAsync(ctx);
        TelemetryStoreAdapter adapter = new(ctx);

        // Act — two download events for the same plugin
        await adapter.RecordEventAsync(MakeEvent(plugin.Id, "download", "1.0.0"));
        await using MarketplaceDbContext ctx2 = _fixture.CreateContext();
        TelemetryStoreAdapter adapter2 = new(ctx2);
        await adapter2.RecordEventAsync(MakeEvent(plugin.Id, "download", "1.0.0"));

        // Assert — aggregate count bumped to 2
        await using MarketplaceDbContext verifyCtx = _fixture.CreateContext();
        TelemetryAggregateEntity? agg = await verifyCtx.TelemetryAggregates
            .FirstOrDefaultAsync(a =>
                a.PluginId == plugin.Id &&
                a.EventType == "download" &&
                a.WindowStart == DateOnly.FromDateTime(DateTime.UtcNow));

        Assert.NotNull(agg);
        Assert.Equal(2, agg!.Count);
    }

    [Fact]
    public async Task RecordEventAsync_DownloadAndInstall_SeparateAggregateBuckets()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        PluginEntity plugin = await SeedPluginAsync(ctx);
        TelemetryStoreAdapter adapter = new(ctx);

        // Act — one download, one install
        await adapter.RecordEventAsync(MakeEvent(plugin.Id, "download", "1.0.0"));
        await using MarketplaceDbContext ctx2 = _fixture.CreateContext();
        TelemetryStoreAdapter adapter2 = new(ctx2);
        await adapter2.RecordEventAsync(MakeEvent(plugin.Id, "install", "1.0.0"));

        // Assert — two SEPARATE bucket rows (download vs install)
        await using MarketplaceDbContext verifyCtx = _fixture.CreateContext();
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

        TelemetryAggregateEntity? downloadAgg = await verifyCtx.TelemetryAggregates
            .FirstOrDefaultAsync(a =>
                a.PluginId == plugin.Id &&
                a.EventType == "download" &&
                a.WindowStart == today);

        TelemetryAggregateEntity? installAgg = await verifyCtx.TelemetryAggregates
            .FirstOrDefaultAsync(a =>
                a.PluginId == plugin.Id &&
                a.EventType == "install" &&
                a.WindowStart == today);

        Assert.NotNull(downloadAgg);
        Assert.Equal(1, downloadAgg!.Count);
        Assert.NotNull(installAgg);
        Assert.Equal(1, installAgg!.Count);
    }

    [Fact]
    public async Task RecordEventAsync_StoresAnonClientIdNotPii()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        PluginEntity plugin = await SeedPluginAsync(ctx);
        TelemetryStoreAdapter adapter = new(ctx);
        TelemetryEvent ev = MakeEvent(plugin.Id) with { AnonClientId = ValidAnonClientId };

        // Act
        await adapter.RecordEventAsync(ev);

        // Assert — anon client id stored, and it's the 64-hex we sent (no PII transformation)
        await using MarketplaceDbContext verifyCtx = _fixture.CreateContext();
        TelemetryEventEntity? entity = await verifyCtx.TelemetryEvents
            .FirstOrDefaultAsync(e => e.PluginId == plugin.Id);

        Assert.NotNull(entity);
        Assert.Equal(ValidAnonClientId, entity!.AnonClientId?.Trim()); // CHAR(64) may pad
    }

    // -------------------------------------------------------------------------
    // GetSummaryAsync: reads aggregates only, returns correct totals + 7-day breakdown
    // Spec: "returns total downloads/installs + last-7-day activity"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetSummaryAsync_AfterIngest_ReturnsCorrectTotalDownloads()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        PluginEntity plugin = await SeedPluginAsync(ctx);
        TelemetryStoreAdapter adapter = new(ctx);

        // Seed 3 download events
        for (int i = 0; i < 3; i++)
        {
            await using MarketplaceDbContext c = _fixture.CreateContext();
            await new TelemetryStoreAdapter(c).RecordEventAsync(MakeEvent(plugin.Id, "download"));
        }

        // Act
        await using MarketplaceDbContext summaryCtx = _fixture.CreateContext();
        TelemetrySummaryDto summary = await new TelemetryStoreAdapter(summaryCtx)
            .GetSummaryAsync(plugin.Id);

        // Assert
        Assert.Equal(plugin.Id, summary.PluginId);
        Assert.Equal(3, summary.TotalDownloads);
    }

    [Fact]
    public async Task GetSummaryAsync_AfterIngest_ReturnsCorrectTotalInstalls()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        PluginEntity plugin = await SeedPluginAsync(ctx);

        for (int i = 0; i < 2; i++)
        {
            await using MarketplaceDbContext c = _fixture.CreateContext();
            await new TelemetryStoreAdapter(c).RecordEventAsync(MakeEvent(plugin.Id, "install"));
        }

        // Act
        await using MarketplaceDbContext summaryCtx = _fixture.CreateContext();
        TelemetrySummaryDto summary = await new TelemetryStoreAdapter(summaryCtx)
            .GetSummaryAsync(plugin.Id);

        // Assert
        Assert.Equal(2, summary.TotalInstalls);
    }

    [Fact]
    public async Task GetSummaryAsync_NoActivity_ReturnsZeroTotalsAndEmptyLast7Days()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        PluginEntity plugin = await SeedPluginAsync(ctx);

        // Act
        await using MarketplaceDbContext summaryCtx = _fixture.CreateContext();
        TelemetrySummaryDto summary = await new TelemetryStoreAdapter(summaryCtx)
            .GetSummaryAsync(plugin.Id);

        // Assert
        Assert.Equal(0, summary.TotalDownloads);
        Assert.Equal(0, summary.TotalInstalls);
        Assert.NotNull(summary.Last7Days);
        // No activity rows for last 7 days expected
        Assert.True(summary.Last7Days.Count <= 7);
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsLast7DayBreakdown_WithTodaysActivity()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        PluginEntity plugin = await SeedPluginAsync(ctx);

        // Insert 2 download events today
        for (int i = 0; i < 2; i++)
        {
            await using MarketplaceDbContext c = _fixture.CreateContext();
            await new TelemetryStoreAdapter(c).RecordEventAsync(MakeEvent(plugin.Id, "download"));
        }

        // Act
        await using MarketplaceDbContext summaryCtx = _fixture.CreateContext();
        TelemetrySummaryDto summary = await new TelemetryStoreAdapter(summaryCtx)
            .GetSummaryAsync(plugin.Id);

        // Assert — today's activity included in breakdown
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        DailyActivityDto? todayActivity = summary.Last7Days
            .FirstOrDefault(d => d.Date == today);

        Assert.NotNull(todayActivity);
        Assert.Equal(2, todayActivity!.Downloads);
    }

    [Fact]
    public async Task GetSummaryAsync_DoesNotReturnRawEventData()
    {
        // Arrange — insert some raw events, then verify summary has no anon IDs etc.
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        PluginEntity plugin = await SeedPluginAsync(ctx);
        await new TelemetryStoreAdapter(ctx).RecordEventAsync(MakeEvent(plugin.Id, "download"));

        // Act
        await using MarketplaceDbContext summaryCtx = _fixture.CreateContext();
        TelemetrySummaryDto summary = await new TelemetryStoreAdapter(summaryCtx)
            .GetSummaryAsync(plugin.Id);

        // Assert via reflection — TelemetrySummaryDto must not have raw-event fields
        System.Reflection.PropertyInfo[] props =
            typeof(TelemetrySummaryDto).GetProperties(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        string[] propNames = props.Select(p => p.Name).ToArray();
        Assert.DoesNotContain("AnonClientId", propNames, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("RawEvents", propNames, StringComparer.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------
    // PurgeRawEventsOlderThanAsync: old raw gone, recent raw kept, aggregates intact
    // Task 8.5 / design.md §3: "nightly batch aggregates raw events into daily windows,
    //   then purges raw events >90 days old"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PurgeRawEventsOlderThanAsync_OldEvents_DeletesOldRawRows()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        PluginEntity plugin = await SeedPluginAsync(ctx);

        DateTimeOffset oldTimestamp = DateTimeOffset.UtcNow.AddDays(-91);
        DateTimeOffset recentTimestamp = DateTimeOffset.UtcNow.AddDays(-1);

        // Manually insert one old + one recent raw event bypassing the use-case
        ctx.TelemetryEvents.Add(new TelemetryEventEntity
        {
            EventType = "download",
            PluginId = plugin.Id,
            Version = "1.0.0",
            AnonClientId = ValidAnonClientId,
            ClientOs = "linux",
            ClientArch = "x64",
            OccurredAt = oldTimestamp,
        });
        ctx.TelemetryEvents.Add(new TelemetryEventEntity
        {
            EventType = "download",
            PluginId = plugin.Id,
            Version = "1.0.0",
            AnonClientId = ValidAnonClientId,
            ClientOs = "linux",
            ClientArch = "x64",
            OccurredAt = recentTimestamp,
        });
        await ctx.SaveChangesAsync();

        // Act
        await using MarketplaceDbContext purgeCtx = _fixture.CreateContext();
        TelemetryStoreAdapter adapter = new(purgeCtx);
        int deletedCount = await adapter.PurgeRawEventsOlderThanAsync(90);

        // Assert — old event deleted, recent kept
        Assert.Equal(1, deletedCount);

        await using MarketplaceDbContext verifyCtx = _fixture.CreateContext();
        int remaining = await verifyCtx.TelemetryEvents.CountAsync(e => e.PluginId == plugin.Id);
        Assert.Equal(1, remaining);
    }

    [Fact]
    public async Task PurgeRawEventsOlderThanAsync_LeavesAggregatesUntouched()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        PluginEntity plugin = await SeedPluginAsync(ctx);
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Insert an aggregate row directly (simulates pre-aggregated data)
        ctx.TelemetryAggregates.Add(new TelemetryAggregateEntity
        {
            PluginId = plugin.Id,
            Version = "1.0.0",
            EventType = "download",
            Count = 42,
            WindowStart = today,
        });
        // Insert an old raw event
        ctx.TelemetryEvents.Add(new TelemetryEventEntity
        {
            EventType = "download",
            PluginId = plugin.Id,
            Version = "1.0.0",
            AnonClientId = ValidAnonClientId,
            OccurredAt = DateTimeOffset.UtcNow.AddDays(-100),
        });
        await ctx.SaveChangesAsync();

        // Act
        await using MarketplaceDbContext purgeCtx = _fixture.CreateContext();
        await new TelemetryStoreAdapter(purgeCtx).PurgeRawEventsOlderThanAsync(90);

        // Assert — aggregate row still intact with count=42
        await using MarketplaceDbContext verifyCtx = _fixture.CreateContext();
        TelemetryAggregateEntity? agg = await verifyCtx.TelemetryAggregates
            .FirstOrDefaultAsync(a =>
                a.PluginId == plugin.Id &&
                a.EventType == "download" &&
                a.WindowStart == today);

        Assert.NotNull(agg);
        Assert.Equal(42, agg!.Count);
    }

    [Fact]
    public async Task PurgeRawEventsOlderThanAsync_RecentEvents_KeepsRecentRawRows()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        PluginEntity plugin = await SeedPluginAsync(ctx);

        // Insert 3 recent raw events (within N days)
        for (int i = 0; i < 3; i++)
        {
            ctx.TelemetryEvents.Add(new TelemetryEventEntity
            {
                EventType = "download",
                PluginId = plugin.Id,
                Version = "1.0.0",
                AnonClientId = ValidAnonClientId,
                OccurredAt = DateTimeOffset.UtcNow.AddDays(-i),
            });
        }
        await ctx.SaveChangesAsync();

        // Act
        await using MarketplaceDbContext purgeCtx = _fixture.CreateContext();
        int deletedCount = await new TelemetryStoreAdapter(purgeCtx).PurgeRawEventsOlderThanAsync(90);

        // Assert — nothing deleted (all within window)
        Assert.Equal(0, deletedCount);

        await using MarketplaceDbContext verifyCtx = _fixture.CreateContext();
        int remaining = await verifyCtx.TelemetryEvents.CountAsync(e => e.PluginId == plugin.Id);
        Assert.Equal(3, remaining);
    }

    [Fact]
    public async Task PurgeRawEventsOlderThanAsync_MixedAgePlusAggregatesExist_ReturnsCorrectDeletedCount()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        PluginEntity plugin = await SeedPluginAsync(ctx);

        ctx.TelemetryEvents.Add(new TelemetryEventEntity
        {
            EventType = "download",
            PluginId = plugin.Id,
            Version = "1.0.0",
            AnonClientId = ValidAnonClientId,
            OccurredAt = DateTimeOffset.UtcNow.AddDays(-95),  // old → delete
        });
        ctx.TelemetryEvents.Add(new TelemetryEventEntity
        {
            EventType = "install",
            PluginId = plugin.Id,
            Version = "1.0.0",
            AnonClientId = ValidAnonClientId,
            OccurredAt = DateTimeOffset.UtcNow.AddDays(-50),  // recent → keep
        });
        await ctx.SaveChangesAsync();

        // Act
        await using MarketplaceDbContext purgeCtx = _fixture.CreateContext();
        int deletedCount = await new TelemetryStoreAdapter(purgeCtx).PurgeRawEventsOlderThanAsync(90);

        // Assert
        Assert.Equal(1, deletedCount);
    }
}
