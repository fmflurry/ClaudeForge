using ClaudeForge.Application.Modules.Marketplace.Ports;
using ClaudeForge.Infrastructure.Marketplace;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using ClaudeForge.Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Tests.Integration.Marketplace;

/// <summary>
/// EF adapter integration tests for MarketplaceStatsAdapter against the shared Postgres fixture.
///
/// Seeds a mix of public/private plugins, varying DownloadCount and Author values, plus
/// category rows, and asserts that the adapter's aggregation queries are correct.
///
/// Expected production types (coder MUST match these names exactly):
///
///   Namespace: ClaudeForge.Infrastructure.Marketplace
///     MarketplaceStatsAdapter : IMarketplaceStatsPort
///       MarketplaceStatsAdapter(MarketplaceDbContext context)
///       Task&lt;MarketplaceStatsDto&gt; GetStatsAsync(CancellationToken ct = default)
///         — totalPlugins: COUNT of plugins WHERE visibility = 'public'
///         — totalDownloads: SUM(download_count) across ALL public plugins
///         — publisherCount: COUNT DISTINCT author across public plugins
///         — categoryCount: COUNT of rows in categories table
///
///   Private plugins (visibility = 'private') must be EXCLUDED from totalPlugins,
///   totalDownloads, and publisherCount. Private-plugin authors are excluded too.
/// </summary>
[Collection(PostgresFixture.CollectionName)]
public sealed class MarketplaceStatsAdapterTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public MarketplaceStatsAdapterTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // -------------------------------------------------------------------------
    // Per-test isolation
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

    private static AddOnEntity MakePublicPlugin(
        string author,
        long downloadCount = 0,
        string? nameSuffix = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = $"Stats-Public-{nameSuffix ?? Guid.NewGuid().ToString("N")[..6]}",
            NameNormalized = $"stats-public-{nameSuffix ?? Guid.NewGuid().ToString("N")[..6]}",
            Slug = $"stats-public-{Guid.NewGuid().ToString("N")[..8]}",
            Description = "Public plugin for stats tests",
            Author = author,
            DownloadCount = downloadCount,
            Visibility = "public",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    private static AddOnEntity MakePrivatePlugin(
        Guid ownerOrgId,
        string author,
        long downloadCount = 0) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = $"Stats-Private-{Guid.NewGuid().ToString("N")[..6]}",
            NameNormalized = $"stats-private-{Guid.NewGuid().ToString("N")[..6]}",
            Slug = $"stats-private-{Guid.NewGuid().ToString("N")[..8]}",
            Description = "Private plugin for stats tests",
            Author = author,
            DownloadCount = downloadCount,
            Visibility = "private",
            OwnerOrgId = ownerOrgId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    private static CategoryEntity MakeCategory(string dimension, string value) =>
        new()
        {
            Dimension = dimension,
            Value = value,
            DisplayName = $"{dimension}:{value}",
        };

    /// <summary>Seeds a minimal org row so the FK constraint on private plugins is satisfied.</summary>
    private static async Task<Guid> SeedOrgAsync(MarketplaceDbContext ctx)
    {
        Guid userId = Guid.NewGuid();
        ctx.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"org-seed-{userId:N}@test.local",
            DisplayName = "Seed User",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        Guid orgId = Guid.NewGuid();
        ctx.Organizations.Add(new OrganizationEntity
        {
            Id = orgId,
            Name = $"Org-{orgId:N}",
            NameNormalized = $"org-{orgId:N}",
            Slug = $"org-{orgId:N}",
            CreatedBy = userId,
            CreatedAt = DateTimeOffset.UtcNow,
        });

        await ctx.SaveChangesAsync();
        return orgId;
    }

    // -------------------------------------------------------------------------
    // totalPlugins: counts only public plugins
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetStatsAsync_EmptyDatabase_ReturnsAllZeros()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        MarketplaceStatsAdapter adapter = new(ctx);

        // Act
        MarketplaceStatsDto stats = await adapter.GetStatsAsync();

        // Assert
        Assert.Equal(0L, stats.TotalPlugins);
        Assert.Equal(0L, stats.TotalDownloads);
        Assert.Equal(0L, stats.PublisherCount);
        Assert.Equal(0L, stats.CategoryCount);
    }

    [Fact]
    public async Task GetStatsAsync_OnlyPublicPlugins_TotalPluginsMatchesCount()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        ctx.Plugins.Add(MakePublicPlugin("alice"));
        ctx.Plugins.Add(MakePublicPlugin("bob"));
        ctx.Plugins.Add(MakePublicPlugin("alice")); // same author — distinct-count test
        await ctx.SaveChangesAsync();

        await using MarketplaceDbContext queryCtx = _fixture.CreateContext();
        MarketplaceStatsAdapter adapter = new(queryCtx);

        // Act
        MarketplaceStatsDto stats = await adapter.GetStatsAsync();

        // Assert
        Assert.Equal(3L, stats.TotalPlugins);
    }

    [Fact]
    public async Task GetStatsAsync_MixPublicAndPrivate_TotalPluginsCountsOnlyPublic()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        Guid orgId = await SeedOrgAsync(ctx);

        await using MarketplaceDbContext seedCtx = _fixture.CreateContext();
        seedCtx.Plugins.Add(MakePublicPlugin("alice", 100));
        seedCtx.Plugins.Add(MakePublicPlugin("bob", 200));
        seedCtx.Plugins.Add(MakePrivatePlugin(orgId, "carol", 50));
        await seedCtx.SaveChangesAsync();

        await using MarketplaceDbContext queryCtx = _fixture.CreateContext();
        MarketplaceStatsAdapter adapter = new(queryCtx);

        // Act
        MarketplaceStatsDto stats = await adapter.GetStatsAsync();

        // Assert — 2 public, not 3
        Assert.Equal(2L, stats.TotalPlugins);
    }

    // -------------------------------------------------------------------------
    // totalDownloads: SUM of DownloadCount for public plugins only
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetStatsAsync_PublicPluginsWithDownloads_TotalDownloadsIsSumOfPublicOnly()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        Guid orgId = await SeedOrgAsync(ctx);

        await using MarketplaceDbContext seedCtx = _fixture.CreateContext();
        seedCtx.Plugins.Add(MakePublicPlugin("alice", downloadCount: 100));
        seedCtx.Plugins.Add(MakePublicPlugin("bob", downloadCount: 300));
        // Private plugin download count must NOT be included
        seedCtx.Plugins.Add(MakePrivatePlugin(orgId, "carol", downloadCount: 999));
        await seedCtx.SaveChangesAsync();

        await using MarketplaceDbContext queryCtx = _fixture.CreateContext();
        MarketplaceStatsAdapter adapter = new(queryCtx);

        // Act
        MarketplaceStatsDto stats = await adapter.GetStatsAsync();

        // Assert — 100 + 300 = 400; private 999 excluded
        Assert.Equal(400L, stats.TotalDownloads);
    }

    [Fact]
    public async Task GetStatsAsync_NoDownloads_TotalDownloadsIsZero()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        ctx.Plugins.Add(MakePublicPlugin("alice", downloadCount: 0));
        await ctx.SaveChangesAsync();

        await using MarketplaceDbContext queryCtx = _fixture.CreateContext();
        MarketplaceStatsAdapter adapter = new(queryCtx);

        // Act
        MarketplaceStatsDto stats = await adapter.GetStatsAsync();

        // Assert
        Assert.Equal(0L, stats.TotalDownloads);
    }

    // -------------------------------------------------------------------------
    // publisherCount: DISTINCT authors across public plugins
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetStatsAsync_SameAuthorMultiplePublicPlugins_PublisherCountIsDistinct()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        ctx.Plugins.Add(MakePublicPlugin("alice"));
        ctx.Plugins.Add(MakePublicPlugin("alice")); // same author, second plugin
        ctx.Plugins.Add(MakePublicPlugin("bob"));
        await ctx.SaveChangesAsync();

        await using MarketplaceDbContext queryCtx = _fixture.CreateContext();
        MarketplaceStatsAdapter adapter = new(queryCtx);

        // Act
        MarketplaceStatsDto stats = await adapter.GetStatsAsync();

        // Assert — 2 distinct authors (alice, bob), not 3
        Assert.Equal(2L, stats.PublisherCount);
    }

    [Fact]
    public async Task GetStatsAsync_PrivatePluginWithUniqueAuthor_PrivateAuthorExcludedFromCount()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        Guid orgId = await SeedOrgAsync(ctx);

        await using MarketplaceDbContext seedCtx = _fixture.CreateContext();
        seedCtx.Plugins.Add(MakePublicPlugin("alice"));
        // carol only has a private plugin — should NOT be counted
        seedCtx.Plugins.Add(MakePrivatePlugin(orgId, "carol"));
        await seedCtx.SaveChangesAsync();

        await using MarketplaceDbContext queryCtx = _fixture.CreateContext();
        MarketplaceStatsAdapter adapter = new(queryCtx);

        // Act
        MarketplaceStatsDto stats = await adapter.GetStatsAsync();

        // Assert — only alice counted; carol excluded
        Assert.Equal(1L, stats.PublisherCount);
    }

    // -------------------------------------------------------------------------
    // categoryCount: COUNT of rows in categories table (all categories, not just used ones)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetStatsAsync_WithCategories_CategoryCountMatchesTableRowCount()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        ctx.Categories.Add(MakeCategory("type", "skill"));
        ctx.Categories.Add(MakeCategory("type", "agent"));
        ctx.Categories.Add(MakeCategory("language", "typescript"));
        await ctx.SaveChangesAsync();

        await using MarketplaceDbContext queryCtx = _fixture.CreateContext();
        MarketplaceStatsAdapter adapter = new(queryCtx);

        // Act
        MarketplaceStatsDto stats = await adapter.GetStatsAsync();

        // Assert
        Assert.Equal(3L, stats.CategoryCount);
    }

    [Fact]
    public async Task GetStatsAsync_CategoriesWithNoPlugins_CategoryCountStillReturnsAllCategories()
    {
        // Arrange — categories exist, but no plugins at all
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        ctx.Categories.Add(MakeCategory("use_case", "dev-team"));
        ctx.Categories.Add(MakeCategory("use_case", "research"));
        await ctx.SaveChangesAsync();

        await using MarketplaceDbContext queryCtx = _fixture.CreateContext();
        MarketplaceStatsAdapter adapter = new(queryCtx);

        // Act
        MarketplaceStatsDto stats = await adapter.GetStatsAsync();

        // Assert
        Assert.Equal(2L, stats.CategoryCount);
        Assert.Equal(0L, stats.TotalPlugins);
    }

    // -------------------------------------------------------------------------
    // Combined scenario: full mix — validate all 4 fields in a single seeded DB
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetStatsAsync_FullMix_AllFourFieldsAreCorrect()
    {
        // Arrange
        await using MarketplaceDbContext setupCtx = _fixture.CreateContext();
        Guid orgId = await SeedOrgAsync(setupCtx);

        await using MarketplaceDbContext seedCtx = _fixture.CreateContext();
        // Public plugins: alice(50), alice(150), bob(100) → totalPlugins=3, totalDownloads=300, publisherCount=2
        seedCtx.Plugins.Add(MakePublicPlugin("alice", 50, "p1"));
        seedCtx.Plugins.Add(MakePublicPlugin("alice", 150, "p2"));
        seedCtx.Plugins.Add(MakePublicPlugin("bob", 100, "p3"));
        // Private plugin: does NOT count toward any aggregate
        seedCtx.Plugins.Add(MakePrivatePlugin(orgId, "carol", 9999));
        // Categories: 4 rows → categoryCount=4
        seedCtx.Categories.Add(MakeCategory("type", "skill"));
        seedCtx.Categories.Add(MakeCategory("type", "agent"));
        seedCtx.Categories.Add(MakeCategory("language", "python"));
        seedCtx.Categories.Add(MakeCategory("use_case", "research"));
        await seedCtx.SaveChangesAsync();

        await using MarketplaceDbContext queryCtx = _fixture.CreateContext();
        MarketplaceStatsAdapter adapter = new(queryCtx);

        // Act
        MarketplaceStatsDto stats = await adapter.GetStatsAsync();

        // Assert
        Assert.Equal(3L, stats.TotalPlugins);
        Assert.Equal(300L, stats.TotalDownloads);
        Assert.Equal(2L, stats.PublisherCount);
        Assert.Equal(4L, stats.CategoryCount);
    }
}
