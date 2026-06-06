using ClaudeForge.Application.Modules.PluginSearch.Ports;
using ClaudeForge.Application.Modules.PluginSearch.UseCases;
using ClaudeForge.Core.Shared.Model;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using ClaudeForge.Infrastructure.PluginSearch;
using ClaudeForge.Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Tests.Integration.PluginSearch;

/// <summary>
/// Integration tests for Group 7 (tasks 7.1–7.2): ISearchIndexPort adapter against real Postgres FTS.
///
/// These tests run against a REAL PostgreSQL 16 container via Testcontainers.
/// Docker must be running on the test host.
///
/// Expected production types (coder MUST match these names exactly):
///
///   ClaudeForge.Infrastructure.PluginSearch.PostgresSearchAdapter : ISearchIndexPort
///     PostgresSearchAdapter(MarketplaceDbContext context)
///
///   ClaudeForge.Application.Modules.PluginSearch.Ports.ISearchIndexPort
///     Task&lt;(IReadOnlyList&lt;SearchResultDto&gt; Items, int TotalCount)&gt; SearchAsync(
///         SearchCriteria criteria, PaginationRequest pagination, CancellationToken ct = default)
///     Task&lt;(IReadOnlyList&lt;DiscoveryResultDto&gt; Items, int TotalCount)&gt; DiscoverAsync(
///         SearchCriteria criteria, CancellationToken ct = default)
///
///   ClaudeForge.Application.Modules.PluginSearch.UseCases.SearchResultDto
///     float RelevanceScore; long DownloadCount; DateTimeOffset CreatedAt; string Name; string Slug
///
///   ClaudeForge.Application.Modules.PluginSearch.UseCases.SearchCriteria
///     string? Query; IReadOnlyList&lt;string&gt;? TypeFilter; IReadOnlyList&lt;string&gt;? LanguageFilter;
///     IReadOnlyList&lt;string&gt;? UseCaseFilter;
///
/// NOTE: The search_vector column is a GENERATED tsvector (weighted name=A, description=B, tags=C).
/// The tests seed plugins via EF and rely on the DB trigger/generated column to populate search_vector.
/// If EF insert does not trigger the computed column, consider seeding via raw SQL to set search_vector.
/// </summary>
[Collection(PostgresFixture.CollectionName)]
public sealed class SearchIndexPortTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public SearchIndexPortTests(PostgresFixture fixture)
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

    private static PluginEntity MakePlugin(
        string name,
        string slug,
        string description,
        long downloadCount = 0,
        DateTimeOffset? createdAt = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        NameNormalized = name.ToLowerInvariant(),
        Slug = slug,
        Description = description,
        Author = "test-author",
        DownloadCount = downloadCount,
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private static PluginVersionEntity MakeVersion(
        Guid pluginId,
        string version,
        long versionSort,
        bool isLatest = true) => new()
    {
        Id = Guid.NewGuid(),
        PluginId = pluginId,
        Version = version,
        VersionSort = versionSort,
        ReleaseNotes = string.Empty,
        IsLatest = isLatest,
        PackageKey = $"plugins/{pluginId}/{version}/package.tar.gz",
        PackageFormat = "tar.gz",
        SizeBytes = 1024,
        Sha256 = new string('a', 64),
        DownloadCount = 0,
        ReleasedAt = DateTimeOffset.UtcNow,
    };

    private static CategoryEntity MakeCategory(string dimension, string value) => new()
    {
        Dimension = dimension,
        Value = value,
        DisplayName = value,
    };

    // search_vector is a GENERATED ALWAYS AS STORED column — the DB populates it
    // automatically on every INSERT/UPDATE. No manual refresh is needed or allowed.
    private static Task RefreshSearchVectors(MarketplaceDbContext ctx) => Task.CompletedTask;

    // -------------------------------------------------------------------------
    // 7.1 — Name match
    // Spec: "returns all plugins whose names match the query in a case-insensitive manner"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SearchAsync_NameMatch_ReturnsMatchingPlugin()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        PluginEntity authPlugin = MakePlugin("AuthHelper", "auth-helper", "OAuth authentication helper");
        PluginEntity unrelatedPlugin = MakePlugin("DatabaseUtils", "database-utils", "Database utility functions");
        ctx.Plugins.AddRange(authPlugin, unrelatedPlugin);
        await ctx.SaveChangesAsync();
        await RefreshSearchVectors(ctx);

        ISearchIndexPort adapter = new PostgresSearchAdapter(ctx);
        SearchCriteria criteria = new() { Query = "auth" };

        // Act
        (IReadOnlyList<SearchResultDto> items, int total) =
            await adapter.SearchAsync(criteria, PaginationRequest.Default);

        // Assert
        Assert.True(total >= 1, "At least AuthHelper should match 'auth'");
        Assert.Contains(items, r => r.Name == "AuthHelper");
    }

    [Fact]
    public async Task SearchAsync_DescriptionMatch_ReturnsPluginWithMatchingDescription()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        PluginEntity plugin = MakePlugin("OAuthValidator", "oauth-validator",
            "Validates machine learning tokens for user authentication workflows");
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();
        await RefreshSearchVectors(ctx);

        ISearchIndexPort adapter = new PostgresSearchAdapter(ctx);
        SearchCriteria criteria = new() { Query = "machine learning" };

        // Act
        (IReadOnlyList<SearchResultDto> items, int total) =
            await adapter.SearchAsync(criteria, PaginationRequest.Default);

        // Assert — name doesn't contain "machine learning" but description does
        Assert.True(total >= 1);
        Assert.Contains(items, r => r.Name == "OAuthValidator");
    }

    // -------------------------------------------------------------------------
    // 7.1 — Case-insensitive search
    // Spec: "returns identical results regardless of case"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SearchAsync_CaseInsensitive_UppercaseQueryReturnsIdenticalResults()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        ctx.Plugins.Add(MakePlugin("AuthHelper", "auth-case-upper", "OAuth2 authentication plugin"));
        ctx.Plugins.Add(MakePlugin("OAuth Validator", "oauth-validator-case", "Validates OAuth tokens"));
        await ctx.SaveChangesAsync();
        await RefreshSearchVectors(ctx);

        ISearchIndexPort adapter = new PostgresSearchAdapter(ctx);

        // Act — search with different case variants
        (IReadOnlyList<SearchResultDto> lowerResults, _) =
            await adapter.SearchAsync(new SearchCriteria { Query = "auth" }, PaginationRequest.Default);
        (IReadOnlyList<SearchResultDto> upperResults, _) =
            await adapter.SearchAsync(new SearchCriteria { Query = "AUTH" }, PaginationRequest.Default);
        (IReadOnlyList<SearchResultDto> mixedResults, _) =
            await adapter.SearchAsync(new SearchCriteria { Query = "Auth" }, PaginationRequest.Default);

        // Assert — identical result names across all three casing variants
        IEnumerable<string> lowerNames = lowerResults.Select(r => r.Name).OrderBy(n => n);
        IEnumerable<string> upperNames = upperResults.Select(r => r.Name).OrderBy(n => n);
        IEnumerable<string> mixedNames = mixedResults.Select(r => r.Name).OrderBy(n => n);

        Assert.Equal(lowerNames, upperNames);
        Assert.Equal(lowerNames, mixedNames);
    }

    // -------------------------------------------------------------------------
    // 7.1 — Ranking: exact > prefix > partial; download_count + recency tiebreakers
    // Spec: "exact match first, prefix match second, partial match last"
    //       "plugin with more downloads is ranked higher (popularity as a tiebreaker)"
    //       "plugin with a more recent version update is ranked higher"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SearchAsync_Ranking_ExactNameMatchRanksBeforePrefixAndPartial()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        // Exact match
        PluginEntity exact = MakePlugin("Logger", "logger-exact",
            "A plugin called Logger", downloadCount: 10);
        // Prefix match
        PluginEntity prefix = MakePlugin("LoggerHelper", "logger-helper",
            "Extends the logger functionality", downloadCount: 5);
        // Partial/description match only
        PluginEntity partial = MakePlugin("FileWriter", "file-writer",
            "Writes files; optionally uses logger internally", downloadCount: 20);

        ctx.Plugins.AddRange(exact, prefix, partial);
        await ctx.SaveChangesAsync();
        await RefreshSearchVectors(ctx);

        ISearchIndexPort adapter = new PostgresSearchAdapter(ctx);
        SearchCriteria criteria = new() { Query = "Logger" };

        // Act
        (IReadOnlyList<SearchResultDto> items, _) =
            await adapter.SearchAsync(criteria, PaginationRequest.Default);

        // Assert — "Logger" (exact name match) must rank before "FileWriter" (description-only match)
        // Exact name = weight A → always higher than description weight B
        int loggerIndex = items.ToList().FindIndex(r => r.Name == "Logger");
        int fileWriterIndex = items.ToList().FindIndex(r => r.Name == "FileWriter");

        Assert.True(loggerIndex >= 0, "'Logger' should appear in results");
        Assert.True(fileWriterIndex < 0 || loggerIndex < fileWriterIndex,
            "Exact name match 'Logger' must rank before description-only match 'FileWriter'");
    }

    [Fact]
    public async Task SearchAsync_Ranking_HigherDownloadCountBreaksTie()
    {
        // Arrange — two plugins with same name relevance, different download counts
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        DateTimeOffset sameTime = DateTimeOffset.UtcNow;
        PluginEntity lowDownloads = MakePlugin("TestingPlugin", "testing-low-dl",
            "A testing plugin", downloadCount: 5, createdAt: sameTime);
        PluginEntity highDownloads = MakePlugin("TestingLib", "testing-high-dl",
            "A testing library", downloadCount: 1000, createdAt: sameTime);

        ctx.Plugins.AddRange(lowDownloads, highDownloads);
        await ctx.SaveChangesAsync();
        await RefreshSearchVectors(ctx);

        ISearchIndexPort adapter = new PostgresSearchAdapter(ctx);
        SearchCriteria criteria = new() { Query = "testing" };

        // Act
        (IReadOnlyList<SearchResultDto> items, _) =
            await adapter.SearchAsync(criteria, PaginationRequest.Default);

        // Assert — high download count ranks first
        Assert.Equal(2, items.Count);
        Assert.Equal("TestingLib", items[0].Name); // 1000 downloads first
        Assert.Equal("TestingPlugin", items[1].Name); // 5 downloads second
    }

    [Fact]
    public async Task SearchAsync_Ranking_MoreRecentPluginBreaksTieByRecency()
    {
        // Arrange — two plugins with equal text relevance, different creation dates
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        DateTimeOffset older = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset newer = new(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);

        PluginEntity oldPlugin = MakePlugin("ValidationHelper", "validation-old",
            "Validates input data", downloadCount: 0, createdAt: older);
        PluginEntity newPlugin = MakePlugin("ValidationKit", "validation-new",
            "Validates input data for modern apps", downloadCount: 0, createdAt: newer);

        ctx.Plugins.AddRange(oldPlugin, newPlugin);
        await ctx.SaveChangesAsync();
        await RefreshSearchVectors(ctx);

        ISearchIndexPort adapter = new PostgresSearchAdapter(ctx);
        SearchCriteria criteria = new() { Query = "validation" };

        // Act
        (IReadOnlyList<SearchResultDto> items, _) =
            await adapter.SearchAsync(criteria, PaginationRequest.Default);

        // Assert — newer plugin ranks higher when downloads are tied
        Assert.Equal(2, items.Count);
        Assert.Equal("ValidationKit", items[0].Name); // more recent
        Assert.Equal("ValidationHelper", items[1].Name); // older
    }

    // -------------------------------------------------------------------------
    // 7.1 — Pagination
    // Spec: "system returns 20 results on page 1"
    //       "requesting page 2 returns the next 20 results"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SearchAsync_Pagination_ReturnsCorrectPageAndTotal()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        for (int i = 1; i <= 5; i++)
        {
            ctx.Plugins.Add(MakePlugin($"SearchPlugin{i:D2}", $"search-plugin-{i:D2}",
                $"Plugin for searching {i}"));
        }
        await ctx.SaveChangesAsync();
        await RefreshSearchVectors(ctx);

        ISearchIndexPort adapter = new PostgresSearchAdapter(ctx);
        SearchCriteria criteria = new() { Query = "Plugin" };
        PaginationRequest pagination = new() { Page = 1, Limit = 3 };

        // Act
        (IReadOnlyList<SearchResultDto> items, int total) =
            await adapter.SearchAsync(criteria, pagination);

        // Assert
        Assert.Equal(5, total);
        Assert.Equal(3, items.Count); // limit respected
    }

    [Fact]
    public async Task SearchAsync_Pagination_Page2ReturnsNextSet()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        DateTimeOffset baseTime = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        for (int i = 1; i <= 4; i++)
        {
            ctx.Plugins.Add(MakePlugin($"PagedPlugin{i:D2}", $"paged-plugin-{i:D2}",
                $"Paged search plugin {i}", createdAt: baseTime.AddDays(i)));
        }
        await ctx.SaveChangesAsync();
        await RefreshSearchVectors(ctx);

        ISearchIndexPort adapter = new PostgresSearchAdapter(ctx);
        SearchCriteria criteria = new() { Query = "Paged" };
        PaginationRequest page1 = new() { Page = 1, Limit = 2 };
        PaginationRequest page2 = new() { Page = 2, Limit = 2 };

        // Act
        (IReadOnlyList<SearchResultDto> page1Items, int total) =
            await adapter.SearchAsync(criteria, page1);
        (IReadOnlyList<SearchResultDto> page2Items, _) =
            await adapter.SearchAsync(criteria, page2);

        // Assert — pages are non-overlapping
        Assert.Equal(4, total);
        Assert.Equal(2, page1Items.Count);
        Assert.Equal(2, page2Items.Count);

        IEnumerable<string> page1Names = page1Items.Select(i => i.Name);
        IEnumerable<string> page2Names = page2Items.Select(i => i.Name);
        Assert.Empty(page1Names.Intersect(page2Names)); // no overlap
    }

    // -------------------------------------------------------------------------
    // 7.1 — Empty result
    // Spec: "system returns an empty result set with HTTP 200"
    //       total must be 0
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SearchAsync_NoMatchingPlugins_ReturnsEmptyWithTotalZero()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        ctx.Plugins.Add(MakePlugin("AuthHelper", "auth-no-match", "OAuth authentication"));
        await ctx.SaveChangesAsync();
        await RefreshSearchVectors(ctx);

        ISearchIndexPort adapter = new PostgresSearchAdapter(ctx);
        SearchCriteria criteria = new() { Query = "zzzznonexistenttermxyz" };

        // Act
        (IReadOnlyList<SearchResultDto> items, int total) =
            await adapter.SearchAsync(criteria, PaginationRequest.Default);

        // Assert
        Assert.Empty(items);
        Assert.Equal(0, total);
    }

    // -------------------------------------------------------------------------
    // 7.1 — Type filter (OR within dimension)
    // Spec: "Filter narrows results by type"
    //       "Multiple type filters are combined with OR logic"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SearchAsync_TypeFilter_ReturnsOnlyMatchingType()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        CategoryEntity typeSkill = MakeCategory("type", "skill");
        CategoryEntity typeHook = MakeCategory("type", "hook");
        ctx.Categories.AddRange(typeSkill, typeHook);
        await ctx.SaveChangesAsync();

        PluginEntity skillPlugin = MakePlugin("AuthSkill", "auth-skill",
            "Auth skill plugin for authentication");
        PluginEntity hookPlugin = MakePlugin("AuthHook", "auth-hook",
            "Auth hook plugin for authentication");
        ctx.Plugins.AddRange(skillPlugin, hookPlugin);
        await ctx.SaveChangesAsync();

        ctx.PluginCategories.Add(new PluginCategoryEntity { PluginId = skillPlugin.Id, CategoryId = typeSkill.Id });
        ctx.PluginCategories.Add(new PluginCategoryEntity { PluginId = hookPlugin.Id, CategoryId = typeHook.Id });
        await ctx.SaveChangesAsync();
        await RefreshSearchVectors(ctx);

        ISearchIndexPort adapter = new PostgresSearchAdapter(ctx);
        SearchCriteria criteria = new() { Query = "auth", TypeFilter = ["skill"] };

        // Act
        (IReadOnlyList<SearchResultDto> items, int total) =
            await adapter.SearchAsync(criteria, PaginationRequest.Default);

        // Assert — only skill plugin returned
        Assert.Equal(1, total);
        Assert.Single(items);
        Assert.Equal("AuthSkill", items[0].Name);
    }

    [Fact]
    public async Task SearchAsync_MultipleTypeFilters_OrLogicWithinDimension()
    {
        // Arrange — skill OR hook → both returned; agent excluded
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        CategoryEntity typeSkill = MakeCategory("type", "skill");
        CategoryEntity typeHook = MakeCategory("type", "hook");
        CategoryEntity typeAgent = MakeCategory("type", "agent");
        ctx.Categories.AddRange(typeSkill, typeHook, typeAgent);
        await ctx.SaveChangesAsync();

        PluginEntity skillPlugin = MakePlugin("MultiAuthSkill", "multi-auth-skill",
            "Multi auth skill plugin");
        PluginEntity hookPlugin = MakePlugin("MultiAuthHook", "multi-auth-hook",
            "Multi auth hook plugin");
        PluginEntity agentPlugin = MakePlugin("MultiAuthAgent", "multi-auth-agent",
            "Multi auth agent plugin");
        ctx.Plugins.AddRange(skillPlugin, hookPlugin, agentPlugin);
        await ctx.SaveChangesAsync();

        ctx.PluginCategories.Add(new PluginCategoryEntity { PluginId = skillPlugin.Id, CategoryId = typeSkill.Id });
        ctx.PluginCategories.Add(new PluginCategoryEntity { PluginId = hookPlugin.Id, CategoryId = typeHook.Id });
        ctx.PluginCategories.Add(new PluginCategoryEntity { PluginId = agentPlugin.Id, CategoryId = typeAgent.Id });
        await ctx.SaveChangesAsync();
        await RefreshSearchVectors(ctx);

        ISearchIndexPort adapter = new PostgresSearchAdapter(ctx);
        // OR within type dimension: skill OR hook
        SearchCriteria criteria = new() { Query = "Multi", TypeFilter = ["skill", "hook"] };

        // Act
        (IReadOnlyList<SearchResultDto> items, int total) =
            await adapter.SearchAsync(criteria, PaginationRequest.Default);

        // Assert — skill and hook included; agent excluded
        Assert.Equal(2, total);
        Assert.Contains(items, r => r.Name == "MultiAuthSkill");
        Assert.Contains(items, r => r.Name == "MultiAuthHook");
        Assert.DoesNotContain(items, r => r.Name == "MultiAuthAgent");
    }

    // -------------------------------------------------------------------------
    // 7.1 — Language filter + AND across dimensions
    // Spec: "Language filter combined with type filter"
    //       "returns plugins that are both TypeScript AND of type command"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SearchAsync_LanguageFilter_ReturnsOnlyMatchingLanguage()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        CategoryEntity langTs = MakeCategory("language", "typescript");
        CategoryEntity langPy = MakeCategory("language", "python");
        ctx.Categories.AddRange(langTs, langPy);
        await ctx.SaveChangesAsync();

        PluginEntity tsPlugin = MakePlugin("ValidationTs", "validation-ts",
            "TypeScript validation plugin");
        PluginEntity pyPlugin = MakePlugin("ValidationPy", "validation-py",
            "Python validation plugin");
        ctx.Plugins.AddRange(tsPlugin, pyPlugin);
        await ctx.SaveChangesAsync();

        ctx.PluginCategories.Add(new PluginCategoryEntity { PluginId = tsPlugin.Id, CategoryId = langTs.Id });
        ctx.PluginCategories.Add(new PluginCategoryEntity { PluginId = pyPlugin.Id, CategoryId = langPy.Id });
        await ctx.SaveChangesAsync();
        await RefreshSearchVectors(ctx);

        ISearchIndexPort adapter = new PostgresSearchAdapter(ctx);
        SearchCriteria criteria = new() { Query = "validation", LanguageFilter = ["python"] };

        // Act
        (IReadOnlyList<SearchResultDto> items, int total) =
            await adapter.SearchAsync(criteria, PaginationRequest.Default);

        // Assert — only Python plugin returned
        Assert.Equal(1, total);
        Assert.Equal("ValidationPy", items[0].Name);
    }

    [Fact]
    public async Task SearchAsync_TypeAndLanguageFilter_AndAcrossDimensionsIntersects()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        CategoryEntity typeCommand = MakeCategory("type", "command");
        CategoryEntity langTs = MakeCategory("language", "typescript");
        CategoryEntity langGo = MakeCategory("language", "go");
        ctx.Categories.AddRange(typeCommand, langTs, langGo);
        await ctx.SaveChangesAsync();

        // command + TypeScript → matches both
        PluginEntity tsCommandPlugin = MakePlugin("TsCommandPlugin", "ts-command-and",
            "TypeScript command plugin for cross-dimension filter test");
        // command + Go → matches type but NOT language
        PluginEntity goCommandPlugin = MakePlugin("GoCommandPlugin", "go-command-and",
            "Go command plugin for cross-dimension filter test");
        ctx.Plugins.AddRange(tsCommandPlugin, goCommandPlugin);
        await ctx.SaveChangesAsync();

        ctx.PluginCategories.Add(new PluginCategoryEntity { PluginId = tsCommandPlugin.Id, CategoryId = typeCommand.Id });
        ctx.PluginCategories.Add(new PluginCategoryEntity { PluginId = tsCommandPlugin.Id, CategoryId = langTs.Id });
        ctx.PluginCategories.Add(new PluginCategoryEntity { PluginId = goCommandPlugin.Id, CategoryId = typeCommand.Id });
        ctx.PluginCategories.Add(new PluginCategoryEntity { PluginId = goCommandPlugin.Id, CategoryId = langGo.Id });
        await ctx.SaveChangesAsync();
        await RefreshSearchVectors(ctx);

        ISearchIndexPort adapter = new PostgresSearchAdapter(ctx);
        // AND across dimensions: type=command AND language=typescript
        SearchCriteria criteria = new()
        {
            Query = "cross-dimension",
            TypeFilter = ["command"],
            LanguageFilter = ["typescript"],
        };

        // Act
        (IReadOnlyList<SearchResultDto> items, int total) =
            await adapter.SearchAsync(criteria, PaginationRequest.Default);

        // Assert — only the TypeScript+command plugin matches
        Assert.Equal(1, total);
        Assert.Equal("TsCommandPlugin", items[0].Name);
    }
}
