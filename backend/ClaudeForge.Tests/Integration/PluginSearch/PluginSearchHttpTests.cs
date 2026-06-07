using System.Net;
using System.Text.Json;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using ClaudeForge.Tests.Integration.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeForge.Tests.Integration.PluginSearch;

/// <summary>
/// HTTP integration tests for Group 7 (tasks 7.5–7.7): PluginSearch API endpoints.
///
/// Uses WebApplicationFactory&lt;Program&gt; with a real PostgreSQL 16 container.
/// Tests the full HTTP stack: routing, serialisation, exception middleware.
///
/// Endpoints under test:
///   GET /api/v1/plugins/search?q=&amp;type=&amp;language=&amp;useCase=&amp;page=&amp;limit=
///   GET /api/v1/search?q=...    — thin alias, delegates to same use-case
///   GET /api/v1/discovery?keyword=&amp;language=&amp;useCase=&amp;type=
///
/// Verbatim spec error strings:
///   "Page and limit must be greater than 0"         (search invalid pagination)
///   "Keyword cannot be empty"                        (discovery blank keyword, spec verbatim)
///   "No plugins found matching your search"          (search empty result message — returned in envelope or response body)
///
/// Response shapes:
///   Search:    PaginatedEnvelope&lt;SearchResultDto&gt; with optional categorySuggestions on empty
///   Discovery: { items: [...], criteriaEchoed: [...] } or similar flat array
///              Each discovery item MUST have: id, name, description, relevanceScore, maturityIndicator,
///              types, languages, useCases, downloadCount, lastUpdated, author
/// </summary>
[Collection(PostgresFixture.CollectionName)]
public sealed class PluginSearchHttpTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public PluginSearchHttpTests(PostgresFixture fixture)
    {
        _fixture = fixture;

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    ServiceDescriptor? descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<MarketplaceDbContext>));
                    if (descriptor is not null)
                        services.Remove(descriptor);

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

    private static PluginVersionEntity MakeVersion(Guid pluginId, string version, long versionSort) => new()
    {
        Id = Guid.NewGuid(),
        PluginId = pluginId,
        Version = version,
        VersionSort = versionSort,
        ReleaseNotes = string.Empty,
        IsLatest = true,
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

    // =========================================================================
    // GET /api/v1/plugins/search
    // =========================================================================

    [Fact]
    public async Task SearchEndpoint_WithMatchingQuery_Returns200PaginatedEnvelope()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        ctx.Plugins.Add(MakePlugin("AuthHelper", "auth-search-http", "OAuth authentication helper plugin"));
        await ctx.SaveChangesAsync();
        await RefreshSearchVectors(ctx);

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/plugins/search?q=auth");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        // Paginated envelope shape
        Assert.True(root.TryGetProperty("data", out JsonElement data), "Response must have 'data' property");
        Assert.True(root.TryGetProperty("totalCount", out _), "Response must have 'totalCount' property");
        Assert.True(root.TryGetProperty("page", out _), "Response must have 'page' property");
        Assert.True(root.TryGetProperty("limit", out _), "Response must have 'limit' property");
        Assert.True(root.TryGetProperty("totalPages", out _), "Response must have 'totalPages' property");

        Assert.Equal(JsonValueKind.Array, data.ValueKind);
        Assert.True(data.GetArrayLength() >= 1, "Should return at least one matching plugin");
    }

    [Fact]
    public async Task SearchEndpoint_WithNoResults_Returns200WithEmptyDataAndCategorySuggestions()
    {
        // Arrange — seed one unrelated plugin
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        ctx.Plugins.Add(MakePlugin("UnrelatedPlugin", "unrelated-http", "Completely unrelated functionality"));
        await ctx.SaveChangesAsync();
        await RefreshSearchVectors(ctx);

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/plugins/search?q=zzz-nonexistent-term-xyz");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        Assert.Equal(0, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, root.GetProperty("data").GetArrayLength());

        // Spec: "result includes suggested categories or popular plugins to explore instead"
        Assert.True(root.TryGetProperty("categorySuggestions", out JsonElement suggestions),
            "Empty search result must include 'categorySuggestions'");
        Assert.Equal(JsonValueKind.Array, suggestions.ValueKind);
        Assert.True(suggestions.GetArrayLength() > 0,
            "categorySuggestions must be non-empty when no results found");
    }

    [Fact]
    public async Task SearchEndpoint_RankedResults_HigherDownloadsRankedFirst()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        ctx.Plugins.Add(MakePlugin("TestingLow", "testing-low-http",
            "Testing plugin with low downloads", downloadCount: 5));
        ctx.Plugins.Add(MakePlugin("TestingHigh", "testing-high-http",
            "Testing plugin with high downloads", downloadCount: 999));
        await ctx.SaveChangesAsync();
        await RefreshSearchVectors(ctx);

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/plugins/search?q=testing");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement data = doc.RootElement.GetProperty("data");

        Assert.Equal(2, data.GetArrayLength());
        Assert.Equal("TestingHigh", data[0].GetProperty("name").GetString());
        Assert.Equal("TestingLow", data[1].GetProperty("name").GetString());
    }

    [Fact]
    public async Task SearchEndpoint_FilterByType_ReturnsOnlyMatchingType()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        CategoryEntity typeSkill = MakeCategory("type", "skill");
        CategoryEntity typeHook = MakeCategory("type", "hook");
        ctx.Categories.AddRange(typeSkill, typeHook);
        await ctx.SaveChangesAsync();

        PluginEntity skillPlugin = MakePlugin("AuthSkillHttp", "auth-skill-http-type",
            "Auth skill plugin");
        PluginEntity hookPlugin = MakePlugin("AuthHookHttp", "auth-hook-http-type",
            "Auth hook plugin");
        ctx.Plugins.AddRange(skillPlugin, hookPlugin);
        await ctx.SaveChangesAsync();

        ctx.PluginCategories.Add(new PluginCategoryEntity { PluginId = skillPlugin.Id, CategoryId = typeSkill.Id });
        ctx.PluginCategories.Add(new PluginCategoryEntity { PluginId = hookPlugin.Id, CategoryId = typeHook.Id });
        await ctx.SaveChangesAsync();
        await RefreshSearchVectors(ctx);

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/plugins/search?q=auth&type=skill");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        Assert.Equal(1, root.GetProperty("totalCount").GetInt32());
        JsonElement data = root.GetProperty("data");
        Assert.Equal(1, data.GetArrayLength());
        Assert.Equal("AuthSkillHttp", data[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task SearchEndpoint_FilterByLanguage_ReturnsOnlyMatchingLanguage()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        CategoryEntity langTs = MakeCategory("language", "typescript");
        CategoryEntity langPy = MakeCategory("language", "python");
        ctx.Categories.AddRange(langTs, langPy);
        await ctx.SaveChangesAsync();

        PluginEntity tsPlugin = MakePlugin("ValidationTsHttp", "validation-ts-http",
            "TypeScript validation http test");
        PluginEntity pyPlugin = MakePlugin("ValidationPyHttp", "validation-py-http",
            "Python validation http test");
        ctx.Plugins.AddRange(tsPlugin, pyPlugin);
        await ctx.SaveChangesAsync();

        ctx.PluginCategories.Add(new PluginCategoryEntity { PluginId = tsPlugin.Id, CategoryId = langTs.Id });
        ctx.PluginCategories.Add(new PluginCategoryEntity { PluginId = pyPlugin.Id, CategoryId = langPy.Id });
        await ctx.SaveChangesAsync();
        await RefreshSearchVectors(ctx);

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/plugins/search?q=validation&language=python");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        Assert.Equal(1, doc.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal("ValidationPyHttp", doc.RootElement.GetProperty("data")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task SearchEndpoint_InvalidPagination_Returns400WithSpecExactDetail()
    {
        // Spec verbatim: "Page and limit must be greater than 0"
        HttpResponseMessage response = await _client.GetAsync("/api/v1/plugins/search?q=test&page=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement detail = doc.RootElement.GetProperty("detail");

        Assert.Equal("Page and limit must be greater than 0", detail.GetString());
    }

    [Fact]
    public async Task SearchEndpoint_LimitZero_Returns400WithSpecExactDetail()
    {
        // Spec verbatim: "Page and limit must be greater than 0"
        HttpResponseMessage response = await _client.GetAsync("/api/v1/plugins/search?q=test&limit=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement detail = doc.RootElement.GetProperty("detail");

        Assert.Equal("Page and limit must be greater than 0", detail.GetString());
    }

    [Fact]
    public async Task SearchEndpoint_Pagination_PageBeyondRangeReturnsEmptyData()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        ctx.Plugins.Add(MakePlugin("OnlyPlugin", "only-plugin-pagination",
            "A single plugin for pagination test"));
        await ctx.SaveChangesAsync();
        await RefreshSearchVectors(ctx);

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/plugins/search?q=plugin&page=999&limit=20");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        Assert.Equal(0, root.GetProperty("data").GetArrayLength());
        Assert.True(root.GetProperty("totalCount").GetInt32() > 0,
            "totalCount must still reflect the real total");
    }

    // =========================================================================
    // GET /api/v1/search — thin alias delegating to same use-case
    // =========================================================================

    [Fact]
    public async Task SearchAliasEndpoint_Returns200WithSameResultsAsPluginsSearch()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        ctx.Plugins.Add(MakePlugin("AliasPlugin", "alias-plugin",
            "Plugin for alias endpoint test"));
        await ctx.SaveChangesAsync();
        await RefreshSearchVectors(ctx);

        // Act — call both endpoints with same query
        HttpResponseMessage primaryResponse = await _client.GetAsync("/api/v1/plugins/search?q=alias");
        HttpResponseMessage aliasResponse = await _client.GetAsync("/api/v1/search?q=alias");

        // Assert — both return 200
        Assert.Equal(HttpStatusCode.OK, primaryResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, aliasResponse.StatusCode);

        // Both return the same result count
        string primaryBody = await primaryResponse.Content.ReadAsStringAsync();
        string aliasBody = await aliasResponse.Content.ReadAsStringAsync();

        using JsonDocument primaryDoc = JsonDocument.Parse(primaryBody);
        using JsonDocument aliasDoc = JsonDocument.Parse(aliasBody);

        Assert.Equal(
            primaryDoc.RootElement.GetProperty("totalCount").GetInt32(),
            aliasDoc.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task SearchAliasEndpoint_EmptyQuery_Returns200EmptyEnvelope()
    {
        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/search");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        // Envelope shape present
        Assert.True(doc.RootElement.TryGetProperty("data", out _));
        Assert.True(doc.RootElement.TryGetProperty("totalCount", out _));
    }

    // =========================================================================
    // GET /api/v1/discovery
    // =========================================================================

    [Fact]
    public async Task DiscoveryEndpoint_WithKeyword_Returns200WithRankedResultsAndMetadata()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        ctx.Plugins.Add(MakePlugin("LoggerPlugin", "logger-discovery",
            "A comprehensive logging plugin", downloadCount: 100));
        ctx.Plugins.Add(MakePlugin("LogWriter", "log-writer-discovery",
            "Writes structured log entries", downloadCount: 50));
        await ctx.SaveChangesAsync();
        await RefreshSearchVectors(ctx);

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/discovery?keyword=logging");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        // Discovery returns items array (may be wrapped or flat)
        Assert.True(root.TryGetProperty("items", out JsonElement items),
            "Discovery response must have 'items' property");
        Assert.Equal(JsonValueKind.Array, items.ValueKind);

        if (items.GetArrayLength() > 0)
        {
            JsonElement firstItem = items[0];
            // Essential metadata per spec
            Assert.True(firstItem.TryGetProperty("name", out _), "Item must have 'name'");
            Assert.True(firstItem.TryGetProperty("description", out _), "Item must have 'description'");
            Assert.True(firstItem.TryGetProperty("relevanceScore", out JsonElement score),
                "Item must have 'relevanceScore'");
            Assert.True(firstItem.TryGetProperty("maturityIndicator", out _),
                "Item must have 'maturityIndicator'");
            Assert.True(firstItem.TryGetProperty("downloadCount", out _),
                "Item must have 'downloadCount'");
            Assert.True(firstItem.TryGetProperty("author", out _),
                "Item must have 'author'");
            Assert.True(firstItem.TryGetProperty("languages", out _),
                "Item must have 'languages' (all supported languages)");

            // Relevance score must be in [0, 1]
            float relevance = score.GetSingle();
            Assert.InRange(relevance, 0.0f, 1.0f);
        }
    }

    [Fact]
    public async Task DiscoveryEndpoint_ResultsSortedByRelevanceDescending()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        ctx.Plugins.Add(MakePlugin("LoggerExact", "logger-exact-rank",
            "Logger plugin — exact keyword match", downloadCount: 100));
        ctx.Plugins.Add(MakePlugin("FileLogger", "file-logger-rank",
            "Logs data to files", downloadCount: 10));
        await ctx.SaveChangesAsync();
        await RefreshSearchVectors(ctx);

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/discovery?keyword=Logger");

        // Assert — results are present and sorted by relevance desc
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement items = doc.RootElement.GetProperty("items");

        if (items.GetArrayLength() >= 2)
        {
            float firstScore = items[0].GetProperty("relevanceScore").GetSingle();
            float secondScore = items[1].GetProperty("relevanceScore").GetSingle();
            Assert.True(firstScore >= secondScore,
                "Discovery items must be sorted by relevanceScore descending");
        }
    }

    [Fact]
    public async Task DiscoveryEndpoint_BlankKeyword_Returns400WithSpecExactDetail()
    {
        // Spec verbatim: "Keyword cannot be empty"
        HttpResponseMessage response = await _client.GetAsync("/api/v1/discovery?keyword=");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement detail = doc.RootElement.GetProperty("detail");

        // VERBATIM spec string from discovery-service/spec.md:
        Assert.Equal("Keyword cannot be empty", detail.GetString());
    }

    [Fact]
    public async Task DiscoveryEndpoint_MissingKeyword_Returns400WithSpecExactDetail()
    {
        // Missing keyword parameter (no keyword= at all) — same as blank
        HttpResponseMessage response = await _client.GetAsync("/api/v1/discovery");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        Assert.Equal("Keyword cannot be empty", doc.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task DiscoveryEndpoint_FilterByLanguage_ReturnsOnlyMatchingLanguage()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        CategoryEntity langTs = MakeCategory("language", "typescript");
        CategoryEntity langPy = MakeCategory("language", "python");
        ctx.Categories.AddRange(langTs, langPy);
        await ctx.SaveChangesAsync();

        PluginEntity tsPlugin = MakePlugin("TsDiscoveryPlugin", "ts-discovery-lang",
            "TypeScript discovery language filter test");
        PluginEntity pyPlugin = MakePlugin("PyDiscoveryPlugin", "py-discovery-lang",
            "Python discovery language filter test");
        ctx.Plugins.AddRange(tsPlugin, pyPlugin);
        await ctx.SaveChangesAsync();

        ctx.PluginCategories.Add(new PluginCategoryEntity { PluginId = tsPlugin.Id, CategoryId = langTs.Id });
        ctx.PluginCategories.Add(new PluginCategoryEntity { PluginId = pyPlugin.Id, CategoryId = langPy.Id });
        await ctx.SaveChangesAsync();
        await RefreshSearchVectors(ctx);

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/discovery?keyword=discovery&language=python");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement items = doc.RootElement.GetProperty("items");

        Assert.True(items.GetArrayLength() >= 1);
        // Only Python plugin should appear
        Assert.All(items.EnumerateArray(), item =>
            Assert.Equal("PyDiscoveryPlugin", item.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task DiscoveryEndpoint_NoResults_Returns200WithCriteriaEchoed()
    {
        // Arrange — empty DB (after truncate)
        // Act
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/discovery?keyword=nonexistent-abc-xyz&language=typescript&type=skill");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        // Items is empty
        JsonElement items = root.GetProperty("items");
        Assert.Equal(0, items.GetArrayLength());

        // Spec: "response includes which criteria were applied"
        Assert.True(root.TryGetProperty("criteriaEchoed", out JsonElement criteria),
            "Empty discovery result must include 'criteriaEchoed'");
        Assert.Equal(JsonValueKind.Array, criteria.ValueKind);
        Assert.True(criteria.GetArrayLength() > 0,
            "criteriaEchoed must list the applied criteria when no results found");
    }

    [Fact]
    public async Task DiscoveryEndpoint_CombinedCriteria_OnlyPluginsSatisfyingAllConditionsReturned()
    {
        // Spec: "when a user requests discovery with keyword='testing', language='Python', useCase='dev team'"
        //       "the system returns plugins matching ALL criteria"
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        CategoryEntity langPy = MakeCategory("language", "python");
        CategoryEntity useCaseDevTeam = MakeCategory("use_case", "dev-team");
        ctx.Categories.AddRange(langPy, useCaseDevTeam);
        await ctx.SaveChangesAsync();

        // Matches all: python + dev-team + keyword "testing"
        PluginEntity matchAll = MakePlugin("PythonTestingDevTeam", "py-testing-dt-combo",
            "Python testing plugin for dev team use case");
        // Matches keyword + language but NOT use case
        PluginEntity matchTwoOnly = MakePlugin("PythonTestingNoUseCase", "py-testing-no-uc-combo",
            "Python testing plugin without dev-team tag");

        ctx.Plugins.AddRange(matchAll, matchTwoOnly);
        await ctx.SaveChangesAsync();

        ctx.PluginCategories.Add(new PluginCategoryEntity { PluginId = matchAll.Id, CategoryId = langPy.Id });
        ctx.PluginCategories.Add(new PluginCategoryEntity { PluginId = matchAll.Id, CategoryId = useCaseDevTeam.Id });
        ctx.PluginCategories.Add(new PluginCategoryEntity { PluginId = matchTwoOnly.Id, CategoryId = langPy.Id });
        await ctx.SaveChangesAsync();
        await RefreshSearchVectors(ctx);

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/discovery?keyword=testing&language=python&useCase=dev-team");

        // Assert — only matchAll returned (AND across dimensions)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement items = doc.RootElement.GetProperty("items");

        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal("PythonTestingDevTeam", items[0].GetProperty("name").GetString());
    }

    // =========================================================================
    // Result shape: relevanceScore must be present and in [0,1]
    // =========================================================================

    [Fact]
    public async Task SearchEndpoint_ResultItems_IncludeRelevanceScore()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        ctx.Plugins.Add(MakePlugin("ScorePlugin", "score-plugin",
            "Plugin to verify relevance score is returned"));
        await ctx.SaveChangesAsync();
        await RefreshSearchVectors(ctx);

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/plugins/search?q=score");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement data = doc.RootElement.GetProperty("data");

        if (data.GetArrayLength() > 0)
        {
            Assert.True(data[0].TryGetProperty("relevanceScore", out JsonElement scoreEl),
                "Search result item must include 'relevanceScore'");
            float score = scoreEl.GetSingle();
            Assert.InRange(score, 0.0f, 1.0f);
        }
    }
}
