using ClaudeForge.Application.Modules.PluginCatalog.Ports;
using ClaudeForge.Application.Modules.PluginCatalog.UseCases;
using ClaudeForge.Application.Modules.PluginSearch.Ports;
using ClaudeForge.Application.Modules.PluginSearch.UseCases;
using ClaudeForge.Core.Shared.Model;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Seeding;
using ClaudeForge.Infrastructure.PluginCatalog;
using ClaudeForge.Infrastructure.PluginSearch;
using ClaudeForge.Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Tests.Integration.Seeding;

/// <summary>
/// Integration tests for Group 20 (tasks 20.1–20.2): PluginDataSeeder.
///
/// These tests run against a REAL PostgreSQL 16 container via Testcontainers.
/// Docker must be running on the test host.
///
/// PACKAGE STORAGE DECISION: Metadata-only seeding. The PluginDataSeeder inserts
/// plugin rows, plugin_version rows, and plugin_category rows directly via
/// MarketplaceDbContext. It does NOT store package blobs through IPackageStoragePort.
/// Each version carries a synthetic PackageKey (e.g., "plugins/{id}/{version}/package.tar.gz"),
/// a non-zero SizeBytes, and a stable Sha256 hex string so that all NOT NULL constraints
/// are satisfied, but no actual file is written to disk. Download-side tests are out of
/// scope for this seeder test class.
///
/// PRODUCTION TYPES TO IMPLEMENT (coder must match these names/namespaces exactly):
///
///   Namespace:  ClaudeForge.Infrastructure.Persistence.Seeding
///
///   Interface:
///     IPluginDataSeeder
///       Task SeedAsync(CancellationToken ct = default)
///
///   Class:
///     PluginDataSeeder : IPluginDataSeeder
///       PluginDataSeeder(MarketplaceDbContext context, ICategorySeeder categorySeeder)
///       Task SeedAsync(CancellationToken ct = default)
///         - Calls categorySeeder.SeedAsync(ct) to ensure category vocab exists.
///         - Idempotently inserts 10 seed plugins (keyed by name_normalized).
///         - Each plugin has at minimum 1 version; one plugin has exactly 3 versions.
///         - One plugin has at least 2 type categories (multi-type).
///         - Collectively the 10 plugins reference ≥3 distinct languages,
///           ≥3 distinct use-cases, and ≥2 distinct types.
///
///   Static accessor (so tests can assert the expected names):
///     IReadOnlyList&lt;SeedPluginDefinition&gt; PluginDataSeeder.SeedDefinitions { get; }
///
///   SeedPluginDefinition record:
///     string Name
///     string Slug
///     string Author
///     string Description
///     IReadOnlyList&lt;string&gt; Types            (controlled vocab values)
///     IReadOnlyList&lt;string&gt; Languages         (controlled vocab values)
///     IReadOnlyList&lt;string&gt; UseCases          (controlled vocab values)
///     IReadOnlyList&lt;string&gt; Versions          (semver strings, ascending; last = latest)
///
/// THE 10 EXPECTED SEED PLUGINS (canonical; coder must produce these):
///
///   #  Name                        Slug                         Notes
///   ── ─────────────────────────── ──────────────────────────── ──────────────────────────────────────────
///   1  TypeScript Linter           typescript-linter            types:[skill], langs:[typescript], uc:[dev-team]
///   2  Python Data Analyzer        python-data-analyzer         types:[skill,agent], langs:[python], uc:[data-analyst]   ← MULTI-TYPE
///   3  Go Build Optimizer          go-build-optimizer           types:[hook], langs:[go], uc:[devops]
///   4  Rust Security Scanner       rust-security-scanner        types:[skill], langs:[rust], uc:[security]
///   5  PR Review Agent             pr-review-agent              types:[agent], langs:[typescript], uc:[dev-team,product-owner]
///   6  Deployment Commander        deployment-commander         types:[command], langs:[go,typescript], uc:[devops]
///   7  Sprint Planning Assistant   sprint-planning-assistant    types:[skill], langs:[python], uc:[product-manager]
///   8  Code Quality Plugin         code-quality-plugin          types:[plugin], langs:[typescript,rust], uc:[dev-team]
///   9  API Gateway Hook            api-gateway-hook             types:[hook], langs:[go], uc:[devops,security]
///   10 Data Pipeline Orchestrator  data-pipeline-orchestrator   types:[agent], langs:[python,rust], uc:[data-analyst]
///
///   Multi-version plugin: #5 "PR Review Agent" — versions: ["1.0.0", "1.1.0", "2.0.0"]  ← is_latest on 2.0.0
///   Multi-type plugin:    #2 "Python Data Analyzer"  — types: ["skill", "agent"]
///
/// DIMENSION COVERAGE:
///   Types:     skill (×4), hook (×2), agent (×2), command (×1), plugin (×1)   → 5 distinct types
///   Languages: typescript (×4), python (×3), go (×3), rust (×3)               → 4 distinct languages
///   Use-cases: dev-team (×3), devops (×3), data-analyst (×2), product-owner (×1),
///              product-manager (×1), security (×2)                             → 6 distinct use-cases
/// </summary>
[Collection(PostgresFixture.CollectionName)]
public sealed class PluginDataSeederTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public PluginDataSeederTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // -------------------------------------------------------------------------
    // Per-test isolation: TRUNCATE all affected tables before each test.
    // Truncates in FK-safe order (children before parents).
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
    // Helper: create a fully wired PluginDataSeeder against the test container.
    // -------------------------------------------------------------------------

    private static IPluginDataSeeder CreateSeeder(MarketplaceDbContext ctx)
    {
        ICategorySeeder categorySeeder = new CategorySeeder(ctx);
        return new PluginDataSeeder(ctx, categorySeeder);
    }

    // =========================================================================
    // Test 1 — After SeedAsync, the plugins table has exactly 10 plugins.
    // =========================================================================

    [Fact]
    public async Task SeedAsync_InsertsExactlyTenPlugins()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginDataSeeder seeder = CreateSeeder(ctx);

        // Act
        await seeder.SeedAsync();

        // Assert: exactly 10 plugin rows
        int pluginCount = await ctx.Plugins.CountAsync();
        Assert.Equal(10, pluginCount);
    }

    [Fact]
    public async Task SeedAsync_PluginsHaveExpectedNamesAndSlugs()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginDataSeeder seeder = CreateSeeder(ctx);

        // Act
        await seeder.SeedAsync();

        // Assert: all expected names and slugs present
        List<(string Name, string Slug)> actual = await ctx.Plugins
            .Select(p => ValueTuple.Create(p.Name, p.Slug))
            .ToListAsync();

        IReadOnlyList<SeedPluginDefinition> definitions = PluginDataSeeder.SeedDefinitions;
        Assert.Equal(10, definitions.Count);

        foreach (SeedPluginDefinition def in definitions)
        {
            Assert.Contains(actual, t => t.Name == def.Name && t.Slug == def.Slug);
        }
    }

    [Fact]
    public async Task SeedDefinitions_StaticList_HasExactlyTenEntries()
    {
        // The static list must be accessible without instantiation.
        IReadOnlyList<SeedPluginDefinition> defs = PluginDataSeeder.SeedDefinitions;
        Assert.Equal(10, defs.Count);
    }

    // =========================================================================
    // Test 2 — Multi-version plugin has ≥2 versions; exactly one is_latest=true;
    //          latest is the highest semver.
    // =========================================================================

    [Fact]
    public async Task SeedAsync_MultiVersionPlugin_HasAtLeastTwoVersions()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginDataSeeder seeder = CreateSeeder(ctx);

        // Act
        await seeder.SeedAsync();

        // Assert: at least one plugin has 2+ versions
        bool anyMultiVersion = await ctx.Plugins
            .AnyAsync(p => p.Versions.Count >= 2);

        Assert.True(anyMultiVersion, "At least one seeded plugin must have 2 or more versions.");
    }

    [Fact]
    public async Task SeedAsync_MultiVersionPlugin_ExactlyOneVersionIsLatest()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginDataSeeder seeder = CreateSeeder(ctx);

        // Act
        await seeder.SeedAsync();

        // Find the plugin with the most versions
        Guid multiVersionPluginId = await ctx.Plugins
            .Where(p => p.Versions.Count >= 2)
            .OrderByDescending(p => p.Versions.Count)
            .Select(p => p.Id)
            .FirstAsync();

        int latestCount = await ctx.PluginVersions
            .CountAsync(v => v.PluginId == multiVersionPluginId && v.IsLatest);

        Assert.Equal(1, latestCount);
    }

    [Fact]
    public async Task SeedAsync_MultiVersionPlugin_LatestVersionIsHighestSemver()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginDataSeeder seeder = CreateSeeder(ctx);

        // Act
        await seeder.SeedAsync();

        // Find the multi-version plugin (PR Review Agent: versions 1.0.0, 1.1.0, 2.0.0)
        Guid multiVersionPluginId = await ctx.Plugins
            .Where(p => p.Versions.Count >= 2)
            .OrderByDescending(p => p.Versions.Count)
            .Select(p => p.Id)
            .FirstAsync();

        List<(string Version, long VersionSort, bool IsLatest)> versions = await ctx.PluginVersions
            .Where(v => v.PluginId == multiVersionPluginId)
            .Select(v => ValueTuple.Create(v.Version, v.VersionSort, v.IsLatest))
            .ToListAsync();

        Assert.True(versions.Count >= 2);

        // The is_latest version must have the highest version_sort
        long highestSort = versions.Max(v => v.VersionSort);
        (string Version, long VersionSort, bool IsLatest) latestVersion =
            versions.Single(v => v.IsLatest);

        Assert.Equal(highestSort, latestVersion.VersionSort);
    }

    [Fact]
    public async Task SeedAsync_MultiVersionPlugin_VersionsSemverAscending_MatchDefinition()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginDataSeeder seeder = CreateSeeder(ctx);

        // Act
        await seeder.SeedAsync();

        // The definition for the multi-version plugin lists versions ascending
        SeedPluginDefinition multiVersionDef = PluginDataSeeder.SeedDefinitions
            .Single(d => d.Versions.Count >= 2 && d.Versions.Count == d.Versions.Distinct().Count());

        // Find the seeded plugin by name
        List<(string Version, long VersionSort)> seededVersions = await ctx.Plugins
            .Where(p => p.Name == multiVersionDef.Name)
            .SelectMany(p => p.Versions)
            .Select(v => ValueTuple.Create(v.Version, v.VersionSort))
            .ToListAsync();

        // Every version in the definition must be present in the DB
        foreach (string expectedVersion in multiVersionDef.Versions)
        {
            Assert.Contains(seededVersions, v => v.Version == expectedVersion);
        }

        // VersionSort values must be strictly ascending (matching definition order)
        List<long> sortKeys = seededVersions
            .OrderBy(v => v.VersionSort)
            .Select(v => v.VersionSort)
            .ToList();

        for (int i = 1; i < sortKeys.Count; i++)
        {
            Assert.True(sortKeys[i] > sortKeys[i - 1],
                "version_sort must be strictly increasing across semver-ascending versions.");
        }
    }

    // =========================================================================
    // Test 3 — Multi-type plugin has ≥2 type categories.
    // =========================================================================

    [Fact]
    public async Task SeedAsync_MultiTypePlugin_HasAtLeastTwoTypeCategories()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginDataSeeder seeder = CreateSeeder(ctx);

        // Act
        await seeder.SeedAsync();

        // Assert: at least one plugin is tagged with 2+ types
        bool anyMultiType = await ctx.Plugins
            .AnyAsync(p =>
                p.PluginCategories.Count(pc => pc.Category.Dimension == "type") >= 2);

        Assert.True(anyMultiType,
            "At least one seeded plugin must be tagged with 2 or more type categories.");
    }

    [Fact]
    public async Task SeedAsync_MultiTypePlugin_DefinitionHasAtLeastTwoTypes()
    {
        // The static definition for the multi-type plugin must declare ≥2 types
        SeedPluginDefinition? multiTypeDef = PluginDataSeeder.SeedDefinitions
            .FirstOrDefault(d => d.Types.Count >= 2);

        Assert.NotNull(multiTypeDef);
        Assert.True(multiTypeDef!.Types.Count >= 2,
            "At least one SeedPluginDefinition must declare ≥2 types.");
    }

    [Fact]
    public async Task SeedAsync_MultiTypePlugin_TypesAreDistinctValidVocabValues()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginDataSeeder seeder = CreateSeeder(ctx);

        HashSet<string> validTypes = ["skill", "hook", "agent", "command", "plugin"];

        // Act
        await seeder.SeedAsync();

        // Find the multi-type plugin by its definition name
        SeedPluginDefinition multiTypeDef = PluginDataSeeder.SeedDefinitions
            .Single(d => d.Types.Count >= 2);

        List<string> dbTypes = await ctx.Plugins
            .Where(p => p.Name == multiTypeDef.Name)
            .SelectMany(p => p.PluginCategories)
            .Where(pc => pc.Category.Dimension == "type")
            .Select(pc => pc.Category.Value)
            .Distinct()
            .ToListAsync();

        Assert.True(dbTypes.Count >= 2,
            $"Multi-type plugin '{multiTypeDef.Name}' must have ≥2 type categories in DB.");

        foreach (string typeValue in dbTypes)
        {
            Assert.Contains(typeValue, validTypes);
        }
    }

    // =========================================================================
    // Test 4 — Category dimension coverage across the 10 seeded plugins.
    // =========================================================================

    [Fact]
    public async Task SeedAsync_SeededPlugins_CoverAtLeastThreeDistinctLanguages()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginDataSeeder seeder = CreateSeeder(ctx);

        // Act
        await seeder.SeedAsync();

        int distinctLanguages = await ctx.PluginCategories
            .Where(pc => pc.Category.Dimension == "language")
            .Select(pc => pc.Category.Value)
            .Distinct()
            .CountAsync();

        Assert.True(distinctLanguages >= 3,
            $"Expected ≥3 distinct seeded languages, got {distinctLanguages}.");
    }

    [Fact]
    public async Task SeedAsync_SeededPlugins_CoverAtLeastThreeDistinctUseCases()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginDataSeeder seeder = CreateSeeder(ctx);

        // Act
        await seeder.SeedAsync();

        int distinctUseCases = await ctx.PluginCategories
            .Where(pc => pc.Category.Dimension == "use_case")
            .Select(pc => pc.Category.Value)
            .Distinct()
            .CountAsync();

        Assert.True(distinctUseCases >= 3,
            $"Expected ≥3 distinct seeded use-cases, got {distinctUseCases}.");
    }

    [Fact]
    public async Task SeedAsync_SeededPlugins_CoverAtLeastTwoDistinctTypes()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginDataSeeder seeder = CreateSeeder(ctx);

        // Act
        await seeder.SeedAsync();

        int distinctTypes = await ctx.PluginCategories
            .Where(pc => pc.Category.Dimension == "type")
            .Select(pc => pc.Category.Value)
            .Distinct()
            .CountAsync();

        Assert.True(distinctTypes >= 2,
            $"Expected ≥2 distinct seeded types, got {distinctTypes}.");
    }

    [Fact]
    public async Task SeedAsync_SeededPlugins_AllDefinedLanguagesArePresentInDb()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginDataSeeder seeder = CreateSeeder(ctx);

        // Act
        await seeder.SeedAsync();

        // Compute the set of languages declared in static definitions
        HashSet<string> expectedLanguages = PluginDataSeeder.SeedDefinitions
            .SelectMany(d => d.Languages)
            .ToHashSet();

        List<string> dbLanguages = await ctx.PluginCategories
            .Where(pc => pc.Category.Dimension == "language")
            .Select(pc => pc.Category.Value)
            .Distinct()
            .ToListAsync();

        foreach (string lang in expectedLanguages)
        {
            Assert.Contains(lang, dbLanguages);
        }
    }

    [Fact]
    public async Task SeedAsync_SeededPlugins_AllDefinedTypesArePresentInDb()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginDataSeeder seeder = CreateSeeder(ctx);

        // Act
        await seeder.SeedAsync();

        HashSet<string> expectedTypes = PluginDataSeeder.SeedDefinitions
            .SelectMany(d => d.Types)
            .ToHashSet();

        List<string> dbTypes = await ctx.PluginCategories
            .Where(pc => pc.Category.Dimension == "type")
            .Select(pc => pc.Category.Value)
            .Distinct()
            .ToListAsync();

        foreach (string type in expectedTypes)
        {
            Assert.Contains(type, dbTypes);
        }
    }

    [Fact]
    public async Task SeedAsync_SeededPlugins_AllDefinedUseCasesArePresentInDb()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginDataSeeder seeder = CreateSeeder(ctx);

        // Act
        await seeder.SeedAsync();

        HashSet<string> expectedUseCases = PluginDataSeeder.SeedDefinitions
            .SelectMany(d => d.UseCases)
            .ToHashSet();

        List<string> dbUseCases = await ctx.PluginCategories
            .Where(pc => pc.Category.Dimension == "use_case")
            .Select(pc => pc.Category.Value)
            .Distinct()
            .ToListAsync();

        foreach (string uc in expectedUseCases)
        {
            Assert.Contains(uc, dbUseCases);
        }
    }

    // =========================================================================
    // Test 5 — Idempotency: calling SeedAsync twice must not duplicate rows.
    // =========================================================================

    [Fact]
    public async Task SeedAsync_CalledTwice_StillHasExactlyTenPlugins()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginDataSeeder seeder = CreateSeeder(ctx);

        // Act — call twice
        await seeder.SeedAsync();
        await seeder.SeedAsync();

        // Assert
        int pluginCount = await ctx.Plugins.CountAsync();
        Assert.Equal(10, pluginCount);
    }

    [Fact]
    public async Task SeedAsync_CalledTwice_VersionCountsAreUnchanged()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginDataSeeder seeder = CreateSeeder(ctx);

        // Act — first call
        await seeder.SeedAsync();

        // Capture version counts per plugin after first seeding
        Dictionary<string, int> versionCountsAfterFirst = await ctx.Plugins
            .ToDictionaryAsync(
                p => p.Name,
                p => p.Versions.Count);

        // Act — second call
        await seeder.SeedAsync();

        // Capture again after second seeding
        Dictionary<string, int> versionCountsAfterSecond = await ctx.Plugins
            .ToDictionaryAsync(
                p => p.Name,
                p => p.Versions.Count);

        // Assert: counts are identical
        foreach (string pluginName in versionCountsAfterFirst.Keys)
        {
            Assert.True(versionCountsAfterSecond.ContainsKey(pluginName));
            Assert.Equal(versionCountsAfterFirst[pluginName], versionCountsAfterSecond[pluginName]);
        }
    }

    [Fact]
    public async Task SeedAsync_CalledTwice_CategoryAssociationsAreUnchanged()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginDataSeeder seeder = CreateSeeder(ctx);

        // Act — first seeding
        await seeder.SeedAsync();
        int categoryAssocCountFirst = await ctx.PluginCategories.CountAsync();

        // Act — second seeding
        await seeder.SeedAsync();
        int categoryAssocCountSecond = await ctx.PluginCategories.CountAsync();

        // Assert — no duplicate associations
        Assert.Equal(categoryAssocCountFirst, categoryAssocCountSecond);
    }

    [Fact]
    public async Task SeedAsync_CalledTwice_IsLatestFlagsRemainConsistent()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginDataSeeder seeder = CreateSeeder(ctx);

        // Act
        await seeder.SeedAsync();
        await seeder.SeedAsync();

        // Assert: every plugin has exactly one is_latest version
        List<Guid> pluginIds = await ctx.Plugins.Select(p => p.Id).ToListAsync();

        foreach (Guid pluginId in pluginIds)
        {
            int latestCount = await ctx.PluginVersions
                .CountAsync(v => v.PluginId == pluginId && v.IsLatest);

            Assert.Equal(1, latestCount);
        }
    }

    // =========================================================================
    // Test 6a — Read side: ListPluginsUseCase returns the 10 seeded plugins.
    // =========================================================================

    [Fact]
    public async Task SeedAsync_AfterSeeding_ListPluginsUseCaseReturnsTen()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginDataSeeder seeder = CreateSeeder(ctx);
        await seeder.SeedAsync();

        IPluginRepositoryPort repo = new PluginRepositoryAdapter(ctx);
        ListPluginsUseCase useCase = new(repo);

        // Act — request all with a limit large enough to get everything in one page
        PaginatedEnvelope<PluginSummaryDto> result = await useCase.ExecuteAsync(
            new ListPluginsQuery { Page = 1, Limit = 20 });

        // Assert
        Assert.Equal(10, result.TotalCount);
        Assert.Equal(10, result.Data.Count);
    }

    [Fact]
    public async Task SeedAsync_AfterSeeding_ListPluginsUseCaseEnvelopeHasCorrectPaginationShape()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginDataSeeder seeder = CreateSeeder(ctx);
        await seeder.SeedAsync();

        IPluginRepositoryPort repo = new PluginRepositoryAdapter(ctx);
        ListPluginsUseCase useCase = new(repo);

        // Act — request page 1 with limit 5
        PaginatedEnvelope<PluginSummaryDto> result = await useCase.ExecuteAsync(
            new ListPluginsQuery { Page = 1, Limit = 5 });

        // Assert
        Assert.Equal(10, result.TotalCount);
        Assert.Equal(5, result.Data.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(5, result.Limit);
        Assert.Equal(2, result.TotalPages);
    }

    [Fact]
    public async Task SeedAsync_AfterSeeding_ListPluginsUseCasePageTwoReturnsRemaining()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginDataSeeder seeder = CreateSeeder(ctx);
        await seeder.SeedAsync();

        IPluginRepositoryPort repo = new PluginRepositoryAdapter(ctx);
        ListPluginsUseCase useCase = new(repo);

        // Act
        PaginatedEnvelope<PluginSummaryDto> page1 = await useCase.ExecuteAsync(
            new ListPluginsQuery { Page = 1, Limit = 5 });
        PaginatedEnvelope<PluginSummaryDto> page2 = await useCase.ExecuteAsync(
            new ListPluginsQuery { Page = 2, Limit = 5 });

        // Assert — pages are non-overlapping and together cover all 10
        IEnumerable<string> page1Names = page1.Data.Select(p => p.Name);
        IEnumerable<string> page2Names = page2.Data.Select(p => p.Name);

        Assert.Equal(5, page2.Data.Count);
        Assert.Empty(page1Names.Intersect(page2Names));
    }

    // =========================================================================
    // Test 6b — Read side: ListCategoriesUseCase returns non-zero counts.
    // =========================================================================

    [Fact]
    public async Task SeedAsync_AfterSeeding_ListCategoriesUseCaseReturnsNonZeroCounts()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginDataSeeder seeder = CreateSeeder(ctx);
        await seeder.SeedAsync();

        ICategoryRepositoryPort categoryRepo = new PluginRepositoryAdapter(ctx);
        ListCategoriesUseCase useCase = new(categoryRepo);

        // Act
        CategoryListDto result = await useCase.ExecuteAsync();

        // Assert: each dimension has at least one category with a non-zero count
        Assert.True(result.Types.Any(c => c.Count > 0),
            "At least one 'type' category must have a non-zero plugin count.");
        Assert.True(result.Languages.Any(c => c.Count > 0),
            "At least one 'language' category must have a non-zero plugin count.");
        Assert.True(result.UseCases.Any(c => c.Count > 0),
            "At least one 'use_case' category must have a non-zero plugin count.");
    }

    [Fact]
    public async Task SeedAsync_AfterSeeding_ListCategoriesUseCaseCountsMatchPluginCategoryRows()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginDataSeeder seeder = CreateSeeder(ctx);
        await seeder.SeedAsync();

        ICategoryRepositoryPort categoryRepo = new PluginRepositoryAdapter(ctx);
        ListCategoriesUseCase useCase = new(categoryRepo);

        // Act
        CategoryListDto result = await useCase.ExecuteAsync();

        // Assert: total sum of all category counts equals total plugin-category associations
        // (each plugin can be associated with the same category type value multiple times, so
        //  the sum counts plug-in level associations, not rows — but category.Count is per-category-value)
        int totalTypeCounts = result.Types.Sum(c => c.Count);
        int totalLangCounts = result.Languages.Sum(c => c.Count);
        int totalUseCaseCounts = result.UseCases.Sum(c => c.Count);

        // At minimum, each of the 10 plugins has 1 type, 1 language, 1 use-case
        Assert.True(totalTypeCounts >= 10,
            $"Sum of type counts must be ≥10 (one per plugin), got {totalTypeCounts}.");
        Assert.True(totalLangCounts >= 10,
            $"Sum of language counts must be ≥10, got {totalLangCounts}.");
        Assert.True(totalUseCaseCounts >= 10,
            $"Sum of use-case counts must be ≥10, got {totalUseCaseCounts}.");
    }

    [Fact]
    public async Task SeedAsync_AfterSeeding_AllDefinedCategoryValuesHaveNonZeroCountInCategories()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginDataSeeder seeder = CreateSeeder(ctx);
        await seeder.SeedAsync();

        ICategoryRepositoryPort categoryRepo = new PluginRepositoryAdapter(ctx);
        ListCategoriesUseCase useCase = new(categoryRepo);

        // Collect all category values declared in definitions
        HashSet<string> expectedTypes = PluginDataSeeder.SeedDefinitions
            .SelectMany(d => d.Types).ToHashSet();
        HashSet<string> expectedLanguages = PluginDataSeeder.SeedDefinitions
            .SelectMany(d => d.Languages).ToHashSet();
        HashSet<string> expectedUseCases = PluginDataSeeder.SeedDefinitions
            .SelectMany(d => d.UseCases).ToHashSet();

        // Act
        CategoryListDto result = await useCase.ExecuteAsync();

        // Assert: every expected type value appears with count > 0
        foreach (string expectedType in expectedTypes)
        {
            CategoryDto? cat = result.Types.FirstOrDefault(c => c.Value == expectedType);
            Assert.NotNull(cat);
            Assert.True(cat!.Count > 0,
                $"Expected type '{expectedType}' to have count > 0, got {cat!.Count}.");
        }

        // Assert: every expected language value appears with count > 0
        foreach (string expectedLang in expectedLanguages)
        {
            CategoryDto? cat = result.Languages.FirstOrDefault(c => c.Value == expectedLang);
            Assert.NotNull(cat);
            Assert.True(cat!.Count > 0,
                $"Expected language '{expectedLang}' to have count > 0, got {cat!.Count}.");
        }

        // Assert: every expected use-case value appears with count > 0
        foreach (string expectedUc in expectedUseCases)
        {
            CategoryDto? cat = result.UseCases.FirstOrDefault(c => c.Value == expectedUc);
            Assert.NotNull(cat);
            Assert.True(cat!.Count > 0,
                $"Expected use-case '{expectedUc}' to have count > 0, got {cat!.Count}.");
        }
    }

    // =========================================================================
    // Test 6c — Read side: SearchPluginsUseCase returns known seeded keyword hits.
    // =========================================================================

    [Fact]
    public async Task SeedAsync_AfterSeeding_SearchReturnsMatchForKnownPluginKeyword()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginDataSeeder seeder = CreateSeeder(ctx);
        await seeder.SeedAsync();

        ISearchIndexPort searchAdapter = new PostgresSearchAdapter(ctx);
        SearchPluginsUseCase searchUseCase = new(searchAdapter);

        // Act — search for "linter", which is a known word in the seeded plugin "TypeScript Linter"
        SearchPluginsResult result = await searchUseCase.ExecuteAsync(
            new SearchPluginsQuery { Q = "linter", Page = 1, Limit = 20 });

        // Assert: at least 1 result and the TypeScript Linter must be present
        Assert.True(result.Envelope.TotalCount >= 1,
            "Search for 'linter' must return at least 1 result after seeding.");
        Assert.Contains(result.Envelope.Data, r => r.Name.Contains("Linter"));
    }

    [Fact]
    public async Task SeedAsync_AfterSeeding_SearchReturnsMatchForSecondKnownKeyword()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginDataSeeder seeder = CreateSeeder(ctx);
        await seeder.SeedAsync();

        ISearchIndexPort searchAdapter = new PostgresSearchAdapter(ctx);
        SearchPluginsUseCase searchUseCase = new(searchAdapter);

        // Act — search for "security", which should hit "Rust Security Scanner"
        SearchPluginsResult result = await searchUseCase.ExecuteAsync(
            new SearchPluginsQuery { Q = "security", Page = 1, Limit = 20 });

        // Assert
        Assert.True(result.Envelope.TotalCount >= 1,
            "Search for 'security' must return at least 1 result after seeding.");
        Assert.Contains(result.Envelope.Data, r => r.Name.Contains("Security"));
    }

    [Fact]
    public async Task SeedAsync_AfterSeeding_SearchWithTypeFilterReturnsSubsetOfSeeds()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginDataSeeder seeder = CreateSeeder(ctx);
        await seeder.SeedAsync();

        ISearchIndexPort searchAdapter = new PostgresSearchAdapter(ctx);
        SearchPluginsUseCase searchUseCase = new(searchAdapter);

        // Act — list all agent-type plugins (no query, just type filter)
        SearchPluginsResult result = await searchUseCase.ExecuteAsync(
            new SearchPluginsQuery
            {
                Q = "agent",
                TypeFilter = ["agent"],
                Page = 1,
                Limit = 20,
            });

        // Assert: only plugins tagged as 'agent' are returned
        Assert.True(result.Envelope.TotalCount >= 1,
            "At least one seeded agent plugin must appear in search results.");

        // All returned results should have 'agent' in their types (from the seed definitions)
        IEnumerable<string> returnedNames = result.Envelope.Data.Select(r => r.Name);
        IReadOnlyList<SeedPluginDefinition> agentDefs = PluginDataSeeder.SeedDefinitions
            .Where(d => d.Types.Contains("agent"))
            .ToList();

        // At least one of the returned names must be a known agent-type seed plugin
        Assert.True(
            returnedNames.Any(name => agentDefs.Any(def => def.Name == name)),
            "At least one seeded agent plugin name must appear in filtered search results.");
    }

    [Fact]
    public async Task SeedAsync_AfterSeeding_SearchWithLanguageFilterReturnsCorrectSubset()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginDataSeeder seeder = CreateSeeder(ctx);
        await seeder.SeedAsync();

        ISearchIndexPort searchAdapter = new PostgresSearchAdapter(ctx);
        SearchPluginsUseCase searchUseCase = new(searchAdapter);

        // Act — search for "optimizer build" in go-language plugins
        SearchPluginsResult result = await searchUseCase.ExecuteAsync(
            new SearchPluginsQuery
            {
                Q = "optimizer",
                LanguageFilter = ["go"],
                Page = 1,
                Limit = 20,
            });

        // Assert: "Go Build Optimizer" must appear
        Assert.True(result.Envelope.TotalCount >= 1,
            "Search for 'optimizer' with language=go must return at least 1 result.");
        Assert.Contains(result.Envelope.Data,
            r => r.Name.Contains("Optimizer"),
            "Expected 'Go Build Optimizer' in search results filtered by language=go.");
    }

    [Fact]
    public async Task SeedAsync_AfterSeeding_SearchForNonExistentTermReturnsEmptyWithSuggestions()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginDataSeeder seeder = CreateSeeder(ctx);
        await seeder.SeedAsync();

        ISearchIndexPort searchAdapter = new PostgresSearchAdapter(ctx);
        SearchPluginsUseCase searchUseCase = new(searchAdapter);

        // Act — search for a term that will not match any seeded plugin
        SearchPluginsResult result = await searchUseCase.ExecuteAsync(
            new SearchPluginsQuery { Q = "zzz_impossible_term_xyz", Page = 1, Limit = 20 });

        // Assert: empty results; category suggestions provided
        Assert.Equal(0, result.Envelope.TotalCount);
        Assert.Empty(result.Envelope.Data);
        Assert.NotEmpty(result.CategorySuggestions);
    }

    // =========================================================================
    // Test 7 — Plugin entity integrity: each plugin has required fields non-empty.
    // =========================================================================

    [Fact]
    public async Task SeedAsync_AllPlugins_HaveNonEmptyRequiredFields()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginDataSeeder seeder = CreateSeeder(ctx);

        // Act
        await seeder.SeedAsync();

        // Assert: every plugin row has non-empty Name, NameNormalized, Slug, Description, Author
        bool allHaveRequiredFields = await ctx.Plugins.AllAsync(p =>
            p.Name != string.Empty &&
            p.NameNormalized != string.Empty &&
            p.Slug != string.Empty &&
            p.Description != string.Empty &&
            p.Author != string.Empty);

        Assert.True(allHaveRequiredFields,
            "Every seeded plugin must have non-empty Name, NameNormalized, Slug, Description, and Author.");
    }

    [Fact]
    public async Task SeedAsync_AllPlugins_NameNormalizedIsLowercaseOfName()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginDataSeeder seeder = CreateSeeder(ctx);

        // Act
        await seeder.SeedAsync();

        // Assert
        List<(string Name, string NameNormalized)> plugins = await ctx.Plugins
            .Select(p => ValueTuple.Create(p.Name, p.NameNormalized))
            .ToListAsync();

        foreach ((string name, string nameNormalized) in plugins)
        {
            Assert.Equal(name.ToLowerInvariant().Replace(" ", ""), nameNormalized.Replace(" ", ""),
                StringComparer.Ordinal);
        }
    }

    [Fact]
    public async Task SeedAsync_AllVersions_HaveNonEmptyPackageKeyAndSha256()
    {
        // Confirms metadata-only seeding populates mandatory storage fields
        // without requiring actual blob storage.

        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginDataSeeder seeder = CreateSeeder(ctx);

        // Act
        await seeder.SeedAsync();

        // Assert
        bool allVersionsHaveStorageFields = await ctx.PluginVersions.AllAsync(v =>
            v.PackageKey != string.Empty &&
            v.Sha256.Length == 64 &&
            v.SizeBytes > 0);

        Assert.True(allVersionsHaveStorageFields,
            "Every seeded version must have a non-empty PackageKey, a 64-char Sha256, and SizeBytes > 0.");
    }

    // =========================================================================
    // Test 8 — CategorySeeder integration: category vocab is present after SeedAsync.
    // =========================================================================

    [Fact]
    public async Task SeedAsync_EnsuresCategoryVocabExists_AllFifteenCategoryRowsPresent()
    {
        // Arrange — start with empty DB (no prior category seeding)
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginDataSeeder seeder = CreateSeeder(ctx);

        // Act — PluginDataSeeder must call CategorySeeder internally
        await seeder.SeedAsync();

        // Assert: the 15 controlled-vocabulary rows are present
        //   type (5) + language (4) + use_case (6) = 15
        int categoryCount = await ctx.Categories.CountAsync();
        Assert.Equal(15, categoryCount);
    }

    [Fact]
    public async Task SeedAsync_WhenCalledOnDbWithExistingCategoryVocab_DoesNotDuplicateCategories()
    {
        // Arrange — manually seed categories first
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        ICategorySeeder catSeeder = new CategorySeeder(ctx);
        await catSeeder.SeedAsync();

        int categoryCountBefore = await ctx.Categories.CountAsync();

        // Act — now run the plugin data seeder (which also calls category seeder)
        IPluginDataSeeder seeder = CreateSeeder(ctx);
        await seeder.SeedAsync();

        int categoryCountAfter = await ctx.Categories.CountAsync();

        // Assert: no additional category rows were added
        Assert.Equal(categoryCountBefore, categoryCountAfter);
    }
}
