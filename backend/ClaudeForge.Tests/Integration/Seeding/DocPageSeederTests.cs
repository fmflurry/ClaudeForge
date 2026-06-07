using ClaudeForge.Application.Modules.Docs.Ports;
using ClaudeForge.Application.Modules.Docs.UseCases;
using ClaudeForge.Core.Shared.Model;
using ClaudeForge.Infrastructure.Docs;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using ClaudeForge.Infrastructure.Persistence.Seeding;
using ClaudeForge.Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Tests.Integration.Seeding;

/// <summary>
/// Integration tests for Group 22 (tasks 22.1–22.4): DocPageSeeder.
///
/// These tests run against a REAL PostgreSQL 16 container via Testcontainers.
/// Docker must be running on the test host.
///
/// CONTENT SOURCING DECISION: Embedded constants.
/// The DocPageSeeder embeds the 5 authored markdown doc pages as compile-time
/// constants inside the seeder class (no runtime file I/O). This keeps prod
/// deployment deterministic — no dependency on a /docs directory being present.
/// The coder copies the authored markdown content from /docs/*.md into the
/// seeder's static SeedDefinitions list.
///
/// PRODUCTION TYPES TO IMPLEMENT (coder MUST match these names/namespaces exactly):
///
///   Namespace: ClaudeForge.Infrastructure.Persistence.Seeding
///
///   Interface:
///     IDocPageSeeder
///       Task SeedAsync(CancellationToken ct = default);
///
///   Class:
///     DocPageSeeder : IDocPageSeeder
///       DocPageSeeder(MarketplaceDbContext context)
///       Task SeedAsync(CancellationToken ct = default)
///         - Idempotently inserts the 5 marketplace doc pages (keyed by slug).
///         - Content sourced from embedded static constants inside the seeder.
///         - Skips any slug that already exists in doc_pages.
///
///   Static accessor (so tests can assert expected slugs/titles/categories):
///     IReadOnlyList&lt;DocPageSeedDefinition&gt; DocPageSeeder.SeedDefinitions { get; }
///
///   DocPageSeedDefinition record:
///     string Slug
///     string Title
///     string Category
///     string ContentMarkdown
///
/// THE 5 EXPECTED SEED PAGES (canonical; coder must produce these):
///
///   Slug                       Title                               Category
///   ─────────────────────────  ─────────────────────────────────   ─────────
///   getting-started            Getting Started                     guide
///   contributing               Contributing &amp; Publishing Plugins  guide
///   faq                        FAQ                                 reference
///   privacy-and-telemetry      Privacy &amp; Telemetry                 reference
///   api-reference              API Reference                       reference
///
/// SEARCH TERM GUARANTEES (each doc contains these terms for search tests):
///   getting-started  → "install"         (has "Install the CLI globally via npm")
///   privacy-and-telemetry → "telemetry"  (has "ClaudeForge collects anonymized telemetry")
///
/// PLUGIN README SURFACING CONVENTION (Group 9, reconfirmed in 22.4):
///   Slug "plugin:{plugin-slug}" → surfaces latest readme_text from plugin_versions.
///   null readme_text → placeholder "No detailed documentation provided".
/// </summary>
[Collection(PostgresFixture.CollectionName)]
public sealed class DocPageSeederTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public DocPageSeederTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // -------------------------------------------------------------------------
    // Per-test isolation: TRUNCATE all affected tables before each test.
    // doc_pages truncated first so seeded pages don't leak across tests.
    // plugins/plugin_versions truncated for the per-plugin README tests (22.4).
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
    // Helper: create a fully wired DocPageSeeder against the test container.
    // -------------------------------------------------------------------------

    private static IDocPageSeeder CreateSeeder(MarketplaceDbContext ctx) =>
        new DocPageSeeder(ctx);

    // -------------------------------------------------------------------------
    // Helpers for per-plugin README tests (22.4).
    // Reuse same pattern as DocsRepositoryPortTests.
    // -------------------------------------------------------------------------

    private static PluginEntity MakePlugin(string name, string slug) =>
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

    private static PluginVersionEntity MakeVersion(
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
            Sha256 = new string('c', 64),
            DownloadCount = 0,
            ReadmeText = readmeText,
            ReleasedAt = DateTimeOffset.UtcNow,
        };

    // =========================================================================
    // Test 1 — After SeedAsync the doc_pages table contains the 5 expected slugs
    //           with non-empty title, contentMarkdown, and a category.
    // =========================================================================

    [Fact]
    public async Task SeedAsync_InsertsExactlyFiveDocPages()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IDocPageSeeder seeder = CreateSeeder(ctx);

        // Act
        await seeder.SeedAsync();

        // Assert: exactly 5 doc_pages rows
        int count = await ctx.DocPages.CountAsync();
        Assert.Equal(5, count);
    }

    [Fact]
    public async Task SeedAsync_AllFiveExpectedSlugsArePresent()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IDocPageSeeder seeder = CreateSeeder(ctx);

        // Act
        await seeder.SeedAsync();

        // Assert: every canonical slug is present
        List<string> slugsInDb = await ctx.DocPages
            .Select(d => d.Slug)
            .ToListAsync();

        IReadOnlyList<DocPageSeedDefinition> definitions = DocPageSeeder.SeedDefinitions;
        Assert.Equal(5, definitions.Count);

        foreach (DocPageSeedDefinition def in definitions)
        {
            Assert.Contains(def.Slug, slugsInDb);
        }
    }

    [Fact]
    public async Task SeedAsync_AllPagesHaveNonEmptyTitleContentMarkdownAndCategory()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IDocPageSeeder seeder = CreateSeeder(ctx);

        // Act
        await seeder.SeedAsync();

        // Assert: every row has non-empty required string fields
        bool allValid = await ctx.DocPages.AllAsync(d =>
            d.Title != string.Empty &&
            d.ContentMarkdown != string.Empty &&
            d.Category != string.Empty);

        Assert.True(allValid,
            "Every seeded doc page must have non-empty Title, ContentMarkdown, and Category.");
    }

    [Fact]
    public async Task SeedAsync_SpecificSlugsHaveCorrectTitlesAndCategories()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IDocPageSeeder seeder = CreateSeeder(ctx);

        // Act
        await seeder.SeedAsync();

        // Assert: each static definition matches what is in the DB
        IReadOnlyList<DocPageSeedDefinition> definitions = DocPageSeeder.SeedDefinitions;

        foreach (DocPageSeedDefinition def in definitions)
        {
            DocPageEntity? entity = await ctx.DocPages
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Slug == def.Slug);

            Assert.NotNull(entity);
            Assert.Equal(def.Title, entity!.Title);
            Assert.Equal(def.Category, entity!.Category);
            Assert.Equal(def.ContentMarkdown, entity!.ContentMarkdown);
        }
    }

    [Fact]
    public async Task SeedDefinitions_StaticList_HasExactlyFiveEntries()
    {
        // The static list must be accessible without instantiation.
        IReadOnlyList<DocPageSeedDefinition> defs = DocPageSeeder.SeedDefinitions;
        Assert.Equal(5, defs.Count);
    }

    [Fact]
    public async Task SeedDefinitions_ContainsAllFiveExpectedSlugs()
    {
        // The static definitions must cover the 5 authored docs exactly.
        string[] expectedSlugs =
        [
            "getting-started",
            "contributing",
            "faq",
            "privacy-and-telemetry",
            "api-reference",
        ];

        IReadOnlyList<DocPageSeedDefinition> defs = DocPageSeeder.SeedDefinitions;

        foreach (string slug in expectedSlugs)
        {
            Assert.Contains(defs, d => d.Slug == slug);
        }

        await Task.CompletedTask; // async test signature for consistency
    }

    // =========================================================================
    // Test 2 — Idempotency: calling SeedAsync twice → still exactly 5 doc pages
    // =========================================================================

    [Fact]
    public async Task SeedAsync_CalledTwice_StillHasExactlyFiveDocPages()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IDocPageSeeder seeder = CreateSeeder(ctx);

        // Act — call twice
        await seeder.SeedAsync();
        await seeder.SeedAsync();

        // Assert: no duplicates
        int count = await ctx.DocPages.CountAsync();
        Assert.Equal(5, count);
    }

    [Fact]
    public async Task SeedAsync_CalledTwice_SlugUniquenessIsPreserved()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IDocPageSeeder seeder = CreateSeeder(ctx);

        // Act
        await seeder.SeedAsync();
        await seeder.SeedAsync();

        // Assert: all slugs remain distinct (no duplicate rows per slug)
        int totalPages = await ctx.DocPages.CountAsync();
        int distinctSlugs = await ctx.DocPages
            .Select(d => d.Slug)
            .Distinct()
            .CountAsync();

        Assert.Equal(totalPages, distinctSlugs);
    }

    // =========================================================================
    // Test 3a — Read-side: after seeding, GetDocPageUseCase returns "getting-started"
    // =========================================================================

    [Fact]
    public async Task AfterSeeding_GetDocPageUseCase_ReturnsGettingStartedPage()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IDocPageSeeder seeder = CreateSeeder(ctx);
        await seeder.SeedAsync();

        IDocsRepositoryPort repo = new DocsRepositoryAdapter(ctx);
        GetDocPageUseCase useCase = new(repo);

        // Act
        DocPageDto page = await useCase.ExecuteAsync("getting-started");

        // Assert
        Assert.Equal("getting-started", page.Slug);
        Assert.False(string.IsNullOrWhiteSpace(page.Title),
            "Title must be non-empty for 'getting-started'.");
        Assert.False(string.IsNullOrWhiteSpace(page.ContentMarkdown),
            "ContentMarkdown must be non-empty for 'getting-started'.");
    }

    [Fact]
    public async Task AfterSeeding_GetDocPageUseCase_ReturnsCorrectTitleForGettingStarted()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IDocPageSeeder seeder = CreateSeeder(ctx);
        await seeder.SeedAsync();

        IDocsRepositoryPort repo = new DocsRepositoryAdapter(ctx);
        GetDocPageUseCase useCase = new(repo);

        // Act
        DocPageDto page = await useCase.ExecuteAsync("getting-started");

        // Assert: title matches the authored doc's H1
        Assert.Equal("Getting Started", page.Title);
    }

    [Fact]
    public async Task AfterSeeding_GetDocPageUseCase_AllFiveSlugsSurfaceSuccessfully()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IDocPageSeeder seeder = CreateSeeder(ctx);
        await seeder.SeedAsync();

        IDocsRepositoryPort repo = new DocsRepositoryAdapter(ctx);
        GetDocPageUseCase useCase = new(repo);

        // Act + Assert: every canonical slug returns a page without throwing
        string[] expectedSlugs =
        [
            "getting-started",
            "contributing",
            "faq",
            "privacy-and-telemetry",
            "api-reference",
        ];

        foreach (string slug in expectedSlugs)
        {
            DocPageDto page = await useCase.ExecuteAsync(slug);

            Assert.Equal(slug, page.Slug);
            Assert.False(string.IsNullOrWhiteSpace(page.Title),
                $"Title must be non-empty for slug '{slug}'.");
            Assert.False(string.IsNullOrWhiteSpace(page.ContentMarkdown),
                $"ContentMarkdown must be non-empty for slug '{slug}'.");
        }
    }

    // =========================================================================
    // Test 3b — Read-side: SearchDocsUseCase for "install" returns getting-started
    // =========================================================================

    [Fact]
    public async Task AfterSeeding_SearchDocsUseCase_ForInstall_ReturnsGettingStarted()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IDocPageSeeder seeder = CreateSeeder(ctx);
        await seeder.SeedAsync();

        IDocsRepositoryPort repo = new DocsRepositoryAdapter(ctx);
        SearchDocsUseCase useCase = new(repo);

        // Act — "install" appears in getting-started content
        PaginatedEnvelope<DocSearchResultDto> result = await useCase.ExecuteAsync(
            new SearchDocsQuery { Search = "install", Page = 1, Limit = 20 });

        // Assert
        Assert.True(result.TotalCount >= 1,
            "Search for 'install' must return at least 1 result after seeding.");
        Assert.Contains(result.Data, r => r.Slug == "getting-started");
    }

    [Fact]
    public async Task AfterSeeding_SearchDocsUseCase_ForTelemetry_ReturnsTelemetryPage()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IDocPageSeeder seeder = CreateSeeder(ctx);
        await seeder.SeedAsync();

        IDocsRepositoryPort repo = new DocsRepositoryAdapter(ctx);
        SearchDocsUseCase useCase = new(repo);

        // Act — "telemetry" appears in privacy-and-telemetry content
        PaginatedEnvelope<DocSearchResultDto> result = await useCase.ExecuteAsync(
            new SearchDocsQuery { Search = "telemetry", Page = 1, Limit = 20 });

        // Assert
        Assert.True(result.TotalCount >= 1,
            "Search for 'telemetry' must return at least 1 result after seeding.");
        Assert.Contains(result.Data, r => r.Slug == "privacy-and-telemetry");
    }

    [Fact]
    public async Task AfterSeeding_SearchDocsUseCase_ReturnsResultsWithNonEmptySnippets()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IDocPageSeeder seeder = CreateSeeder(ctx);
        await seeder.SeedAsync();

        IDocsRepositoryPort repo = new DocsRepositoryAdapter(ctx);
        SearchDocsUseCase useCase = new(repo);

        // Act
        PaginatedEnvelope<DocSearchResultDto> result = await useCase.ExecuteAsync(
            new SearchDocsQuery { Search = "plugin", Page = 1, Limit = 20 });

        // Assert: at least one result with a non-empty snippet and positive relevance score
        Assert.True(result.TotalCount >= 1,
            "Search for 'plugin' must return at least 1 seeded doc page.");

        Assert.All(result.Data, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Snippet),
                $"Snippet must be non-empty for search result '{item.Slug}'.");
            Assert.True(item.RelevanceScore > 0f,
                $"RelevanceScore must be positive for search result '{item.Slug}'.");
        });
    }

    // =========================================================================
    // Test 4a — 22.4: Per-plugin README surfacing — plugin WITH readme_text
    //
    // Reconfirms Group 9 behavior end-to-end using GetDocPageUseCase.
    // After seeding a plugin (with readme_text), GetDocPageUseCase("plugin:{slug}")
    // returns the README-derived doc page.
    // =========================================================================

    [Fact]
    public async Task PluginWithReadme_GetDocPageUseCase_ReturnsReadmeDerivedDocPage()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        // Insert a plugin with a version that has readme_text
        PluginEntity plugin = MakePlugin("Docs Test Plugin", "docs-test-plugin");
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        const string readmeContent =
            "# Docs Test Plugin\n\nThis plugin provides documentation testing capabilities. " +
            "Install with `claude plugin install docs-test-plugin` to get started.";

        ctx.PluginVersions.Add(MakeVersion(plugin.Id, "1.0.0", readmeText: readmeContent));
        await ctx.SaveChangesAsync();

        IDocsRepositoryPort repo = new DocsRepositoryAdapter(ctx);
        GetDocPageUseCase useCase = new(repo);

        // Act — slug convention: "plugin:{plugin-slug}"
        DocPageDto page = await useCase.ExecuteAsync($"plugin:{plugin.Slug}");

        // Assert: slug, title extracted from H1, content from readme_text
        Assert.Equal($"plugin:{plugin.Slug}", page.Slug);
        Assert.Equal("Docs Test Plugin", page.Title);
        Assert.Contains("documentation testing capabilities", page.ContentMarkdown);
    }

    [Fact]
    public async Task PluginWithReadme_GetDocPageUseCase_TitleExtractedFromH1Heading()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        PluginEntity plugin = MakePlugin("H1 Title Plugin", "h1-title-plugin");
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        ctx.PluginVersions.Add(MakeVersion(
            plugin.Id,
            "2.0.0",
            readmeText: "# My Custom Title\n\nDescription goes here."));
        await ctx.SaveChangesAsync();

        IDocsRepositoryPort repo = new DocsRepositoryAdapter(ctx);
        GetDocPageUseCase useCase = new(repo);

        // Act
        DocPageDto page = await useCase.ExecuteAsync($"plugin:{plugin.Slug}");

        // Assert: title is extracted from the H1 heading in the README
        Assert.Equal("My Custom Title", page.Title);
    }

    // =========================================================================
    // Test 4b — 22.4: Per-plugin README surfacing — plugin WITH null readme_text
    //           → graceful placeholder "No detailed documentation provided"
    // =========================================================================

    [Fact]
    public async Task PluginWithNullReadme_GetDocPageUseCase_ReturnsGracefulPlaceholder()
    {
        // Spec (Group 9): "Broken or missing README handled gracefully"
        // "THEN the marketplace displays: 'No detailed documentation provided'"
        // "AND the plugin remains functional and installable"

        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        PluginEntity plugin = MakePlugin("Silent Plugin 22", "silent-plugin-22");
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        // Version with null readme_text
        ctx.PluginVersions.Add(MakeVersion(plugin.Id, "1.0.0", readmeText: null));
        await ctx.SaveChangesAsync();

        IDocsRepositoryPort repo = new DocsRepositoryAdapter(ctx);
        GetDocPageUseCase useCase = new(repo);

        // Act — must NOT throw
        DocPageDto page = await useCase.ExecuteAsync($"plugin:{plugin.Slug}");

        // Assert: placeholder text returned
        Assert.Contains("No detailed documentation provided", page.ContentMarkdown);
    }

    [Fact]
    public async Task PluginWithNullReadme_GetDocPageUseCase_PlaceholderPageHasNonEmptySlugAndTitle()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        PluginEntity plugin = MakePlugin("Empty Readme Plugin", "empty-readme-plugin");
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        ctx.PluginVersions.Add(MakeVersion(plugin.Id, "1.0.0", readmeText: null));
        await ctx.SaveChangesAsync();

        IDocsRepositoryPort repo = new DocsRepositoryAdapter(ctx);
        GetDocPageUseCase useCase = new(repo);

        // Act
        DocPageDto page = await useCase.ExecuteAsync($"plugin:{plugin.Slug}");

        // Assert: slug and title are still populated on the placeholder
        Assert.False(string.IsNullOrWhiteSpace(page.Slug));
        Assert.False(string.IsNullOrWhiteSpace(page.Title));
    }

    [Fact]
    public async Task NonExistentPlugin_GetDocPageUseCase_ThrowsDocNotFoundException()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IDocsRepositoryPort repo = new DocsRepositoryAdapter(ctx);
        GetDocPageUseCase useCase = new(repo);

        // Act + Assert: requesting docs for a plugin that doesn't exist must throw DocNotFoundException
        await Assert.ThrowsAsync<DocNotFoundException>(
            () => useCase.ExecuteAsync("plugin:plugin-that-does-not-exist-22"));
    }
}
