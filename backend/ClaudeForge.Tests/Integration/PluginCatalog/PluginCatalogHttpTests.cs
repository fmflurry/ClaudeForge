using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClaudeForge.Core.Shared.Model;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using ClaudeForge.Infrastructure.Persistence.Seeding;
using ClaudeForge.Tests.Integration.Fixtures;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeForge.Tests.Integration.PluginCatalog;

/// <summary>
/// HTTP integration tests for Group 4: Plugin Catalog API endpoints.
///
/// Uses WebApplicationFactory&lt;Program&gt; with a real PostgreSQL 16 container.
/// Tests the full HTTP stack: routing, serialisation, exception mapping.
///
/// Endpoints under test:
///   GET /api/v1/plugins               — list with pagination, filter, sort
///   GET /api/v1/plugins/{pluginId}    — single plugin detail + version history
///   GET /api/v1/categories            — category list by dimension
/// </summary>
[Collection(PostgresFixture.CollectionName)]
public sealed class PluginCatalogHttpTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public PluginCatalogHttpTests(PostgresFixture fixture)
    {
        _fixture = fixture;

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace the DbContext registration with the test container connection
                    ServiceDescriptor? descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<MarketplaceDbContext>));
                    if (descriptor is not null)
                        services.Remove(descriptor);

                    // Also remove any DbContext registration
                    ServiceDescriptor? ctxDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(MarketplaceDbContext));
                    if (ctxDescriptor is not null)
                        services.Remove(ctxDescriptor);

                    services.AddDbContext<MarketplaceDbContext>(options =>
                        options.UseNpgsql(fixture.ConnectionString));
                });
            });

        _client = _factory.CreateClient();
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

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    // -------------------------------------------------------------------------
    // Helpers
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
        long downloadCount = 0) => new()
    {
        Id = Guid.NewGuid(),
        PluginId = pluginId,
        Version = version,
        VersionSort = versionSort,
        ReleaseNotes = $"Release notes for {version}",
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
    // GET /api/v1/plugins — 200 + paginated envelope shape
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetPlugins_EmptyDatabase_Returns200WithEmptyPaginatedEnvelope()
    {
        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/plugins");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        // Required envelope shape: data, totalCount, page, limit, totalPages
        Assert.True(root.TryGetProperty("data", out JsonElement data), "Response must have 'data' property");
        Assert.True(root.TryGetProperty("totalCount", out JsonElement totalCount), "Response must have 'totalCount' property");
        Assert.True(root.TryGetProperty("page", out JsonElement page), "Response must have 'page' property");
        Assert.True(root.TryGetProperty("limit", out JsonElement limit), "Response must have 'limit' property");
        Assert.True(root.TryGetProperty("totalPages", out JsonElement totalPages), "Response must have 'totalPages' property");

        Assert.Equal(JsonValueKind.Array, data.ValueKind);
        Assert.Equal(0, data.GetArrayLength());
        Assert.Equal(0, totalCount.GetInt32());
        Assert.Equal(1, page.GetInt32());
        Assert.Equal(20, limit.GetInt32());
        Assert.Equal(0, totalPages.GetInt32());
    }

    [Fact]
    public async Task GetPlugins_WithPlugins_Returns200WithPaginatedEnvelope()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        for (int i = 1; i <= 3; i++)
        {
            ctx.Plugins.Add(MakePlugin($"Plugin{i:D2}", $"plugin-http-{i:D2}"));
        }
        await ctx.SaveChangesAsync();

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/plugins?page=1&limit=20");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        Assert.Equal(3, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(20, root.GetProperty("limit").GetInt32());
        Assert.Equal(1, root.GetProperty("totalPages").GetInt32());
        Assert.Equal(3, root.GetProperty("data").GetArrayLength());
    }

    [Fact]
    public async Task GetPlugins_PaginationLimitAndPage_ReturnsCorrectSubset()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        DateTimeOffset baseTime = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        for (int i = 1; i <= 5; i++)
        {
            ctx.Plugins.Add(MakePlugin($"Plugin{i:D2}", $"plugin-page-{i:D2}",
                createdAt: baseTime.AddDays(i)));
        }
        await ctx.SaveChangesAsync();

        // Act — page 2, limit 2, sorted by createdAt desc
        HttpResponseMessage response = await _client.GetAsync("/api/v1/plugins?page=2&limit=2&sort=createdAt&order=desc");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        Assert.Equal(5, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, root.GetProperty("page").GetInt32());
        Assert.Equal(2, root.GetProperty("limit").GetInt32());
        Assert.Equal(3, root.GetProperty("totalPages").GetInt32()); // ceil(5/2) = 3
        Assert.Equal(2, root.GetProperty("data").GetArrayLength());
    }

    // -------------------------------------------------------------------------
    // GET /api/v1/plugins — filter + sort query params
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetPlugins_FilterByType_ReturnsOnlyMatchingPlugins()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        CategoryEntity typeSkill = MakeCategory("type", "skill", "Skill");
        CategoryEntity typeHook = MakeCategory("type", "hook", "Hook");
        ctx.Categories.AddRange(typeSkill, typeHook);
        await ctx.SaveChangesAsync();

        PluginEntity skillPlugin = MakePlugin("SkillHttpPlugin", "skill-http-filter");
        PluginEntity hookPlugin = MakePlugin("HookHttpPlugin", "hook-http-filter");
        ctx.Plugins.AddRange(skillPlugin, hookPlugin);
        await ctx.SaveChangesAsync();

        ctx.PluginCategories.Add(new PluginCategoryEntity { PluginId = skillPlugin.Id, CategoryId = typeSkill.Id });
        ctx.PluginCategories.Add(new PluginCategoryEntity { PluginId = hookPlugin.Id, CategoryId = typeHook.Id });
        await ctx.SaveChangesAsync();

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/plugins?type=skill");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        Assert.Equal(1, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, root.GetProperty("data").GetArrayLength());

        JsonElement firstPlugin = root.GetProperty("data")[0];
        Assert.Equal("SkillHttpPlugin", firstPlugin.GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetPlugins_SortByDownloadsDesc_ReturnsHighestDownloadsFirst()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        ctx.Plugins.Add(MakePlugin("LowPlugin", "low-http-dl", downloadCount: 5));
        ctx.Plugins.Add(MakePlugin("HighPlugin", "high-http-dl", downloadCount: 999));
        await ctx.SaveChangesAsync();

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/plugins?sort=downloads&order=desc");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement data = doc.RootElement.GetProperty("data");

        Assert.Equal(2, data.GetArrayLength());
        Assert.Equal("HighPlugin", data[0].GetProperty("name").GetString());
        Assert.Equal("LowPlugin", data[1].GetProperty("name").GetString());
    }

    // -------------------------------------------------------------------------
    // GET /api/v1/plugins/{id} — 200 detail + version history
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetPluginById_ExistingPlugin_Returns200WithDetailAndVersionHistory()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        PluginEntity plugin = MakePlugin("DetailPlugin", "detail-plugin-http");
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        long sort100 = new ClaudeForge.Core.Domain.Plugins.SemVer(1, 0, 0).ToVersionSort();
        long sort110 = new ClaudeForge.Core.Domain.Plugins.SemVer(1, 1, 0).ToVersionSort();

        ctx.PluginVersions.AddRange(
            MakeVersion(plugin.Id, "1.0.0", sort100, isLatest: false, downloadCount: 100),
            MakeVersion(plugin.Id, "1.1.0", sort110, isLatest: true, downloadCount: 25)
        );
        await ctx.SaveChangesAsync();

        // Act
        HttpResponseMessage response = await _client.GetAsync($"/api/v1/plugins/{plugin.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        Assert.Equal(plugin.Id.ToString(), root.GetProperty("id").GetString(), StringComparer.OrdinalIgnoreCase);
        Assert.Equal("DetailPlugin", root.GetProperty("name").GetString());

        // versions array — sorted semver desc
        JsonElement versions = root.GetProperty("versions");
        Assert.Equal(2, versions.GetArrayLength());
        Assert.Equal("1.1.0", versions[0].GetProperty("versionNumber").GetString());
        Assert.Equal("1.0.0", versions[1].GetProperty("versionNumber").GetString());
        Assert.True(versions[0].GetProperty("isLatest").GetBoolean());
        Assert.False(versions[1].GetProperty("isLatest").GetBoolean());
    }

    [Fact]
    public async Task GetPluginById_UnknownId_Returns404WithProblemDetails()
    {
        // Arrange
        Guid unknownId = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await _client.GetAsync($"/api/v1/plugins/{unknownId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        // RFC 7807 ProblemDetails shape
        Assert.True(root.TryGetProperty("detail", out JsonElement detail), "Response must have 'detail' property");
        Assert.Equal("Plugin not found", detail.GetString());
    }

    // -------------------------------------------------------------------------
    // GET /api/v1/categories — 200 {types, languages, useCases}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetCategories_EmptyDatabase_Returns200WithEmptyDimensions()
    {
        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/categories");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        Assert.True(root.TryGetProperty("types", out JsonElement types), "Response must have 'types' property");
        Assert.True(root.TryGetProperty("languages", out JsonElement languages), "Response must have 'languages' property");
        Assert.True(root.TryGetProperty("useCases", out JsonElement useCases), "Response must have 'useCases' property");

        Assert.Equal(JsonValueKind.Array, types.ValueKind);
        Assert.Equal(JsonValueKind.Array, languages.ValueKind);
        Assert.Equal(JsonValueKind.Array, useCases.ValueKind);
    }

    [Fact]
    public async Task GetCategories_WithSeededCategories_Returns200WithAllDimensions()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        ICategorySeeder seeder = new CategorySeeder(ctx);
        await seeder.SeedAsync();

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/categories");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        JsonElement types = root.GetProperty("types");
        JsonElement languages = root.GetProperty("languages");
        JsonElement useCases = root.GetProperty("useCases");

        // Seeded types: skill, hook, agent, command, plugin
        Assert.Equal(5, types.GetArrayLength());
        // Seeded languages: typescript, python, go, rust
        Assert.Equal(4, languages.GetArrayLength());
        // Seeded use-cases: dev-team, product-owner, product-manager, devops, security, data-analyst
        Assert.Equal(6, useCases.GetArrayLength());
    }

    // -------------------------------------------------------------------------
    // Validation: invalid sort/category/pagination → 400 ProblemDetails
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetPlugins_InvalidPaginationPageLessThan1_Returns400WithSpecExactDetail()
    {
        // Act — page=0 is invalid (must be >= 1)
        HttpResponseMessage response = await _client.GetAsync("/api/v1/plugins?page=0");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement detail = doc.RootElement.GetProperty("detail");

        Assert.Equal("Page must be greater than or equal to 1.", detail.GetString());
    }

    [Fact]
    public async Task GetPlugins_InvalidPaginationLimitOver100_Returns400WithSpecExactDetail()
    {
        // Act — limit=101 is invalid (must be 1–100)
        HttpResponseMessage response = await _client.GetAsync("/api/v1/plugins?limit=101");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement detail = doc.RootElement.GetProperty("detail");

        Assert.Equal("Limit must be between 1 and 100.", detail.GetString());
    }

    [Fact]
    public async Task GetPlugins_InvalidType_Returns400WithSpecExactDetail()
    {
        // Act — "widget" is not a valid type per spec
        HttpResponseMessage response = await _client.GetAsync("/api/v1/plugins?type=widget");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement detail = doc.RootElement.GetProperty("detail");

        // Spec: "Type must be one of: skill, hook, plugin, command, agent"
        Assert.Equal("Type must be one of: skill, hook, plugin, command, agent", detail.GetString());
    }

    [Fact]
    public async Task GetPlugins_InvalidLanguage_Returns400WithSpecExactDetail()
    {
        // Act — "FORTRAN" is not a valid language category per spec
        HttpResponseMessage response = await _client.GetAsync("/api/v1/plugins?language=FORTRAN");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement detail = doc.RootElement.GetProperty("detail");

        // Spec: "language 'FORTRAN' is not a valid category value"
        Assert.Equal("language 'FORTRAN' is not a valid category value", detail.GetString());
    }

    [Fact]
    public async Task GetPlugins_EmptyLanguageArray_Returns400WithSpecExactDetail()
    {
        // Act — an explicit empty language value violates "At least one language must be specified"
        HttpResponseMessage response = await _client.GetAsync("/api/v1/plugins?language=");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement detail = doc.RootElement.GetProperty("detail");

        // Spec: "At least one language must be specified"
        Assert.Equal("At least one language must be specified", detail.GetString());
    }

    [Fact]
    public async Task GetPlugins_DefaultParameters_OmittedPaginationUsesDefaults()
    {
        // Act — no page/limit specified
        HttpResponseMessage response = await _client.GetAsync("/api/v1/plugins");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        // Defaults: page=1, limit=20
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(20, root.GetProperty("limit").GetInt32());
    }

    [Fact]
    public async Task GetPluginById_PluginWithNoVersions_Returns200WithEmptyVersionsAndNullLatestVersion()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        PluginEntity plugin = MakePlugin("NoVersionHttp", "no-version-http");
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        // Act
        HttpResponseMessage response = await _client.GetAsync($"/api/v1/plugins/{plugin.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        Assert.Equal(JsonValueKind.Array, root.GetProperty("versions").ValueKind);
        Assert.Equal(0, root.GetProperty("versions").GetArrayLength());

        // latestVersion must be null (JSON null)
        Assert.Equal(JsonValueKind.Null, root.GetProperty("latestVersion").ValueKind);
    }
}
