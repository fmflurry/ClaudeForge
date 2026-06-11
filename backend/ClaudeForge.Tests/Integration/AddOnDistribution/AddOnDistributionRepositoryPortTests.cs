using ClaudeForge.Application.Modules.AddOnDistribution.Ports;
using ClaudeForge.Core.Domain.Plugins;
using ClaudeForge.Infrastructure.AddOnDistribution;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using ClaudeForge.Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Tests.Integration.AddOnDistribution;

/// <summary>
/// Integration tests for Group 6 — IAddOnDistributionRepositoryPort adapter.
///
/// Runs against a REAL PostgreSQL 16 container via Testcontainers.
/// Docker must be running on the test host.
///
/// Expected production types (coder MUST match these names exactly):
///
///   ClaudeForge.Infrastructure.AddOnDistribution.AddOnDistributionRepositoryAdapter
///     AddOnDistributionRepositoryAdapter(MarketplaceDbContext context)
///     implements IAddOnDistributionRepositoryPort
///
///   ClaudeForge.Application.Modules.AddOnDistribution.Ports.IAddOnDistributionRepositoryPort
///     Task&lt;DownloadResolutionResult&gt; ResolveAsync(
///         Guid pluginId, string? version, CancellationToken ct = default)
///     Task IncrementDownloadCountAsync(
///         Guid pluginId, string version, CancellationToken ct = default)
///
///   DownloadResolutionResult (sealed hierarchy):
///     abstract record DownloadResolutionResult
///     sealed record PluginNotFoundResult : DownloadResolutionResult
///     sealed record VersionNotFoundResult(string Version) : DownloadResolutionResult
///     sealed record FoundResult(DownloadResolution Resolution) : DownloadResolutionResult
///
///   DownloadResolution (sealed record)
///     string PluginName, string Version, string PackageKey,
///     string PackageFormat, long SizeBytes, string Sha256
///
/// IncrementDownloadCountAsync MUST atomically (one transaction):
///   1. Upsert telemetry_aggregates — (pluginId, version, 'download', TODAY) count += 1
///   2. UPDATE plugin_versions SET download_count = download_count + 1
///      WHERE plugin_id = @pluginId AND version = @version
///   3. UPDATE plugins SET download_count = download_count + 1
///      WHERE id = @pluginId
/// </summary>
[Collection(PostgresFixture.CollectionName)]
public sealed class PluginDistributionRepositoryPortTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public PluginDistributionRepositoryPortTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // -------------------------------------------------------------------------
    // Per-test isolation: truncate all marketplace tables before each test.
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
    // Seed helpers
    // -------------------------------------------------------------------------

    private static AddOnEntity BuildPluginEntity(
        string name = "test-plugin",
        long downloadCount = 0) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            NameNormalized = name.ToLowerInvariant(),
            Slug = name.ToLowerInvariant().Replace(" ", "-"),
            Description = "Integration test plugin",
            Author = "Test Author",
            DownloadCount = downloadCount,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    private static AddOnVersionEntity BuildVersionEntity(
        Guid pluginId,
        string version,
        bool isLatest,
        long downloadCount = 0) =>
        new()
        {
            Id = Guid.NewGuid(),
            PluginId = pluginId,
            Version = version,
            VersionSort = SemVer.Parse(version).ToVersionSort(),
            ReleaseNotes = string.Empty,
            IsLatest = isLatest,
            PackageKey = $"plugins/{pluginId}/{version}/package.tar.gz",
            PackageFormat = "tar.gz",
            SizeBytes = 1024,
            Sha256 = "aabbccddaabbccddaabbccddaabbccddaabbccddaabbccddaabbccddaabbccdd",
            DownloadCount = downloadCount,
            ReleasedAt = DateTimeOffset.UtcNow,
        };

    private async Task<(AddOnEntity plugin, AddOnVersionEntity version)> SeedPluginWithVersionAsync(
        string pluginName = "test-plugin",
        string version = "1.0.0",
        bool isLatest = true)
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        AddOnEntity plugin = BuildPluginEntity(pluginName);
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        AddOnVersionEntity versionEntity = BuildVersionEntity(plugin.Id, version, isLatest);
        ctx.PluginVersions.Add(versionEntity);
        await ctx.SaveChangesAsync();

        return (plugin, versionEntity);
    }

    // =========================================================================
    // ResolveAsync — null version returns latest
    // =========================================================================

    [Fact]
    public async Task ResolveAsync_NullVersion_ReturnsFoundResultWithLatestVersion()
    {
        // Arrange
        (AddOnEntity plugin, AddOnVersionEntity _) =
            await SeedPluginWithVersionAsync("resolve-plugin", "1.5.0", isLatest: true);

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        AddOnDistributionRepositoryAdapter adapter = new(ctx);

        // Act
        DownloadResolutionResult result = await adapter.ResolveAsync(plugin.Id, null);

        // Assert
        FoundResult found = Assert.IsType<FoundResult>(result);
        Assert.Equal("1.5.0", found.Resolution.Version);
        Assert.Equal("resolve-plugin", found.Resolution.PluginName);
        Assert.Equal("plugins", found.Resolution.PackageKey.Split('/')[0]);
    }

    [Fact]
    public async Task ResolveAsync_NullVersion_WhenMultipleVersionsExist_ReturnsIsLatest()
    {
        // Arrange — two versions; only 2.0.0 is_latest
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        AddOnEntity plugin = BuildPluginEntity("multi-version-plugin");
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        ctx.PluginVersions.Add(BuildVersionEntity(plugin.Id, "1.0.0", isLatest: false));
        ctx.PluginVersions.Add(BuildVersionEntity(plugin.Id, "2.0.0", isLatest: true));
        await ctx.SaveChangesAsync();

        AddOnDistributionRepositoryAdapter adapter = new(ctx);

        // Act
        DownloadResolutionResult result = await adapter.ResolveAsync(plugin.Id, null);

        // Assert
        FoundResult found = Assert.IsType<FoundResult>(result);
        Assert.Equal("2.0.0", found.Resolution.Version);
    }

    // =========================================================================
    // ResolveAsync — explicit version
    // =========================================================================

    [Fact]
    public async Task ResolveAsync_ExplicitVersion_ReturnsFoundResultWithMatchingVersion()
    {
        // Arrange — two versions; requesting older one explicitly
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        AddOnEntity plugin = BuildPluginEntity("explicit-version-plugin");
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        ctx.PluginVersions.Add(BuildVersionEntity(plugin.Id, "1.0.0", isLatest: false));
        ctx.PluginVersions.Add(BuildVersionEntity(plugin.Id, "2.0.0", isLatest: true));
        await ctx.SaveChangesAsync();

        AddOnDistributionRepositoryAdapter adapter = new(ctx);

        // Act
        DownloadResolutionResult result = await adapter.ResolveAsync(plugin.Id, "1.0.0");

        // Assert
        FoundResult found = Assert.IsType<FoundResult>(result);
        Assert.Equal("1.0.0", found.Resolution.Version);
    }

    [Fact]
    public async Task ResolveAsync_ExplicitVersion_ResolutionContainsCorrectPackageKey()
    {
        // Arrange
        (AddOnEntity plugin, AddOnVersionEntity versionEntity) =
            await SeedPluginWithVersionAsync("key-plugin", "3.2.1");

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        AddOnDistributionRepositoryAdapter adapter = new(ctx);

        // Act
        DownloadResolutionResult result = await adapter.ResolveAsync(plugin.Id, "3.2.1");

        // Assert
        FoundResult found = Assert.IsType<FoundResult>(result);
        Assert.Equal(versionEntity.PackageKey, found.Resolution.PackageKey);
        Assert.Equal(versionEntity.SizeBytes, found.Resolution.SizeBytes);
        Assert.Equal(versionEntity.Sha256, found.Resolution.Sha256);
    }

    // =========================================================================
    // ResolveAsync — plugin not found → PluginNotFoundResult
    // =========================================================================

    [Fact]
    public async Task ResolveAsync_UnknownPluginId_ReturnsPluginNotFoundResult()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        AddOnDistributionRepositoryAdapter adapter = new(ctx);

        // Act
        DownloadResolutionResult result = await adapter.ResolveAsync(Guid.NewGuid(), null);

        // Assert
        Assert.IsType<PluginNotFoundResult>(result);
    }

    [Fact]
    public async Task ResolveAsync_UnknownPluginId_WithExplicitVersion_ReturnsPluginNotFoundResult()
    {
        // Arrange — no plugin seeded
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        AddOnDistributionRepositoryAdapter adapter = new(ctx);

        // Act
        DownloadResolutionResult result = await adapter.ResolveAsync(Guid.NewGuid(), "1.0.0");

        // Assert
        Assert.IsType<PluginNotFoundResult>(result);
    }

    // =========================================================================
    // ResolveAsync — version not found → VersionNotFoundResult
    // =========================================================================

    [Fact]
    public async Task ResolveAsync_KnownPluginUnknownVersion_ReturnsVersionNotFoundResult()
    {
        // Arrange — plugin exists, but 9.9.9 does not
        (AddOnEntity plugin, _) =
            await SeedPluginWithVersionAsync("known-plugin", "1.0.0");

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        AddOnDistributionRepositoryAdapter adapter = new(ctx);

        // Act
        DownloadResolutionResult result = await adapter.ResolveAsync(plugin.Id, "9.9.9");

        // Assert
        VersionNotFoundResult versionNotFound = Assert.IsType<VersionNotFoundResult>(result);
        Assert.Equal("9.9.9", versionNotFound.Version);
    }

    // =========================================================================
    // IncrementDownloadCountAsync — atomic three-table update
    // =========================================================================

    [Fact]
    public async Task IncrementDownloadCountAsync_BumpsTelemetryAggregatesBy1()
    {
        // Arrange
        (AddOnEntity plugin, _) =
            await SeedPluginWithVersionAsync("inc-telemetry-plugin", "1.0.0");

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        AddOnDistributionRepositoryAdapter adapter = new(ctx);

        // Act
        await adapter.IncrementDownloadCountAsync(plugin.Id, "1.0.0");

        // Assert
        await using MarketplaceDbContext verifyCtx = _fixture.CreateContext();
        long totalCount = await verifyCtx.TelemetryAggregates
            .Where(ta => ta.PluginId == plugin.Id
                      && ta.Version == "1.0.0"
                      && ta.EventType == "download")
            .SumAsync(ta => ta.Count);

        Assert.Equal(1L, totalCount);
    }

    [Fact]
    public async Task IncrementDownloadCountAsync_BumpsPluginVersionDownloadCountBy1()
    {
        // Arrange
        (AddOnEntity plugin, _) =
            await SeedPluginWithVersionAsync("inc-version-plugin", "1.0.0");

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        AddOnDistributionRepositoryAdapter adapter = new(ctx);

        // Act
        await adapter.IncrementDownloadCountAsync(plugin.Id, "1.0.0");

        // Assert
        await using MarketplaceDbContext verifyCtx = _fixture.CreateContext();
        AddOnVersionEntity? version = await verifyCtx.PluginVersions
            .FirstOrDefaultAsync(pv => pv.PluginId == plugin.Id && pv.Version == "1.0.0");

        Assert.NotNull(version);
        Assert.Equal(1L, version!.DownloadCount);
    }

    [Fact]
    public async Task IncrementDownloadCountAsync_BumpsPluginDownloadCountBy1()
    {
        // Arrange
        (AddOnEntity plugin, _) =
            await SeedPluginWithVersionAsync("inc-plugin-plugin", "1.0.0");

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        AddOnDistributionRepositoryAdapter adapter = new(ctx);

        // Act
        await adapter.IncrementDownloadCountAsync(plugin.Id, "1.0.0");

        // Assert
        await using MarketplaceDbContext verifyCtx = _fixture.CreateContext();
        AddOnEntity? pluginEntity = await verifyCtx.Plugins.FindAsync(plugin.Id);

        Assert.NotNull(pluginEntity);
        Assert.Equal(1L, pluginEntity!.DownloadCount);
    }

    [Fact]
    public async Task IncrementDownloadCountAsync_CalledThreeTimes_AllCountersReflectThree()
    {
        // Arrange
        (AddOnEntity plugin, _) =
            await SeedPluginWithVersionAsync("triple-inc-plugin", "1.0.0");

        // Act — three sequential increments
        for (int i = 0; i < 3; i++)
        {
            await using MarketplaceDbContext ctx = _fixture.CreateContext();
            AddOnDistributionRepositoryAdapter adapter = new(ctx);
            await adapter.IncrementDownloadCountAsync(plugin.Id, "1.0.0");
        }

        // Assert
        await using MarketplaceDbContext verifyCtx = _fixture.CreateContext();

        AddOnEntity? pluginRow = await verifyCtx.Plugins.FindAsync(plugin.Id);
        Assert.NotNull(pluginRow);
        Assert.Equal(3L, pluginRow!.DownloadCount);

        AddOnVersionEntity? versionRow = await verifyCtx.PluginVersions
            .FirstOrDefaultAsync(pv => pv.PluginId == plugin.Id && pv.Version == "1.0.0");
        Assert.NotNull(versionRow);
        Assert.Equal(3L, versionRow!.DownloadCount);

        long aggCount = await verifyCtx.TelemetryAggregates
            .Where(ta => ta.PluginId == plugin.Id
                      && ta.Version == "1.0.0"
                      && ta.EventType == "download")
            .SumAsync(ta => ta.Count);
        Assert.Equal(3L, aggCount);
    }

    // =========================================================================
    // Concurrency test — N concurrent IncrementDownloadCountAsync
    // SPEC: "100 concurrent requests → downloadCount incremented exactly 100 times"
    // =========================================================================

    [Fact]
    public async Task IncrementDownloadCountAsync_100ConcurrentCalls_AllCountersEqualExactly100()
    {
        // Arrange
        const int concurrency = 100;

        (AddOnEntity plugin, _) =
            await SeedPluginWithVersionAsync("concurrent-plugin", "1.0.0");

        // Act — fire 100 concurrent increments using independent DbContext instances
        Task[] tasks = Enumerable.Range(0, concurrency)
            .Select(async _ =>
            {
                await using MarketplaceDbContext ctx = _fixture.CreateContext();
                AddOnDistributionRepositoryAdapter adapter = new(ctx);
                await adapter.IncrementDownloadCountAsync(plugin.Id, "1.0.0");
            })
            .ToArray<Task>();

        await Task.WhenAll(tasks);

        // Assert — all three counters must reflect exactly concurrency (no lost updates)
        await using MarketplaceDbContext verifyCtx = _fixture.CreateContext();

        AddOnEntity? pluginRow = await verifyCtx.Plugins.FindAsync(plugin.Id);
        Assert.NotNull(pluginRow);
        Assert.Equal((long)concurrency, pluginRow!.DownloadCount);

        AddOnVersionEntity? versionRow = await verifyCtx.PluginVersions
            .FirstOrDefaultAsync(pv => pv.PluginId == plugin.Id && pv.Version == "1.0.0");
        Assert.NotNull(versionRow);
        Assert.Equal((long)concurrency, versionRow!.DownloadCount);

        long aggTotal = await verifyCtx.TelemetryAggregates
            .Where(ta => ta.PluginId == plugin.Id
                      && ta.Version == "1.0.0"
                      && ta.EventType == "download")
            .SumAsync(ta => ta.Count);
        Assert.Equal((long)concurrency, aggTotal);
    }
}
