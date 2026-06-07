using ClaudeForge.Application.Modules.PluginCatalog.Ports;
using ClaudeForge.Application.Modules.PluginCatalog.UseCases;
using ClaudeForge.Core.Shared.Model;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using ClaudeForge.Infrastructure.PluginCatalog;
using ClaudeForge.Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Tests.Integration.PluginCatalog;

/// <summary>
/// Integration tests for Group 4: IPluginRepositoryPort and ICategoryRepositoryPort adapters.
///
/// These tests run against a REAL PostgreSQL 16 container via Testcontainers.
/// Docker must be running on the test host.
///
/// Expected production types (coder must match these names exactly):
///
///   ClaudeForge.Infrastructure.PluginCatalog.PluginRepositoryAdapter
///     PluginRepositoryAdapter(MarketplaceDbContext context)
///     implements IPluginRepositoryPort, ICategoryRepositoryPort
///
///   ClaudeForge.Application.Modules.PluginCatalog.Ports.IPluginRepositoryPort
///   ClaudeForge.Application.Modules.PluginCatalog.Ports.ICategoryRepositoryPort
///
///   ClaudeForge.Application.Modules.PluginCatalog.UseCases.PluginSummaryDto
///   ClaudeForge.Application.Modules.PluginCatalog.UseCases.PluginDetailDto
///   ClaudeForge.Application.Modules.PluginCatalog.UseCases.PluginVersionDto
///   ClaudeForge.Application.Modules.PluginCatalog.UseCases.CategoryListDto
/// </summary>
[Collection(PostgresFixture.CollectionName)]
public sealed class PluginRepositoryPortTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public PluginRepositoryPortTests(PostgresFixture fixture)
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
    // Helper factories
    // -------------------------------------------------------------------------

    private static PluginEntity MakePlugin(
        string name,
        string slug,
        long downloadCount = 0,
        DateTimeOffset? createdAt = null) => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            NameNormalized = name.ToLowerInvariant(),
            Slug = slug,
            Description = $"Description for {name}",
            Author = "test-author",
            DownloadCount = downloadCount,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    private static PluginVersionEntity MakeVersion(
        Guid pluginId,
        string version,
        long versionSort,
        bool isLatest = false,
        long downloadCount = 0,
        string releaseNotes = "") => new()
        {
            Id = Guid.NewGuid(),
            PluginId = pluginId,
            Version = version,
            VersionSort = versionSort,
            ReleaseNotes = releaseNotes,
            IsLatest = isLatest,
            PackageKey = $"plugins/{pluginId}/{version}/package.tar.gz",
            PackageFormat = "tar.gz",
            SizeBytes = 1024,
            Sha256 = new string('a', 64),
            DownloadCount = downloadCount,
            ReleasedAt = DateTimeOffset.UtcNow,
        };

    private static CategoryEntity MakeCategory(string dimension, string value, string? displayName = null) => new()
    {
        Dimension = dimension,
        Value = value,
        DisplayName = displayName ?? value,
    };

    // -------------------------------------------------------------------------
    // Pagination: page/limit/total
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ListPluginsAsync_Pagination_ReturnsCorrectPageAndTotal()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        for (int i = 1; i <= 5; i++)
        {
            ctx.Plugins.Add(MakePlugin($"Plugin{i:D2}", $"plugin-{i:D2}"));
        }
        await ctx.SaveChangesAsync();

        IPluginRepositoryPort repo = new PluginRepositoryAdapter(ctx);
        PaginationRequest pagination = new() { Page = 2, Limit = 2 };

        // Act
        (IReadOnlyList<PluginSummaryDto> items, int totalCount) =
            await repo.ListPluginsAsync(pagination, "createdAt", "desc", null, null, null);

        // Assert
        Assert.Equal(5, totalCount);
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task ListPluginsAsync_PageBeyondRange_ReturnsEmptyItems()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        ctx.Plugins.Add(MakePlugin("OnlyPlugin", "only-plugin"));
        await ctx.SaveChangesAsync();

        IPluginRepositoryPort repo = new PluginRepositoryAdapter(ctx);
        PaginationRequest pagination = new() { Page = 100, Limit = 20 };

        // Act
        (IReadOnlyList<PluginSummaryDto> items, int totalCount) =
            await repo.ListPluginsAsync(pagination, "createdAt", "desc", null, null, null);

        // Assert
        Assert.Equal(1, totalCount);
        Assert.Empty(items);
    }

    // -------------------------------------------------------------------------
    // Sorting: downloads asc/desc, createdAt asc/desc, name asc/desc
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ListPluginsAsync_SortByDownloadsDesc_ReturnsHighestFirst()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        ctx.Plugins.Add(MakePlugin("LowDownloads", "low-dl", downloadCount: 10));
        ctx.Plugins.Add(MakePlugin("HighDownloads", "high-dl", downloadCount: 1000));
        ctx.Plugins.Add(MakePlugin("MidDownloads", "mid-dl", downloadCount: 500));
        await ctx.SaveChangesAsync();

        IPluginRepositoryPort repo = new PluginRepositoryAdapter(ctx);

        // Act
        (IReadOnlyList<PluginSummaryDto> items, _) =
            await repo.ListPluginsAsync(PaginationRequest.Default, "downloads", "desc", null, null, null);

        // Assert
        Assert.Equal(3, items.Count);
        Assert.Equal("HighDownloads", items[0].Name);
        Assert.Equal("MidDownloads", items[1].Name);
        Assert.Equal("LowDownloads", items[2].Name);
    }

    [Fact]
    public async Task ListPluginsAsync_SortByDownloadsAsc_ReturnsLowestFirst()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        ctx.Plugins.Add(MakePlugin("LowDownloads", "low-dl-asc", downloadCount: 10));
        ctx.Plugins.Add(MakePlugin("HighDownloads", "high-dl-asc", downloadCount: 1000));
        await ctx.SaveChangesAsync();

        IPluginRepositoryPort repo = new PluginRepositoryAdapter(ctx);

        // Act
        (IReadOnlyList<PluginSummaryDto> items, _) =
            await repo.ListPluginsAsync(PaginationRequest.Default, "downloads", "asc", null, null, null);

        // Assert
        Assert.Equal(2, items.Count);
        Assert.Equal("LowDownloads", items[0].Name);
        Assert.Equal("HighDownloads", items[1].Name);
    }

    [Fact]
    public async Task ListPluginsAsync_SortByCreatedAtAsc_ReturnsOldestFirst()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        DateTimeOffset older = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset newer = new(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);

        ctx.Plugins.Add(MakePlugin("NewerPlugin", "newer-plugin", createdAt: newer));
        ctx.Plugins.Add(MakePlugin("OlderPlugin", "older-plugin", createdAt: older));
        await ctx.SaveChangesAsync();

        IPluginRepositoryPort repo = new PluginRepositoryAdapter(ctx);

        // Act
        (IReadOnlyList<PluginSummaryDto> items, _) =
            await repo.ListPluginsAsync(PaginationRequest.Default, "createdAt", "asc", null, null, null);

        // Assert
        Assert.Equal(2, items.Count);
        Assert.Equal("OlderPlugin", items[0].Name);
        Assert.Equal("NewerPlugin", items[1].Name);
    }

    [Fact]
    public async Task ListPluginsAsync_SortByCreatedAtDesc_ReturnsNewestFirst()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        DateTimeOffset older = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset newer = new(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);

        ctx.Plugins.Add(MakePlugin("OlderPlugin", "older-plugin-desc", createdAt: older));
        ctx.Plugins.Add(MakePlugin("NewerPlugin", "newer-plugin-desc", createdAt: newer));
        await ctx.SaveChangesAsync();

        IPluginRepositoryPort repo = new PluginRepositoryAdapter(ctx);

        // Act
        (IReadOnlyList<PluginSummaryDto> items, _) =
            await repo.ListPluginsAsync(PaginationRequest.Default, "createdAt", "desc", null, null, null);

        // Assert
        Assert.Equal(2, items.Count);
        Assert.Equal("NewerPlugin", items[0].Name);
        Assert.Equal("OlderPlugin", items[1].Name);
    }

    [Fact]
    public async Task ListPluginsAsync_SortByNameAsc_ReturnsAlphabetically()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        ctx.Plugins.Add(MakePlugin("Zebra", "zebra-plugin"));
        ctx.Plugins.Add(MakePlugin("Alpha", "alpha-plugin"));
        ctx.Plugins.Add(MakePlugin("Mango", "mango-plugin"));
        await ctx.SaveChangesAsync();

        IPluginRepositoryPort repo = new PluginRepositoryAdapter(ctx);

        // Act
        (IReadOnlyList<PluginSummaryDto> items, _) =
            await repo.ListPluginsAsync(PaginationRequest.Default, "name", "asc", null, null, null);

        // Assert
        Assert.Equal(3, items.Count);
        Assert.Equal("Alpha", items[0].Name);
        Assert.Equal("Mango", items[1].Name);
        Assert.Equal("Zebra", items[2].Name);
    }

    [Fact]
    public async Task ListPluginsAsync_SortByNameDesc_ReturnsReverseAlphabetically()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        ctx.Plugins.Add(MakePlugin("Zebra", "zebra-plugin-desc"));
        ctx.Plugins.Add(MakePlugin("Alpha", "alpha-plugin-desc"));
        ctx.Plugins.Add(MakePlugin("Mango", "mango-plugin-desc"));
        await ctx.SaveChangesAsync();

        IPluginRepositoryPort repo = new PluginRepositoryAdapter(ctx);

        // Act
        (IReadOnlyList<PluginSummaryDto> items, _) =
            await repo.ListPluginsAsync(PaginationRequest.Default, "name", "desc", null, null, null);

        // Assert
        Assert.Equal(3, items.Count);
        Assert.Equal("Zebra", items[0].Name);
        Assert.Equal("Mango", items[1].Name);
        Assert.Equal("Alpha", items[2].Name);
    }

    // -------------------------------------------------------------------------
    // Category filter: AND across dimensions (type AND language AND useCase)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ListPluginsAsync_FilterByType_ReturnsOnlyMatchingPlugins()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        CategoryEntity typeSkill = MakeCategory("type", "skill", "Skill");
        CategoryEntity typeHook = MakeCategory("type", "hook", "Hook");
        ctx.Categories.AddRange(typeSkill, typeHook);
        await ctx.SaveChangesAsync();

        PluginEntity skillPlugin = MakePlugin("SkillPlugin", "skill-plugin-filter");
        PluginEntity hookPlugin = MakePlugin("HookPlugin", "hook-plugin-filter");
        ctx.Plugins.AddRange(skillPlugin, hookPlugin);
        await ctx.SaveChangesAsync();

        ctx.PluginCategories.Add(new PluginCategoryEntity { PluginId = skillPlugin.Id, CategoryId = typeSkill.Id });
        ctx.PluginCategories.Add(new PluginCategoryEntity { PluginId = hookPlugin.Id, CategoryId = typeHook.Id });
        await ctx.SaveChangesAsync();

        IPluginRepositoryPort repo = new PluginRepositoryAdapter(ctx);
        IReadOnlyList<string> typeFilter = ["skill"];

        // Act
        (IReadOnlyList<PluginSummaryDto> items, int totalCount) =
            await repo.ListPluginsAsync(PaginationRequest.Default, "name", "asc", typeFilter, null, null);

        // Assert
        Assert.Equal(1, totalCount);
        Assert.Single(items);
        Assert.Equal("SkillPlugin", items[0].Name);
    }

    [Fact]
    public async Task ListPluginsAsync_FilterByTypeAndLanguage_ReturnsIntersection()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        CategoryEntity typeSkill = MakeCategory("type", "skill", "Skill");
        CategoryEntity langTs = MakeCategory("language", "typescript", "TypeScript");
        CategoryEntity langPy = MakeCategory("language", "python", "Python");
        ctx.Categories.AddRange(typeSkill, langTs, langPy);
        await ctx.SaveChangesAsync();

        // Plugin A: skill + TypeScript (matches type=skill AND language=typescript)
        PluginEntity pluginA = MakePlugin("SkillTsPlugin", "skill-ts-plugin");
        // Plugin B: skill + Python (matches type=skill but NOT language=typescript)
        PluginEntity pluginB = MakePlugin("SkillPyPlugin", "skill-py-plugin");
        ctx.Plugins.AddRange(pluginA, pluginB);
        await ctx.SaveChangesAsync();

        ctx.PluginCategories.Add(new PluginCategoryEntity { PluginId = pluginA.Id, CategoryId = typeSkill.Id });
        ctx.PluginCategories.Add(new PluginCategoryEntity { PluginId = pluginA.Id, CategoryId = langTs.Id });
        ctx.PluginCategories.Add(new PluginCategoryEntity { PluginId = pluginB.Id, CategoryId = typeSkill.Id });
        ctx.PluginCategories.Add(new PluginCategoryEntity { PluginId = pluginB.Id, CategoryId = langPy.Id });
        await ctx.SaveChangesAsync();

        IPluginRepositoryPort repo = new PluginRepositoryAdapter(ctx);
        IReadOnlyList<string> typeFilter = ["skill"];
        IReadOnlyList<string> langFilter = ["typescript"];

        // Act
        (IReadOnlyList<PluginSummaryDto> items, int totalCount) =
            await repo.ListPluginsAsync(PaginationRequest.Default, "name", "asc", typeFilter, langFilter, null);

        // Assert — only Plugin A matches both dimensions
        Assert.Equal(1, totalCount);
        Assert.Single(items);
        Assert.Equal("SkillTsPlugin", items[0].Name);
    }

    [Fact]
    public async Task ListPluginsAsync_FilterWithMultipleValuesInSameDimension_UsesOrLogic()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        CategoryEntity langTs = MakeCategory("language", "typescript", "TypeScript");
        CategoryEntity langPy = MakeCategory("language", "python", "Python");
        CategoryEntity langGo = MakeCategory("language", "go", "Go");
        ctx.Categories.AddRange(langTs, langPy, langGo);
        await ctx.SaveChangesAsync();

        PluginEntity tsPlugin = MakePlugin("TsPlugin", "ts-plugin-or");
        PluginEntity pyPlugin = MakePlugin("PyPlugin", "py-plugin-or");
        PluginEntity goPlugin = MakePlugin("GoPlugin", "go-plugin-or");
        ctx.Plugins.AddRange(tsPlugin, pyPlugin, goPlugin);
        await ctx.SaveChangesAsync();

        ctx.PluginCategories.Add(new PluginCategoryEntity { PluginId = tsPlugin.Id, CategoryId = langTs.Id });
        ctx.PluginCategories.Add(new PluginCategoryEntity { PluginId = pyPlugin.Id, CategoryId = langPy.Id });
        ctx.PluginCategories.Add(new PluginCategoryEntity { PluginId = goPlugin.Id, CategoryId = langGo.Id });
        await ctx.SaveChangesAsync();

        IPluginRepositoryPort repo = new PluginRepositoryAdapter(ctx);
        // Filter for typescript OR python (OR within same dimension)
        IReadOnlyList<string> langFilter = ["typescript", "python"];

        // Act
        (IReadOnlyList<PluginSummaryDto> items, int totalCount) =
            await repo.ListPluginsAsync(PaginationRequest.Default, "name", "asc", null, langFilter, null);

        // Assert — Go plugin excluded, TS and Python included
        Assert.Equal(2, totalCount);
        Assert.Equal(2, items.Count);
        Assert.Contains(items, p => p.Name == "TsPlugin");
        Assert.Contains(items, p => p.Name == "PyPlugin");
    }

    // -------------------------------------------------------------------------
    // Duplicate name detection (case-insensitive)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExistsByNameNormalizedAsync_WhenNameExistsCaseInsensitive_ReturnsTrue()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        ctx.Plugins.Add(MakePlugin("MyPlugin", "my-plugin-norm"));
        await ctx.SaveChangesAsync();

        IPluginRepositoryPort repo = new PluginRepositoryAdapter(ctx);

        // Act — check with different casing
        bool existsLower = await repo.ExistsByNameNormalizedAsync("myplugin");
        bool existsUpper = await repo.ExistsByNameNormalizedAsync("MYPLUGIN");
        bool existsOriginal = await repo.ExistsByNameNormalizedAsync("myplugin");

        // Assert
        Assert.True(existsLower);
        Assert.True(existsUpper);
        Assert.True(existsOriginal);
    }

    [Fact]
    public async Task ExistsByNameNormalizedAsync_WhenNameDoesNotExist_ReturnsFalse()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        IPluginRepositoryPort repo = new PluginRepositoryAdapter(ctx);

        // Act
        bool exists = await repo.ExistsByNameNormalizedAsync("nonexistentplugin");

        // Assert
        Assert.False(exists);
    }

    // -------------------------------------------------------------------------
    // GetPluginByIdAsync: returns versions semver-desc with isLatest + per-version download counts
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetPluginByIdAsync_PluginWithVersions_ReturnsVersionsInSemVerDescOrder()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        PluginEntity plugin = MakePlugin("VersionedPlugin", "versioned-plugin-detail");
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        long sort100 = new ClaudeForge.Core.Domain.Plugins.SemVer(1, 0, 0).ToVersionSort();
        long sort110 = new ClaudeForge.Core.Domain.Plugins.SemVer(1, 1, 0).ToVersionSort();
        long sort200 = new ClaudeForge.Core.Domain.Plugins.SemVer(2, 0, 0).ToVersionSort();

        ctx.PluginVersions.AddRange(
            MakeVersion(plugin.Id, "1.0.0", sort100, isLatest: false, downloadCount: 100, releaseNotes: "Initial release"),
            MakeVersion(plugin.Id, "1.1.0", sort110, isLatest: false, downloadCount: 200, releaseNotes: "Minor update"),
            MakeVersion(plugin.Id, "2.0.0", sort200, isLatest: true, downloadCount: 50, releaseNotes: "Major release")
        );
        await ctx.SaveChangesAsync();

        IPluginRepositoryPort repo = new PluginRepositoryAdapter(ctx);

        // Act
        PluginDetailDto? result = await repo.GetPluginByIdAsync(plugin.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(plugin.Id, result.Id);
        Assert.Equal(3, result.Versions.Count);

        // Versions must be sorted semver descending (highest first)
        Assert.Equal("2.0.0", result.Versions[0].VersionNumber);
        Assert.Equal("1.1.0", result.Versions[1].VersionNumber);
        Assert.Equal("1.0.0", result.Versions[2].VersionNumber);

        // isLatest flag
        Assert.True(result.Versions[0].IsLatest);
        Assert.False(result.Versions[1].IsLatest);
        Assert.False(result.Versions[2].IsLatest);

        // Per-version download counts
        Assert.Equal(50L, result.Versions[0].DownloadCount);
        Assert.Equal(200L, result.Versions[1].DownloadCount);
        Assert.Equal(100L, result.Versions[2].DownloadCount);
    }

    [Fact]
    public async Task GetPluginByIdAsync_PluginWithNoVersions_ReturnsEmptyVersionsAndNullLatestVersion()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        PluginEntity plugin = MakePlugin("NoVersionPlugin", "no-version-plugin");
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        IPluginRepositoryPort repo = new PluginRepositoryAdapter(ctx);

        // Act
        PluginDetailDto? result = await repo.GetPluginByIdAsync(plugin.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Versions);
        Assert.Null(result.LatestVersion);
    }

    [Fact]
    public async Task GetPluginByIdAsync_UnknownId_ReturnsNull()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginRepositoryPort repo = new PluginRepositoryAdapter(ctx);

        // Act
        PluginDetailDto? result = await repo.GetPluginByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    // -------------------------------------------------------------------------
    // Plugin summary includes categories
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ListPluginsAsync_PluginWithCategories_SummaryIncludesTypesLanguagesUseCases()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        CategoryEntity typeSkill = MakeCategory("type", "skill", "Skill");
        CategoryEntity langTs = MakeCategory("language", "typescript", "TypeScript");
        CategoryEntity useCaseDevTeam = MakeCategory("use_case", "dev-team", "Development Team");
        ctx.Categories.AddRange(typeSkill, langTs, useCaseDevTeam);
        await ctx.SaveChangesAsync();

        PluginEntity plugin = MakePlugin("CategorizedPlugin", "categorized-plugin");
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        ctx.PluginCategories.AddRange(
            new PluginCategoryEntity { PluginId = plugin.Id, CategoryId = typeSkill.Id },
            new PluginCategoryEntity { PluginId = plugin.Id, CategoryId = langTs.Id },
            new PluginCategoryEntity { PluginId = plugin.Id, CategoryId = useCaseDevTeam.Id }
        );
        await ctx.SaveChangesAsync();

        IPluginRepositoryPort repo = new PluginRepositoryAdapter(ctx);

        // Act
        (IReadOnlyList<PluginSummaryDto> items, _) =
            await repo.ListPluginsAsync(PaginationRequest.Default, "name", "asc", null, null, null);

        // Assert
        Assert.Single(items);
        PluginSummaryDto summary = items[0];
        Assert.Contains("skill", summary.Types);
        Assert.Contains("typescript", summary.Languages);
        Assert.Contains("dev-team", summary.UseCases);
    }

    // -------------------------------------------------------------------------
    // GetAllCategoriesAsync: returns three dimensions with counts
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAllCategoriesAsync_WithSeededCategories_ReturnsAllThreeDimensions()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        ctx.Categories.AddRange(
            MakeCategory("type", "skill", "Skill"),
            MakeCategory("type", "hook", "Hook"),
            MakeCategory("language", "typescript", "TypeScript"),
            MakeCategory("use_case", "dev-team", "Development Team")
        );
        await ctx.SaveChangesAsync();

        ICategoryRepositoryPort repo = new PluginRepositoryAdapter(ctx);

        // Act
        CategoryListDto result = await repo.GetAllCategoriesAsync();

        // Assert
        Assert.Equal(2, result.Types.Count);
        Assert.Single(result.Languages);
        Assert.Single(result.UseCases);

        Assert.Contains(result.Types, c => c.Value == "skill");
        Assert.Contains(result.Types, c => c.Value == "hook");
        Assert.Contains(result.Languages, c => c.Value == "typescript");
        Assert.Contains(result.UseCases, c => c.Value == "dev-team");
    }

    [Fact]
    public async Task GetAllCategoriesAsync_EmptyDatabase_ReturnsEmptyLists()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        ICategoryRepositoryPort repo = new PluginRepositoryAdapter(ctx);

        // Act
        CategoryListDto result = await repo.GetAllCategoriesAsync();

        // Assert
        Assert.Empty(result.Types);
        Assert.Empty(result.Languages);
        Assert.Empty(result.UseCases);
    }

    [Fact]
    public async Task GetAllCategoriesAsync_CategoryWithPlugins_ReturnsNonZeroCount()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        CategoryEntity typeSkill = MakeCategory("type", "skill", "Skill");
        CategoryEntity typeHook = MakeCategory("type", "hook", "Hook");
        ctx.Categories.AddRange(typeSkill, typeHook);
        await ctx.SaveChangesAsync();

        PluginEntity plugin1 = MakePlugin("Skill1", "skill-plugin-count-1");
        PluginEntity plugin2 = MakePlugin("Skill2", "skill-plugin-count-2");
        ctx.Plugins.AddRange(plugin1, plugin2);
        await ctx.SaveChangesAsync();

        ctx.PluginCategories.Add(new PluginCategoryEntity { PluginId = plugin1.Id, CategoryId = typeSkill.Id });
        ctx.PluginCategories.Add(new PluginCategoryEntity { PluginId = plugin2.Id, CategoryId = typeSkill.Id });
        await ctx.SaveChangesAsync();

        ICategoryRepositoryPort repo = new PluginRepositoryAdapter(ctx);

        // Act
        CategoryListDto result = await repo.GetAllCategoriesAsync();

        // Assert
        CategoryDto? skillCategory = result.Types.FirstOrDefault(c => c.Value == "skill");
        CategoryDto? hookCategory = result.Types.FirstOrDefault(c => c.Value == "hook");

        Assert.NotNull(skillCategory);
        Assert.Equal(2, skillCategory.Count);

        Assert.NotNull(hookCategory);
        Assert.Equal(0, hookCategory.Count);
    }
}
