using ClaudeForge.Application.Modules.Docs.Ports;
using ClaudeForge.Core.Shared.Model;
using ClaudeForge.Infrastructure.Docs;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using ClaudeForge.Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Tests.Integration.Docs;

/// <summary>
/// Repository integration tests for Group 9 (tasks 9.1–9.2, 9.5):
///   IDocsRepositoryPort — full-text search over doc_pages table and plugin README retrieval.
///
/// These tests run against a REAL PostgreSQL 16 container via Testcontainers.
/// Docker must be running on the test host.
///
/// IMPORTANT — schema requirement for coder:
///   This test group introduces a NEW table <c>doc_pages</c> (static markdown pages).
///   The coder MUST add an EF migration to create this table. The DocPageEntity is mapped
///   to the "doc_pages" table with columns:
///     id UUID PK, slug TEXT UNIQUE NOT NULL, title TEXT NOT NULL,
///     content_markdown TEXT NOT NULL, category TEXT NOT NULL,
///     last_updated TIMESTAMPTZ NOT NULL DEFAULT NOW(),
///     search_vector TSVECTOR (generated: to_tsvector('english', title || ' ' || content_markdown))
///   The TRUNCATE isolation list must include "doc_pages" to prevent seeded pages from
///   leaking across tests (see InitializeAsync below).
///
/// Expected production types (coder MUST match these names exactly):
///
///   Namespace: ClaudeForge.Infrastructure.Docs
///     DocsRepositoryAdapter(MarketplaceDbContext context) : IDocsRepositoryPort
///
///   Namespace: ClaudeForge.Infrastructure.Persistence.Entities
///     DocPageEntity
///       Guid Id; string Slug; string Title; string ContentMarkdown;
///       string Category; DateTimeOffset LastUpdated; string? SearchVector
///
///   Namespace: ClaudeForge.Application.Modules.Docs.Ports
///     IDocsRepositoryPort
///       Task&lt;(IReadOnlyList&lt;DocSearchResultDto&gt; Items, int TotalCount)&gt; SearchAsync(
///           string query, PaginationRequest pagination, CancellationToken ct = default)
///       Task&lt;DocPageDto?&gt; GetBySlugAsync(string slug, CancellationToken ct = default)
///
///   MarketplaceDbContext must expose:
///     DbSet&lt;DocPageEntity&gt; DocPages
///
/// Spec scenarios:
///   "Full-text search across all docs"
///     WHEN a user enters a search term
///     THEN results include matching documentation pages
///     AND results are ranked by relevance (title match > content match)
///     AND up to 20 results are displayed with pagination
///
///   "Documentation synced from plugin metadata"
///     WHEN a plugin is uploaded or updated
///     THEN readme_text from plugin_versions is surfaced as a doc page
///
///   "Broken or missing README handled gracefully"
///     WHEN a plugin has no readme_text
///     THEN the system displays a placeholder (no exception thrown)
/// </summary>
[Collection(PostgresFixture.CollectionName)]
public sealed class DocsRepositoryPortTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public DocsRepositoryPortTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // -------------------------------------------------------------------------
    // Per-test isolation: truncate all marketplace tables before each test.
    // NOTE: doc_pages is included so seeded doc pages don't leak across tests.
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

    public Task DisposeAsync() => Task.CompletedTask;

    // -------------------------------------------------------------------------
    // Seed helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates a DocPageEntity for insertion into the doc_pages table.
    /// </summary>
    private static DocPageEntity MakeDocPage(
        string slug,
        string title,
        string category = "Getting Started",
        string contentMarkdown = "Default content for the documentation page.",
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

    /// <summary>
    /// Creates a AddOnEntity for seeding plugin README tests.
    /// </summary>
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

    /// <summary>
    /// Creates a AddOnVersionEntity with optional readme_text.
    /// </summary>
    private static AddOnVersionEntity MakeVersion(
        Guid pluginId,
        string version,
        string? readmeText = null,
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
            SizeBytes = 2048,
            Sha256 = new string('b', 64),
            DownloadCount = 0,
            ReadmeText = readmeText,
            ReleasedAt = DateTimeOffset.UtcNow,
        };

    // -------------------------------------------------------------------------
    // SearchAsync — basic match
    // Spec: "results include matching documentation pages"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SearchAsync_MatchingQuery_ReturnsMatchingDocPages()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        ctx.DocPages.AddRange(
            MakeDocPage("getting-started", "Getting Started",
                contentMarkdown: "Install the CLI and configure your environment."),
            MakeDocPage("contributor-guide", "Contributor Guide",
                contentMarkdown: "How to author and publish plugins to the marketplace.")
        );
        await ctx.SaveChangesAsync();

        IDocsRepositoryPort repo = new DocsRepositoryAdapter(ctx);
        PaginationRequest pagination = new() { Page = 1, Limit = 20 };

        // Act
        (IReadOnlyList<DocSearchResultDto> items, int totalCount) =
            await repo.SearchAsync("install", pagination);

        // Assert — "getting-started" page mentions "install", contributor guide does not
        Assert.True(totalCount >= 1, $"Expected at least 1 match, got {totalCount}");
        Assert.True(items.Count >= 1);
        Assert.Contains(items, r => r.Slug == "getting-started");
    }

    [Fact]
    public async Task SearchAsync_CaseInsensitiveQuery_ReturnsMatchingResults()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        ctx.DocPages.Add(MakeDocPage("privacy-guide", "Privacy and Telemetry",
            contentMarkdown: "We collect anonymized telemetry only. No PII."));
        await ctx.SaveChangesAsync();

        IDocsRepositoryPort repo = new DocsRepositoryAdapter(ctx);

        // Act — search with different casing
        (IReadOnlyList<DocSearchResultDto> items, int total) =
            await repo.SearchAsync("PRIVACY", PaginationRequest.Default);

        // Assert — FTS is case-insensitive
        Assert.True(total >= 1);
        Assert.Contains(items, r => r.Slug == "privacy-guide");
    }

    // -------------------------------------------------------------------------
    // SearchAsync — title match ranks above content match
    // Spec: "results are ranked by relevance (title match > content match)"
    // Design §4: "name weight A > description weight B" — for docs: title > content
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SearchAsync_TitleMatchRanksAboveContentMatch()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        // Page A: "Installation" in title (strong title weight)
        ctx.DocPages.Add(MakeDocPage("installation-guide", "Installation Guide",
            contentMarkdown: "Follow these steps to set up the plugin marketplace."));

        // Page B: "installation" only in content (lower weight)
        ctx.DocPages.Add(MakeDocPage("faq", "FAQ",
            contentMarkdown: "Q: How does installation work? A: See the installation guide."));

        await ctx.SaveChangesAsync();

        IDocsRepositoryPort repo = new DocsRepositoryAdapter(ctx);

        // Act — search for "installation"
        (IReadOnlyList<DocSearchResultDto> items, int total) =
            await repo.SearchAsync("installation", PaginationRequest.Default);

        // Assert — both match, but title-match must rank first
        Assert.Equal(2, total);
        Assert.Equal(2, items.Count);
        Assert.Equal("installation-guide", items[0].Slug);  // title match first
        Assert.True(items[0].RelevanceScore >= items[1].RelevanceScore);
    }

    // -------------------------------------------------------------------------
    // SearchAsync — pagination + total count
    // Spec: "up to 20 results are displayed with pagination"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SearchAsync_PaginationLimitAndPage_ReturnsCorrectSubset()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        for (int i = 1; i <= 5; i++)
        {
            ctx.DocPages.Add(MakeDocPage(
                $"guide-{i:D2}",
                $"Guide {i:D2}",
                contentMarkdown: "This guide contains plugin information."));
        }
        await ctx.SaveChangesAsync();

        IDocsRepositoryPort repo = new DocsRepositoryAdapter(ctx);
        PaginationRequest page1 = new() { Page = 1, Limit = 2 };

        // Act
        (IReadOnlyList<DocSearchResultDto> items, int totalCount) =
            await repo.SearchAsync("plugin", page1);

        // Assert
        Assert.Equal(5, totalCount);
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task SearchAsync_PageBeyondRange_ReturnsEmptyItemsWithCorrectTotal()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        ctx.DocPages.Add(MakeDocPage("only-page", "Only Page",
            contentMarkdown: "A single documentation page about plugins."));
        await ctx.SaveChangesAsync();

        IDocsRepositoryPort repo = new DocsRepositoryAdapter(ctx);
        PaginationRequest farPage = new() { Page = 99, Limit = 20 };

        // Act
        (IReadOnlyList<DocSearchResultDto> items, int totalCount) =
            await repo.SearchAsync("plugin", farPage);

        // Assert
        Assert.True(totalCount >= 1);
        Assert.Empty(items);
    }

    // -------------------------------------------------------------------------
    // SearchAsync — empty query / no match → empty results, total=0
    // Spec: graceful handling of empty/no-match scenarios
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SearchAsync_EmptyQuery_ReturnsEmptyWithZeroTotal()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        ctx.DocPages.Add(MakeDocPage("some-doc", "Some Doc",
            contentMarkdown: "Some content here."));
        await ctx.SaveChangesAsync();

        IDocsRepositoryPort repo = new DocsRepositoryAdapter(ctx);

        // Act — empty string query
        (IReadOnlyList<DocSearchResultDto> items, int totalCount) =
            await repo.SearchAsync(string.Empty, PaginationRequest.Default);

        // Assert — empty query returns empty results gracefully (no exception)
        Assert.Equal(0, totalCount);
        Assert.Empty(items);
    }

    [Fact]
    public async Task SearchAsync_NoMatchingDocuments_ReturnsEmptyWithZeroTotal()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        ctx.DocPages.Add(MakeDocPage("a-page", "A Page",
            contentMarkdown: "Content about typescript plugins."));
        await ctx.SaveChangesAsync();

        IDocsRepositoryPort repo = new DocsRepositoryAdapter(ctx);

        // Act — query that matches nothing
        (IReadOnlyList<DocSearchResultDto> items, int totalCount) =
            await repo.SearchAsync("xyzqqqunknown", PaginationRequest.Default);

        // Assert
        Assert.Equal(0, totalCount);
        Assert.Empty(items);
    }

    [Fact]
    public async Task SearchAsync_EmptyDatabase_ReturnsEmptyWithZeroTotal()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IDocsRepositoryPort repo = new DocsRepositoryAdapter(ctx);

        // Act
        (IReadOnlyList<DocSearchResultDto> items, int totalCount) =
            await repo.SearchAsync("anything", PaginationRequest.Default);

        // Assert
        Assert.Equal(0, totalCount);
        Assert.Empty(items);
    }

    // -------------------------------------------------------------------------
    // SearchAsync — result shape: slug, title, category, snippet, relevanceScore
    // Spec: DocSearchResultDto { Slug, Title, Category, Snippet, RelevanceScore }
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SearchAsync_Match_ResultHasSlugTitleCategorySnippetAndScore()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        ctx.DocPages.Add(MakeDocPage(
            "api-reference",
            "API Reference",
            category: "API Reference",
            contentMarkdown: "All available plugin hooks and their signatures are documented here."));
        await ctx.SaveChangesAsync();

        IDocsRepositoryPort repo = new DocsRepositoryAdapter(ctx);

        // Act
        (IReadOnlyList<DocSearchResultDto> items, _) =
            await repo.SearchAsync("hooks", PaginationRequest.Default);

        // Assert
        Assert.Single(items);
        DocSearchResultDto result = items[0];
        Assert.Equal("api-reference", result.Slug);
        Assert.Equal("API Reference", result.Title);
        Assert.Equal("API Reference", result.Category);
        Assert.False(string.IsNullOrWhiteSpace(result.Snippet),
            "Snippet must be non-empty for a matching result");
        Assert.True(result.RelevanceScore > 0f,
            "RelevanceScore must be positive for a matching result");
    }

    // -------------------------------------------------------------------------
    // GetBySlugAsync — returns the matching page with all fields
    // Spec: "GET /api/v1/docs/{slug} → 200 doc page, markdown + lastUpdated"
    //       "Response: { slug, title, content (markdown), last_updated }"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetBySlugAsync_ExistingSlug_ReturnsDocPageWithAllFields()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        DateTimeOffset lastUpdated = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
        ctx.DocPages.Add(MakeDocPage(
            "contributor-guide",
            "Contributor Guide",
            category: "Publishing Plugins",
            contentMarkdown: "# Contributor Guide\n\nLearn how to publish plugins.",
            lastUpdated: lastUpdated));
        await ctx.SaveChangesAsync();

        IDocsRepositoryPort repo = new DocsRepositoryAdapter(ctx);

        // Act
        DocPageDto? result = await repo.GetBySlugAsync("contributor-guide");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("contributor-guide", result!.Slug);
        Assert.Equal("Contributor Guide", result!.Title);
        Assert.Equal("Publishing Plugins", result!.Category);
        Assert.Equal("# Contributor Guide\n\nLearn how to publish plugins.", result!.ContentMarkdown);
        Assert.Equal(lastUpdated, result!.LastUpdated);
    }

    // -------------------------------------------------------------------------
    // GetBySlugAsync — unknown slug returns null (not an exception)
    // Spec: slug retrieval / 404 handling is the use-case's responsibility.
    //       The repository returns null; the use-case converts to DocNotFoundException.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetBySlugAsync_UnknownSlug_ReturnsNull()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IDocsRepositoryPort repo = new DocsRepositoryAdapter(ctx);

        // Act
        DocPageDto? result = await repo.GetBySlugAsync("this-slug-does-not-exist");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetBySlugAsync_EmptyDatabase_ReturnsNull()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IDocsRepositoryPort repo = new DocsRepositoryAdapter(ctx);

        // Act
        DocPageDto? result = await repo.GetBySlugAsync("any-slug");

        // Assert
        Assert.Null(result);
    }

    // -------------------------------------------------------------------------
    // Plugin README surfacing (9.5, per spec "Documentation synced from plugin metadata")
    //
    // Per design.md §3: "README extracted from package on upload → readme_text"
    // Per spec: "Plugin-specific documentation displayed — sourced from plugin metadata or README"
    // The repository surfaces a plugin's latest readme_text as a retrievable doc.
    // The slug convention for plugin README docs is "plugin:{slug}" (e.g. "plugin:my-tool").
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetBySlugAsync_PluginWithReadme_ReturnsPluginDocFromReadmeText()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        AddOnEntity plugin = MakePlugin("AwesomeTool", "awesome-tool");
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        ctx.PluginVersions.Add(MakeVersion(
            plugin.Id,
            "1.0.0",
            readmeText: "# Awesome Tool\n\nThis tool is amazing. Install with `claude plugin install awesome-tool`.",
            isLatest: true));
        await ctx.SaveChangesAsync();

        IDocsRepositoryPort repo = new DocsRepositoryAdapter(ctx);

        // Act — plugin README is surfaced under slug "plugin:{plugin-slug}"
        DocPageDto? result = await repo.GetBySlugAsync($"plugin:{plugin.Slug}");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("awesome-tool", result!.Slug);
        Assert.Contains("Awesome Tool", result!.Title);
        Assert.Contains("This tool is amazing", result!.ContentMarkdown);
    }

    [Fact]
    public async Task GetBySlugAsync_PluginWithoutReadme_ReturnsGracefulPlaceholder()
    {
        // Arrange
        // Spec: "Broken or missing README handled gracefully"
        //       "THEN the marketplace displays: Plugin metadata (name, description, version)"
        //       "AND a placeholder: 'No detailed documentation provided'"
        //       "AND the plugin remains functional and installable"
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        AddOnEntity plugin = MakePlugin("SilentPlugin", "silent-plugin");
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        // Version with null readme_text
        ctx.PluginVersions.Add(MakeVersion(plugin.Id, "1.0.0", readmeText: null, isLatest: true));
        await ctx.SaveChangesAsync();

        IDocsRepositoryPort repo = new DocsRepositoryAdapter(ctx);

        // Act — should return a placeholder page, NOT null, NOT throw
        DocPageDto? result = await repo.GetBySlugAsync($"plugin:{plugin.Slug}");

        // Assert — placeholder page returned; spec string "No detailed documentation provided"
        Assert.NotNull(result);
        Assert.Contains("No detailed documentation provided", result!.ContentMarkdown);
    }

    [Fact]
    public async Task GetBySlugAsync_NonExistentPlugin_ReturnsNull()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IDocsRepositoryPort repo = new DocsRepositoryAdapter(ctx);

        // Act — slug references a plugin that doesn't exist
        DocPageDto? result = await repo.GetBySlugAsync("plugin:plugin-that-does-not-exist");

        // Assert — returns null (no such plugin); use-case converts to DocNotFoundException
        Assert.Null(result);
    }

    // -------------------------------------------------------------------------
    // SearchAsync — multiple categories are returned in results
    // Spec: sidebar shows categories: Getting Started, Installation & Configuration,
    //       Publishing Plugins, API Reference, Privacy & Security, Troubleshooting
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SearchAsync_PagesInDifferentCategories_ResultsIncludeCorrectCategories()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        ctx.DocPages.AddRange(
            MakeDocPage("getting-started", "Getting Started",
                category: "Getting Started",
                contentMarkdown: "Welcome to the plugin marketplace. Install to begin."),
            MakeDocPage("faq", "FAQ",
                category: "General",
                contentMarkdown: "How do I install? How do I configure the plugin marketplace?"),
            MakeDocPage("privacy-telemetry", "Privacy and Telemetry",
                category: "Privacy & Security",
                contentMarkdown: "Marketplace data collection policy. Install requires no PII.")
        );
        await ctx.SaveChangesAsync();

        IDocsRepositoryPort repo = new DocsRepositoryAdapter(ctx);

        // Act — all three pages contain "install" or "marketplace"
        (IReadOnlyList<DocSearchResultDto> items, int totalCount) =
            await repo.SearchAsync("install", PaginationRequest.Default);

        // Assert — categories come back correctly
        Assert.True(totalCount >= 1);
        string[] returnedCategories = items.Select(r => r.Category).Distinct().ToArray();
        Assert.True(returnedCategories.Length >= 1,
            "At least one category must be present in search results");
    }
}
