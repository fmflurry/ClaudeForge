using System.Net;
using System.Text.Json;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using ClaudeForge.Tests.Integration.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeForge.Tests.Integration.Docs;

/// <summary>
/// HTTP integration tests for Group 9 (tasks 9.4–9.5): Docs API endpoints.
///
/// Uses WebApplicationFactory&lt;Program&gt; with a real PostgreSQL 16 container.
/// Tests the full HTTP stack: routing, serialisation, exception → ProblemDetails mapping.
///
/// IMPORTANT — schema requirement (same as DocsRepositoryPortTests):
///   The "doc_pages" table must exist (coder adds EF migration).
///   The TRUNCATE isolation list includes "doc_pages" to prevent cross-test leakage.
///
/// Endpoints under test:
///   GET /api/v1/docs?search=&amp;page=&amp;limit=  → 200 paginated search results
///   GET /api/v1/docs/{slug}                → 200 doc page, 404 ProblemDetails for unknown slug
///
/// Spec scenarios (docs/spec.md):
///
///   "Full-text search across all docs"
///     WHEN a user enters a search term
///     THEN results include matching documentation pages
///     AND results are ranked by relevance
///     AND up to 20 results are displayed with pagination
///
///   "Search highlights and context snippets"
///     WHEN a user clicks a search result
///     THEN the documentation page is displayed with a short context snippet
///
///   "Plugin lacks documentation displays placeholder"
///     WHEN a user views a plugin with no documentation
///     THEN "No documentation available yet" / "No detailed documentation provided" is shown
///
///   "Broken or missing README handled gracefully"
///     WHEN a plugin lacks README
///     THEN placeholder displayed, plugin functional, NOT a 500 error
///
/// Verbatim spec strings used in assertions:
///   "No detailed documentation provided"  (placeholder for plugin without README)
///   "Documentation page not found"         (ProblemDetails detail for unknown slug)
///
/// Response shapes (design.md §7):
///   GET /api/v1/docs?search= → { data: [{slug, title, category, snippet, relevanceScore}], totalCount, page, limit, totalPages }
///   GET /api/v1/docs/{slug}  → { slug, title, category, contentMarkdown, lastUpdated }
/// </summary>
[Collection(PostgresFixture.CollectionName)]
public sealed class DocsHttpTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public DocsHttpTests(PostgresFixture fixture)
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
    // doc_pages is included to prevent seeded pages from leaking across tests.
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
                categories,
                doc_pages
            RESTART IDENTITY CASCADE
            """);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    // -------------------------------------------------------------------------
    // Seed helpers
    // -------------------------------------------------------------------------

    private static DocPageEntity MakeDocPage(
        string slug,
        string title,
        string category = "Getting Started",
        string contentMarkdown = "Default content for the doc page.",
        DateTimeOffset? lastUpdated = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            Title = title,
            Category = category,
            ContentMarkdown = contentMarkdown,
            LastUpdated = lastUpdated ?? DateTimeOffset.UtcNow,
        };

    private static AddOnEntity MakePlugin(string name, string slug) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            NameNormalized = name.ToLowerInvariant(),
            Slug = slug,
            Description = $"Description for {name}",
            Author = "test-author",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    private static AddOnVersionEntity MakeVersion(
        Guid pluginId,
        string version,
        string? readmeText,
        bool isLatest = true) =>
        new()
        {
            Id = Guid.NewGuid(),
            PluginId = pluginId,
            Version = version,
            VersionSort = 1_000_000L,
            ReleaseNotes = string.Empty,
            IsLatest = isLatest,
            PackageKey = $"plugins/{pluginId}/{version}/package.tar.gz",
            PackageFormat = "tar.gz",
            SizeBytes = 1024,
            Sha256 = new string('f', 64),
            DownloadCount = 0,
            ReadmeText = readmeText,
            ReleasedAt = DateTimeOffset.UtcNow,
        };

    // -------------------------------------------------------------------------
    // GET /api/v1/docs?search= — 200 + paginated envelope shape
    // Spec: "results include: Matching documentation pages, Plugin guides, FAQ entries"
    //       "up to 20 results are displayed with pagination"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetDocs_EmptyDatabase_Returns200WithEmptyPaginatedEnvelope()
    {
        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/docs?search=anything");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        // Required envelope shape: data, totalCount, page, limit, totalPages
        Assert.True(root.TryGetProperty("data", out JsonElement data),
            "Response must have 'data' property");
        Assert.True(root.TryGetProperty("totalCount", out JsonElement totalCount),
            "Response must have 'totalCount' property");
        Assert.True(root.TryGetProperty("page", out JsonElement page),
            "Response must have 'page' property");
        Assert.True(root.TryGetProperty("limit", out JsonElement limit),
            "Response must have 'limit' property");
        Assert.True(root.TryGetProperty("totalPages", out JsonElement totalPages),
            "Response must have 'totalPages' property");

        Assert.Equal(JsonValueKind.Array, data.ValueKind);
        Assert.Equal(0, data.GetArrayLength());
        Assert.Equal(0, totalCount.GetInt32());
        Assert.Equal(1, page.GetInt32());
        Assert.Equal(20, limit.GetInt32());
        Assert.Equal(0, totalPages.GetInt32());
    }

    [Fact]
    public async Task GetDocs_WithMatchingDocPages_Returns200WithRankedResults()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        ctx.DocPages.AddRange(
            MakeDocPage("getting-started", "Getting Started",
                contentMarkdown: "Install the CLI and configure your environment to get started."),
            MakeDocPage("contributor-guide", "Contributor Guide",
                contentMarkdown: "How to create, publish and maintain plugins in the marketplace.")
        );
        await ctx.SaveChangesAsync();

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/docs?search=install");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        int count = root.GetProperty("totalCount").GetInt32();
        Assert.True(count >= 1, $"Expected at least 1 result for 'install', got {count}");
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(20, root.GetProperty("limit").GetInt32());
        Assert.Equal(JsonValueKind.Array, root.GetProperty("data").ValueKind);
    }

    [Fact]
    public async Task GetDocs_DefaultParameters_UsesDefaultsPageOneLimit20()
    {
        // Act — no search, no page/limit
        HttpResponseMessage response = await _client.GetAsync("/api/v1/docs");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        // Defaults: page=1, limit=20
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(20, root.GetProperty("limit").GetInt32());
    }

    // -------------------------------------------------------------------------
    // GET /api/v1/docs — search result item shape
    // Spec: "results include: { slug, title, excerpt }" (design.md §7 full shape)
    // Response shape: { slug, title, category, snippet, relevanceScore }
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetDocs_MatchingResult_ItemHasExpectedProperties()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        ctx.DocPages.Add(MakeDocPage(
            "api-reference",
            "API Reference",
            category: "API Reference",
            contentMarkdown: "All plugin hooks and their lifecycle events are documented here."));
        await ctx.SaveChangesAsync();

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/docs?search=hooks");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement data = doc.RootElement.GetProperty("data");

        Assert.True(data.GetArrayLength() >= 1, "Expected at least one result");
        JsonElement item = data[0];

        Assert.True(item.TryGetProperty("slug", out _), "Result must have 'slug'");
        Assert.True(item.TryGetProperty("title", out _), "Result must have 'title'");
        Assert.True(item.TryGetProperty("category", out _), "Result must have 'category'");
        Assert.True(item.TryGetProperty("snippet", out _), "Result must have 'snippet'");
        Assert.True(item.TryGetProperty("relevanceScore", out _), "Result must have 'relevanceScore'");
    }

    // -------------------------------------------------------------------------
    // GET /api/v1/docs — ranked results (title > content per spec)
    // Spec: "results are ranked by relevance (title match > content match)"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetDocs_TitleMatchRanksAboveContentMatch_ResultsOrderedByRelevance()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        // Page 1: "Configuration" in title — should rank first
        ctx.DocPages.Add(MakeDocPage(
            "configuration-guide",
            "Configuration Guide",
            category: "Installation & Configuration",
            contentMarkdown: "Step by step setup and environment requirements."));

        // Page 2: "configuration" only in content — should rank second
        ctx.DocPages.Add(MakeDocPage(
            "faq",
            "FAQ",
            category: "General",
            contentMarkdown: "Common questions about plugin configuration and usage."));

        await ctx.SaveChangesAsync();

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/docs?search=configuration");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement data = doc.RootElement.GetProperty("data");

        Assert.True(data.GetArrayLength() >= 1);

        // The title-match page must appear first
        string firstSlug = data[0].GetProperty("slug").GetString() ?? string.Empty;
        Assert.Equal("configuration-guide", firstSlug);
    }

    // -------------------------------------------------------------------------
    // GET /api/v1/docs — pagination
    // Spec: "up to 20 results are displayed with pagination"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetDocs_Pagination_ReturnsCorrectSubsetAndTotalCount()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        for (int i = 1; i <= 5; i++)
        {
            ctx.DocPages.Add(MakeDocPage(
                $"guide-{i:D2}",
                $"Guide {i:D2}",
                contentMarkdown: "Guide content about the plugin marketplace."));
        }
        await ctx.SaveChangesAsync();

        // Act — page 2, limit 2
        HttpResponseMessage response = await _client.GetAsync("/api/v1/docs?search=plugin&page=2&limit=2");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        Assert.Equal(5, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, root.GetProperty("page").GetInt32());
        Assert.Equal(2, root.GetProperty("limit").GetInt32());
        Assert.Equal(3, root.GetProperty("totalPages").GetInt32());  // ceil(5/2) = 3
        Assert.Equal(2, root.GetProperty("data").GetArrayLength());
    }

    // -------------------------------------------------------------------------
    // GET /api/v1/docs/{slug} — 200 with full doc page
    // Spec: "Documentation version matches plugin version"
    //       Response: { slug, title, content (markdown), last_updated }
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetDocBySlug_ExistingSlug_Returns200WithDocPageShape()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        DateTimeOffset lastUpdated = new(2025, 5, 10, 8, 0, 0, TimeSpan.Zero);
        ctx.DocPages.Add(MakeDocPage(
            "getting-started",
            "Getting Started",
            category: "Getting Started",
            contentMarkdown: "# Getting Started\n\nWelcome to the plugin marketplace.",
            lastUpdated: lastUpdated));
        await ctx.SaveChangesAsync();

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/docs/getting-started");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        Assert.Equal("getting-started", root.GetProperty("slug").GetString());
        Assert.Equal("Getting Started", root.GetProperty("title").GetString());
        Assert.Equal("Getting Started", root.GetProperty("category").GetString());

        // contentMarkdown must be present and non-empty
        Assert.True(root.TryGetProperty("contentMarkdown", out JsonElement contentMarkdown),
            "Response must have 'contentMarkdown' property");
        Assert.False(string.IsNullOrWhiteSpace(contentMarkdown.GetString()),
            "'contentMarkdown' must not be empty");

        // lastUpdated must be present and parseable as a DateTimeOffset
        Assert.True(root.TryGetProperty("lastUpdated", out JsonElement lastUpdatedEl),
            "Response must have 'lastUpdated' property");
        Assert.True(DateTimeOffset.TryParse(lastUpdatedEl.GetString(), out _),
            "'lastUpdated' must be a valid DateTimeOffset");
    }

    [Fact]
    public async Task GetDocBySlug_ExistingSlug_ContentMarkdownMatchesSavedContent()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        string expectedContent = "# Privacy\n\nNo PII is collected. Telemetry is optional.";
        ctx.DocPages.Add(MakeDocPage(
            "privacy-telemetry",
            "Privacy and Telemetry",
            category: "Privacy & Security",
            contentMarkdown: expectedContent));
        await ctx.SaveChangesAsync();

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/docs/privacy-telemetry");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        Assert.Equal(expectedContent,
            doc.RootElement.GetProperty("contentMarkdown").GetString());
    }

    // -------------------------------------------------------------------------
    // GET /api/v1/docs/{slug} — 404 ProblemDetails for unknown slug
    // Spec: "404/placeholder per spec"
    //       ProblemDetails detail: "Documentation page not found"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetDocBySlug_UnknownSlug_Returns404WithProblemDetails()
    {
        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/docs/this-does-not-exist");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        // RFC 7807 ProblemDetails shape
        Assert.True(root.TryGetProperty("detail", out JsonElement detail),
            "404 response must have 'detail' property (ProblemDetails)");

        // Spec verbatim string for missing docs
        Assert.Equal("Documentation page not found", detail.GetString());
    }

    [Fact]
    public async Task GetDocBySlug_EmptyDatabase_Returns404WithProblemDetails()
    {
        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/docs/any-slug");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // GET /api/v1/docs/{slug} — plugin README docs surfacing
    // Spec: "Plugin-specific documentation displayed — sourced from plugin metadata or README"
    //       "Documentation synced from plugin metadata"
    //         WHEN a plugin is uploaded THEN readme_text appears as a doc page
    //
    // Plugin README docs are surfaced under slug "plugin:{plugin-slug}"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetDocBySlug_PluginReadmeSlug_Returns200WithReadmeContent()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        AddOnEntity plugin = MakePlugin("CoolPlugin", "cool-plugin");
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        ctx.PluginVersions.Add(MakeVersion(
            plugin.Id, "1.2.0",
            readmeText: "# Cool Plugin\n\nInstall with `claude plugin install cool-plugin`.",
            isLatest: true));
        await ctx.SaveChangesAsync();

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/docs/plugin:cool-plugin");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        string? contentMarkdown = root.GetProperty("contentMarkdown").GetString();
        Assert.NotNull(contentMarkdown);
        Assert.Contains("Cool Plugin", contentMarkdown);
    }

    // -------------------------------------------------------------------------
    // GET /api/v1/docs/{slug} — plugin with NO README → placeholder (not 500, not 404)
    // Spec: "Plugin lacks documentation displays placeholder"
    //         THEN "No documentation available yet"
    //       "Broken or missing README handled gracefully"
    //         THEN "No detailed documentation provided"
    //         AND the plugin remains functional and installable (NOT blocked)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetDocBySlug_PluginWithNoReadme_Returns200WithPlaceholderContent()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        AddOnEntity plugin = MakePlugin("BareBonePlugin", "bare-bone-plugin");
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        // Plugin with null readme_text
        ctx.PluginVersions.Add(MakeVersion(
            plugin.Id, "0.1.0",
            readmeText: null,
            isLatest: true));
        await ctx.SaveChangesAsync();

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/docs/plugin:bare-bone-plugin");

        // Assert — must be 200 with placeholder, NOT 404 and NOT 500
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        string? contentMarkdown = root.GetProperty("contentMarkdown").GetString();
        Assert.NotNull(contentMarkdown);

        // Spec verbatim placeholder string: "No detailed documentation provided"
        Assert.Contains("No detailed documentation provided", contentMarkdown);
    }

    // -------------------------------------------------------------------------
    // GET /api/v1/docs — graceful handling when docs are missing/incomplete
    // Spec: "Partial documentation indicated to user"
    //       "Missing or Incomplete Documentation Handling"
    //       "The system SHALL gracefully handle plugins with missing documentation."
    //       "Missing docs are not hidden; instead, clear guidance is provided."
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetDocs_SearchForPluginWithMissingDocs_DoesNotReturn500()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        // A plugin with no readme_text should not cause search errors
        AddOnEntity plugin = MakePlugin("IncompletePlugin", "incomplete-plugin");
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        ctx.PluginVersions.Add(MakeVersion(plugin.Id, "1.0.0", readmeText: null, isLatest: true));
        await ctx.SaveChangesAsync();

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/docs?search=incomplete");

        // Assert — graceful response (200), no 500
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // GET /api/v1/docs — all public, no authentication required
    // Spec: "All documentation is public and requires no authentication."
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetDocs_NoAuthHeader_Returns200NotUnauthorized()
    {
        // Act — no Authorization header
        HttpResponseMessage response = await _client.GetAsync("/api/v1/docs");

        // Assert — docs are public
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetDocBySlug_NoAuthHeader_Returns200OrDocNotFound()
    {
        // Act — no Authorization header
        HttpResponseMessage response = await _client.GetAsync("/api/v1/docs/getting-started");

        // Assert — doc endpoint is public; must not return 401/403
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        // Either 200 (doc exists) or 404 (doc not seeded) — never 401/403
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.NotFound,
            $"Expected 200 or 404, got {(int)response.StatusCode}");
    }
}
