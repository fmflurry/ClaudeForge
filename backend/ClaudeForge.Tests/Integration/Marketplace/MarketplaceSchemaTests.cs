using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using ClaudeForge.Infrastructure.Persistence.Seeding;
using ClaudeForge.Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ClaudeForge.Tests.Integration.Marketplace;

/// <summary>
/// Integration tests for Group 2: Database schema constraints and the category seeder.
///
/// These tests run against a REAL PostgreSQL 16 container via Testcontainers.
/// Docker must be running on the test host.  If Docker is unavailable the
/// PostgresFixture constructor will throw and xUnit will surface a clear failure.
///
/// Expected production types (coder must match these names exactly):
///
///   ClaudeForge.Infrastructure.Persistence.MarketplaceDbContext
///     DbSet&lt;PluginEntity&gt;         Plugins
///     DbSet&lt;PluginVersionEntity&gt;   PluginVersions
///     DbSet&lt;CategoryEntity&gt;        Categories
///     DbSet&lt;PluginCategoryEntity&gt;  PluginCategories
///     DbSet&lt;TelemetryEventEntity&gt;  TelemetryEvents
///     DbSet&lt;TelemetryAggregateEntity&gt; TelemetryAggregates
///
///   ClaudeForge.Infrastructure.Persistence.Entities.PluginEntity
///     Guid   Id
///     string Name
///     string NameNormalized   (UNIQUE, generated as lower(name) via EF value converter or DB trigger)
///     string Slug             (UNIQUE)
///     string Description
///     string Author
///     long   DownloadCount    default 0
///     string? SearchVector    (tsvector column, mapped as string; populated by DB trigger/generated col)
///     DateTimeOffset CreatedAt
///     DateTimeOffset UpdatedAt
///     ICollection&lt;PluginVersionEntity&gt; Versions
///     ICollection&lt;PluginCategoryEntity&gt; PluginCategories
///
///   ClaudeForge.Infrastructure.Persistence.Entities.PluginVersionEntity
///     Guid   Id
///     Guid   PluginId          (FK → plugins ON DELETE CASCADE)
///     string Version
///     long   VersionSort        (bigint sort key)
///     string ReleaseNotes       default ''
///     bool   IsLatest           default false
///     string PackageKey
///     string PackageFormat
///     long   SizeBytes
///     string Sha256
///     long   DownloadCount      default 0
///     string? ReadmeText
///     DateTimeOffset ReleasedAt
///     PluginEntity Plugin
///
///   ClaudeForge.Infrastructure.Persistence.Entities.CategoryEntity
///     short  Id
///     string Dimension    ('type' | 'language' | 'use_case')
///     string Value
///     string? DisplayName
///     string? Description
///     ICollection&lt;PluginCategoryEntity&gt; PluginCategories
///
///   ClaudeForge.Infrastructure.Persistence.Entities.PluginCategoryEntity
///     Guid  PluginId
///     short CategoryId
///     PluginEntity Plugin
///     CategoryEntity Category
///
///   ClaudeForge.Infrastructure.Persistence.Entities.TelemetryEventEntity
///     long   Id            (bigserial)
///     string EventType     ('download' | 'install')
///     Guid?  PluginId      (nullable FK → plugins ON DELETE SET NULL)
///     string? Version
///     string? AnonClientId  char(64)
///     string? ClientOs
///     string? ClientArch
///     DateTimeOffset OccurredAt
///
///   ClaudeForge.Infrastructure.Persistence.Entities.TelemetryAggregateEntity
///     Guid   PluginId
///     string Version        ('' = rollup)
///     string EventType
///     long   Count           default 0
///     DateOnly WindowStart
///
///   ClaudeForge.Infrastructure.Persistence.Seeding.ICategorySeeder
///     Task SeedAsync(CancellationToken cancellationToken = default)
///
///   ClaudeForge.Infrastructure.Persistence.Seeding.CategorySeeder
///     CategorySeeder(MarketplaceDbContext context)
///     implements ICategorySeeder
/// </summary>
[Collection(PostgresFixture.CollectionName)]
public sealed class MarketplaceSchemaTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    // Expected controlled-vocabulary counts
    private const int ExpectedTypeCount = 5;      // skill, hook, agent, command, plugin
    private const int ExpectedLanguageCount = 4;  // typescript, python, go, rust
    private const int ExpectedUseCaseCount = 6;   // dev-team, product-owner, product-manager, devops, security, data-analyst
    private const int ExpectedTotalCategoryCount = ExpectedTypeCount + ExpectedLanguageCount + ExpectedUseCaseCount;

    public MarketplaceSchemaTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // -------------------------------------------------------------------------
    // Per-test isolation: truncate all marketplace tables before each test so
    // rows inserted by earlier tests (e.g. categories in Test 5 / Test 6) do
    // not bleed into later tests (e.g. the seeder count assertion in Test 9).
    // RESTART IDENTITY resets sequences; CASCADE handles FK ordering.
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
    // Helper factories
    // -------------------------------------------------------------------------

    private static PluginEntity MakePlugin(string name, string slug) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        NameNormalized = name.ToLowerInvariant(),
        Slug = slug,
        Description = "Test description",
        Author = "test-author",
        DownloadCount = 0,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private static PluginVersionEntity MakeVersion(
        Guid pluginId,
        string version,
        long versionSort,
        bool isLatest = false) => new()
    {
        Id = Guid.NewGuid(),
        PluginId = pluginId,
        Version = version,
        VersionSort = versionSort,
        ReleaseNotes = "",
        IsLatest = isLatest,
        PackageKey = $"plugins/{pluginId}/{version}/package.tar.gz",
        PackageFormat = "tar.gz",
        SizeBytes = 1024,
        Sha256 = new string('a', 64),
        DownloadCount = 0,
        ReleasedAt = DateTimeOffset.UtcNow,
    };

    // -------------------------------------------------------------------------
    // Test 1 — names differing only by case → name_normalized UNIQUE violation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Insert_TwoPluginsWithSameCaseInsensitiveName_ThrowsUniqueViolation()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        PluginEntity first = MakePlugin("MyPlugin", "my-plugin-t1a");
        PluginEntity second = MakePlugin("myplugin", "my-plugin-t1b"); // different casing, same normalized

        ctx.Plugins.Add(first);
        await ctx.SaveChangesAsync();

        ctx.Plugins.Add(second);
        Exception ex = await Assert.ThrowsAnyAsync<Exception>(() => ctx.SaveChangesAsync());

        Assert.True(
            ex is DbUpdateException or PostgresException,
            $"Expected unique violation but got {ex.GetType().Name}: {ex.Message}");
    }

    // -------------------------------------------------------------------------
    // Test 2 — duplicate slug → UNIQUE violation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Insert_TwoPluginsWithSameSlug_ThrowsUniqueViolation()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        PluginEntity first = MakePlugin("PluginAlpha", "shared-slug-t2");
        PluginEntity second = MakePlugin("PluginBeta", "shared-slug-t2");

        ctx.Plugins.Add(first);
        await ctx.SaveChangesAsync();

        ctx.Plugins.Add(second);
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    // -------------------------------------------------------------------------
    // Test 3 — two plugin_versions with same (plugin_id, version) → UNIQUE violation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Insert_TwoVersionsWithSamePluginIdAndVersion_ThrowsUniqueViolation()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        PluginEntity plugin = MakePlugin("VersionDupPlugin", "version-dup-t3");
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        PluginVersionEntity v1 = MakeVersion(plugin.Id, "1.0.0", 1_000_000_000_000L);
        PluginVersionEntity v2 = MakeVersion(plugin.Id, "1.0.0", 1_000_000_000_000L); // same version

        ctx.PluginVersions.Add(v1);
        await ctx.SaveChangesAsync();

        ctx.PluginVersions.Add(v2);
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    // -------------------------------------------------------------------------
    // Test 4a — two versions for the SAME plugin both is_latest=true → partial UNIQUE violation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Insert_TwoLatestVersionsForSamePlugin_ThrowsPartialUniqueViolation()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        PluginEntity plugin = MakePlugin("DoubleLatestPlugin", "double-latest-t4a");
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        PluginVersionEntity v1 = MakeVersion(plugin.Id, "1.0.0", 1_000_000_000_000L, isLatest: true);
        PluginVersionEntity v2 = MakeVersion(plugin.Id, "1.1.0", 1_001_000_000_000L, isLatest: true);

        ctx.PluginVersions.Add(v1);
        await ctx.SaveChangesAsync();

        ctx.PluginVersions.Add(v2);
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    // -------------------------------------------------------------------------
    // Test 4b — two DIFFERENT plugins each having is_latest=true → ALLOWED
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Insert_LatestVersionForTwoDifferentPlugins_Succeeds()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        PluginEntity pluginA = MakePlugin("LatestPluginA", "latest-plugin-a-t4b");
        PluginEntity pluginB = MakePlugin("LatestPluginB", "latest-plugin-b-t4b");
        ctx.Plugins.AddRange(pluginA, pluginB);
        await ctx.SaveChangesAsync();

        PluginVersionEntity vA = MakeVersion(pluginA.Id, "1.0.0", 1_000_000_000_000L, isLatest: true);
        PluginVersionEntity vB = MakeVersion(pluginB.Id, "1.0.0", 1_000_000_000_000L, isLatest: true);
        ctx.PluginVersions.AddRange(vA, vB);

        // Must NOT throw
        await ctx.SaveChangesAsync();

        int count = await ctx.PluginVersions
            .Where(v => v.IsLatest && (v.PluginId == pluginA.Id || v.PluginId == pluginB.Id))
            .CountAsync();

        Assert.Equal(2, count);
    }

    // -------------------------------------------------------------------------
    // Test 5 — categories duplicate (dimension, value) → UNIQUE violation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Insert_DuplicateCategoryDimensionValue_ThrowsUniqueViolation()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        CategoryEntity cat1 = new() { Dimension = "type", Value = "skill-dup-t5", DisplayName = "Skill" };
        CategoryEntity cat2 = new() { Dimension = "type", Value = "skill-dup-t5", DisplayName = "Skill Duplicate" };

        ctx.Categories.Add(cat1);
        await ctx.SaveChangesAsync();

        ctx.Categories.Add(cat2);
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    // -------------------------------------------------------------------------
    // Test 6 — deleting a plugin cascades to plugin_versions and plugin_categories
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Delete_Plugin_CascadesToVersionsAndCategories()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        PluginEntity plugin = MakePlugin("CascadePlugin", "cascade-plugin-t6");
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        PluginVersionEntity version = MakeVersion(plugin.Id, "1.0.0", 1_000_000_000_000L, isLatest: true);
        ctx.PluginVersions.Add(version);

        CategoryEntity cat = new() { Dimension = "type", Value = "cascade-hook-t6", DisplayName = "Hook" };
        ctx.Categories.Add(cat);
        await ctx.SaveChangesAsync();

        PluginCategoryEntity link = new() { PluginId = plugin.Id, CategoryId = cat.Id };
        ctx.PluginCategories.Add(link);
        await ctx.SaveChangesAsync();

        // Delete the plugin — cascade should remove versions and category mappings
        ctx.Plugins.Remove(plugin);
        await ctx.SaveChangesAsync();

        bool versionExists = await ctx.PluginVersions.AnyAsync(v => v.PluginId == plugin.Id);
        bool categoryLinkExists = await ctx.PluginCategories.AnyAsync(pc => pc.PluginId == plugin.Id);

        Assert.False(versionExists, "plugin_versions must be deleted when the plugin is deleted");
        Assert.False(categoryLinkExists, "plugin_categories must be deleted when the plugin is deleted");
    }

    // -------------------------------------------------------------------------
    // Test 7 — search_vector is non-null after insert and contains name tokens
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Insert_Plugin_SearchVectorIsPopulatedWithNameTokens()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        PluginEntity plugin = MakePlugin("AsyncHelper", "async-helper-t7a");
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        // Re-read from DB to get server-generated value
        PluginEntity? persisted = await ctx.Plugins
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == plugin.Id);

        Assert.NotNull(persisted);
        Assert.NotNull(persisted.SearchVector);
        Assert.False(string.IsNullOrWhiteSpace(persisted.SearchVector),
            "search_vector should be non-empty after insert");

        // The vector must contain a token derived from the name "asynchelper"
        Assert.Contains("async", persisted.SearchVector, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_PluginNameAndDescription_SearchVectorIsRefreshed()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        PluginEntity plugin = MakePlugin("OldToolName", "old-tool-t7b");
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        // Update name and description — trigger or generated column should refresh search_vector
        PluginEntity? tracked = await ctx.Plugins.FindAsync(plugin.Id);
        Assert.NotNull(tracked);
        tracked.Name = "NewToolName";
        tracked.NameNormalized = "newtoolname";
        tracked.Description = "A brand new description with token";
        tracked.UpdatedAt = DateTimeOffset.UtcNow;
        await ctx.SaveChangesAsync();

        PluginEntity? refreshed = await ctx.Plugins
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == plugin.Id);

        Assert.NotNull(refreshed);
        Assert.NotNull(refreshed.SearchVector);
        // After update the old name token should be gone and new name present
        Assert.Contains("newtool", refreshed.SearchVector, StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------
    // Test 8 — version_sort orders versions correctly: 1.10.0 after 1.9.0
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Query_VersionsByVersionSort_OrdersCorrectly()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        PluginEntity plugin = MakePlugin("SortPlugin", "sort-plugin-t8");
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        // Use the SemVer value object to compute sort keys
        long sort190 = new ClaudeForge.Core.Domain.Plugins.SemVer(1, 9, 0).ToVersionSort();
        long sort1100 = new ClaudeForge.Core.Domain.Plugins.SemVer(1, 10, 0).ToVersionSort();
        long sort200 = new ClaudeForge.Core.Domain.Plugins.SemVer(2, 0, 0).ToVersionSort();

        PluginVersionEntity v190 = MakeVersion(plugin.Id, "1.9.0", sort190);
        PluginVersionEntity v1100 = MakeVersion(plugin.Id, "1.10.0", sort1100);
        PluginVersionEntity v200 = MakeVersion(plugin.Id, "2.0.0", sort200);

        ctx.PluginVersions.AddRange(v190, v1100, v200);
        await ctx.SaveChangesAsync();

        List<string> ordered = await ctx.PluginVersions
            .Where(v => v.PluginId == plugin.Id)
            .OrderByDescending(v => v.VersionSort)
            .Select(v => v.Version)
            .ToListAsync();

        Assert.Equal(new[] { "2.0.0", "1.10.0", "1.9.0" }, ordered);
    }

    // -------------------------------------------------------------------------
    // Test 9 — category seeder idempotency
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CategorySeeder_RunTwice_ProducesNoDuplicatesAndAllVocabExists()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        ICategorySeeder seeder = new CategorySeeder(ctx);

        // First run
        await seeder.SeedAsync();
        // Second run — must be idempotent
        await seeder.SeedAsync();

        int totalCount = await ctx.Categories.CountAsync();
        Assert.Equal(ExpectedTotalCategoryCount, totalCount);

        // Type dimension
        string[] expectedTypes = ["skill", "hook", "agent", "command", "plugin"];
        foreach (string value in expectedTypes)
        {
            bool exists = await ctx.Categories
                .AnyAsync(c => c.Dimension == "type" && c.Value == value);
            Assert.True(exists, $"Category type='{value}' must exist after seeding");
        }

        // Language dimension
        string[] expectedLanguages = ["typescript", "python", "go", "rust"];
        foreach (string value in expectedLanguages)
        {
            bool exists = await ctx.Categories
                .AnyAsync(c => c.Dimension == "language" && c.Value == value);
            Assert.True(exists, $"Category language='{value}' must exist after seeding");
        }

        // Use-case dimension
        string[] expectedUseCases =
        [
            "dev-team", "product-owner", "product-manager",
            "devops", "security", "data-analyst"
        ];
        foreach (string value in expectedUseCases)
        {
            bool exists = await ctx.Categories
                .AnyAsync(c => c.Dimension == "use_case" && c.Value == value);
            Assert.True(exists, $"Category use_case='{value}' must exist after seeding");
        }
    }

    // -------------------------------------------------------------------------
    // Test 10 — required-field / not-null enforcement
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Insert_PluginWithNullName_ThrowsException()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        // PluginEntity.Name is required (NOT NULL in DB).
        // EF Core will throw either a DbUpdateException (DB constraint) or
        // an InvalidOperationException before even hitting the DB if nullable annotations are correct.
        PluginEntity invalid = new()
        {
            Id = Guid.NewGuid(),
            Name = null!,   // violates NOT NULL
            NameNormalized = "null-name-t10",
            Slug = "null-name-slug-t10",
            Description = "Should fail",
            Author = "test",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        ctx.Plugins.Add(invalid);
        await Assert.ThrowsAnyAsync<Exception>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Insert_PluginVersionWithNullPackageKey_ThrowsException()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        PluginEntity plugin = MakePlugin("NotNullVersionPlugin", "not-null-version-t10");
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        PluginVersionEntity invalid = new()
        {
            Id = Guid.NewGuid(),
            PluginId = plugin.Id,
            Version = "1.0.0",
            VersionSort = 1_000_000_000_000L,
            PackageKey = null!,  // violates NOT NULL
            PackageFormat = "tar.gz",
            SizeBytes = 1024,
            Sha256 = new string('b', 64),
            ReleasedAt = DateTimeOffset.UtcNow,
        };

        ctx.PluginVersions.Add(invalid);
        await Assert.ThrowsAnyAsync<Exception>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Insert_PluginWithNullSlug_ThrowsException()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        PluginEntity invalid = new()
        {
            Id = Guid.NewGuid(),
            Name = "NullSlugPlugin",
            NameNormalized = "nullslugplugin",
            Slug = null!,  // violates NOT NULL
            Description = "Test",
            Author = "test",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        ctx.Plugins.Add(invalid);
        await Assert.ThrowsAnyAsync<Exception>(() => ctx.SaveChangesAsync());
    }
}
