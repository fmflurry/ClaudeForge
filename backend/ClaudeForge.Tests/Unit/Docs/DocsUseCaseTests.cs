using ClaudeForge.Application.Modules.Docs.Ports;
using ClaudeForge.Application.Modules.Docs.UseCases;
using ClaudeForge.Core.Shared.Exceptions;
using ClaudeForge.Core.Shared.Model;
using NSubstitute;

namespace ClaudeForge.Tests.Unit.Docs;

/// <summary>
/// Unit tests for Group 9 (tasks 9.1, 9.3): SearchDocsUseCase and GetDocPageUseCase.
///
/// Uses NSubstitute mocks — no real database.
///
/// Expected production types (coder MUST match these names exactly):
///
///   Namespace: ClaudeForge.Application.Modules.Docs.Ports
///     IDocsRepositoryPort
///       Task&lt;(IReadOnlyList&lt;DocSearchResultDto&gt; Items, int TotalCount)&gt; SearchAsync(
///           string query, PaginationRequest pagination, CancellationToken ct = default)
///       Task&lt;DocPageDto?&gt; GetBySlugAsync(string slug, CancellationToken ct = default)
///
///     DocSearchResultDto
///       string Slug; string Title; string Category; string Snippet; float RelevanceScore
///
///     DocPageDto
///       string Slug; string Title; string Category; string ContentMarkdown;
///       DateTimeOffset LastUpdated
///
///   Namespace: ClaudeForge.Application.Modules.Docs.UseCases
///     SearchDocsQuery
///       string? Search; int Page = 1; int Limit = 20
///
///     SearchDocsUseCase(IDocsRepositoryPort repo)
///       Task&lt;PaginatedEnvelope&lt;DocSearchResultDto&gt;&gt; ExecuteAsync(
///           SearchDocsQuery query, CancellationToken ct = default)
///
///     GetDocPageUseCase(IDocsRepositoryPort repo)
///       Task&lt;DocPageDto&gt; ExecuteAsync(string slug, CancellationToken ct = default)
///
///     DocNotFoundException : ProblemDetailsException
///       (StatusCode = 404, message = "Documentation page not found")
///
/// Spec scenarios (docs/spec.md):
///   "Full-text search across all docs"
///     WHEN a user enters a search term
///     THEN results are ranked by relevance (title match > content match)
///     AND up to 20 results are displayed with pagination
///
///   "Plugin lacks documentation displays placeholder"
///     WHEN a user views a plugin with no documentation
///     THEN the Documentation section displays "No documentation available yet"
///
///   "Missing or Incomplete Documentation Handling"
///     The system SHALL gracefully handle plugins with missing documentation.
///     Missing docs are not hidden; instead, clear guidance is provided.
///
///   "Broken or missing README handled gracefully"
///     WHEN a plugin package lacks a README or the link is broken
///     THEN the marketplace displays a placeholder: "No detailed documentation provided"
///     AND the plugin remains functional and installable
/// </summary>
public sealed class DocsUseCaseTests
{
    // -------------------------------------------------------------------------
    // Seed helpers
    // -------------------------------------------------------------------------

    private static DocSearchResultDto MakeSearchResult(
        string slug,
        string title,
        string category = "Getting Started",
        float relevanceScore = 0.9f) =>
        new()
        {
            Slug = slug,
            Title = title,
            Category = category,
            Snippet = $"Snippet for {title}",
            RelevanceScore = relevanceScore,
        };

    private static DocPageDto MakeDocPage(
        string slug,
        string title,
        string category = "Getting Started",
        string contentMarkdown = "# Hello\nThis is content.",
        DateTimeOffset? lastUpdated = null) =>
        new()
        {
            Slug = slug,
            Title = title,
            Category = category,
            ContentMarkdown = contentMarkdown,
            LastUpdated = lastUpdated ?? DateTimeOffset.UtcNow,
        };

    // -------------------------------------------------------------------------
    // SearchDocsUseCase — happy path: returns paginated envelope
    // Spec: "results are ranked by relevance (title match > content match)"
    //       "up to 20 results are displayed with pagination"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SearchDocs_ValidQuery_ReturnsPaginatedEnvelopeFromRepository()
    {
        // Arrange
        IDocsRepositoryPort repo = Substitute.For<IDocsRepositoryPort>();
        IReadOnlyList<DocSearchResultDto> results =
        [
            MakeSearchResult("getting-started", "Getting Started", relevanceScore: 0.95f),
            MakeSearchResult("installation-guide", "Installation Guide", relevanceScore: 0.80f),
        ];
        repo.SearchAsync("install", Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>())
            .Returns((results, 2));

        SearchDocsUseCase useCase = new(repo);
        SearchDocsQuery query = new() { Search = "install", Page = 1, Limit = 20 };

        // Act
        PaginatedEnvelope<DocSearchResultDto> envelope = await useCase.ExecuteAsync(query);

        // Assert
        Assert.Equal(2, envelope.TotalCount);
        Assert.Equal(2, envelope.Data.Count);
        Assert.Equal(1, envelope.Page);
        Assert.Equal(20, envelope.Limit);
    }

    [Fact]
    public async Task SearchDocs_ValidQuery_PassesQueryAndPaginationToRepository()
    {
        // Arrange
        IDocsRepositoryPort repo = Substitute.For<IDocsRepositoryPort>();
        repo.SearchAsync(Arg.Any<string>(), Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<DocSearchResultDto>(), 0));

        SearchDocsUseCase useCase = new(repo);
        SearchDocsQuery query = new() { Search = "configure", Page = 2, Limit = 10 };

        // Act
        await useCase.ExecuteAsync(query);

        // Assert — repo must receive the search term and correct pagination
        await repo.Received(1).SearchAsync(
            "configure",
            Arg.Is<PaginationRequest>(p => p.Page == 2 && p.Limit == 10),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchDocs_ValidQuery_PassesCancellationTokenToRepository()
    {
        // Arrange
        IDocsRepositoryPort repo = Substitute.For<IDocsRepositoryPort>();
        repo.SearchAsync(Arg.Any<string>(), Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<DocSearchResultDto>(), 0));

        SearchDocsUseCase useCase = new(repo);
        SearchDocsQuery query = new() { Search = "search" };
        using CancellationTokenSource cts = new();

        // Act
        await useCase.ExecuteAsync(query, cts.Token);

        // Assert
        await repo.Received(1).SearchAsync(
            Arg.Any<string>(),
            Arg.Any<PaginationRequest>(),
            Arg.Is<CancellationToken>(t => t == cts.Token));
    }

    // -------------------------------------------------------------------------
    // SearchDocsUseCase — empty query or no match → empty envelope (no exception)
    // Spec: "Full-text search across all docs"
    //       results can be empty — must not throw, total=0
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchDocs_EmptyOrNullQuery_ReturnsEmptyEnvelopeGracefully(string? searchTerm)
    {
        // Arrange
        IDocsRepositoryPort repo = Substitute.For<IDocsRepositoryPort>();
        repo.SearchAsync(Arg.Any<string>(), Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<DocSearchResultDto>(), 0));

        SearchDocsUseCase useCase = new(repo);
        SearchDocsQuery query = new() { Search = searchTerm };

        // Act — must NOT throw even with blank/null search
        PaginatedEnvelope<DocSearchResultDto> envelope = await useCase.ExecuteAsync(query);

        // Assert
        Assert.Equal(0, envelope.TotalCount);
        Assert.Empty(envelope.Data);
    }

    [Fact]
    public async Task SearchDocs_NoMatchingResults_ReturnsEmptyEnvelopeWithZeroTotal()
    {
        // Arrange
        IDocsRepositoryPort repo = Substitute.For<IDocsRepositoryPort>();
        repo.SearchAsync("xyzzy-nonexistent", Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<DocSearchResultDto>(), 0));

        SearchDocsUseCase useCase = new(repo);
        SearchDocsQuery query = new() { Search = "xyzzy-nonexistent" };

        // Act
        PaginatedEnvelope<DocSearchResultDto> envelope = await useCase.ExecuteAsync(query);

        // Assert
        Assert.Equal(0, envelope.TotalCount);
        Assert.Empty(envelope.Data);
    }

    // -------------------------------------------------------------------------
    // SearchDocsUseCase — limit cap: Limit is capped at 20 per spec
    // Spec: "up to 20 results are displayed with pagination"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SearchDocs_LimitAboveCap_ClampsLimitToTwenty()
    {
        // Arrange
        IDocsRepositoryPort repo = Substitute.For<IDocsRepositoryPort>();
        repo.SearchAsync(Arg.Any<string>(), Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<DocSearchResultDto>(), 0));

        SearchDocsUseCase useCase = new(repo);
        // Requesting 50 results but spec caps docs search at 20
        SearchDocsQuery query = new() { Search = "test", Page = 1, Limit = 50 };

        // Act
        await useCase.ExecuteAsync(query);

        // Assert — the pagination passed to the repo must have Limit <= 20
        await repo.Received(1).SearchAsync(
            Arg.Any<string>(),
            Arg.Is<PaginationRequest>(p => p.Limit <= 20),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // SearchDocsUseCase — ranking passthrough: results returned in order from repo
    // Spec: "results are ranked by relevance (title match > content match)"
    // The use-case does NOT re-sort; it relies on the repo to return ranked results.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SearchDocs_RankedResults_ReturnsResultsInOrderFromRepository()
    {
        // Arrange
        IDocsRepositoryPort repo = Substitute.For<IDocsRepositoryPort>();
        IReadOnlyList<DocSearchResultDto> rankedResults =
        [
            MakeSearchResult("getting-started", "Getting Started", relevanceScore: 1.0f),   // title match — highest
            MakeSearchResult("faq", "FAQ", relevanceScore: 0.75f),                           // content match — lower
            MakeSearchResult("telemetry", "Telemetry", relevanceScore: 0.50f),               // weak match
        ];
        repo.SearchAsync(Arg.Any<string>(), Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>())
            .Returns((rankedResults, 3));

        SearchDocsUseCase useCase = new(repo);
        SearchDocsQuery query = new() { Search = "getting started" };

        // Act
        PaginatedEnvelope<DocSearchResultDto> envelope = await useCase.ExecuteAsync(query);

        // Assert — order must be preserved from repository (ranked by relevance)
        Assert.Equal(3, envelope.Data.Count);
        Assert.True(envelope.Data[0].RelevanceScore >= envelope.Data[1].RelevanceScore);
        Assert.True(envelope.Data[1].RelevanceScore >= envelope.Data[2].RelevanceScore);
        Assert.Equal("Getting Started", envelope.Data[0].Title);
    }

    // -------------------------------------------------------------------------
    // SearchDocsUseCase — pagination metadata correctness
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SearchDocs_Pagination_EnvelopeContainsCorrectPageMetadata()
    {
        // Arrange
        IDocsRepositoryPort repo = Substitute.For<IDocsRepositoryPort>();
        IReadOnlyList<DocSearchResultDto> pageItems =
        [
            MakeSearchResult("page-3-item-1", "Page 3 Item 1"),
        ];
        // Simulate 41 total items; page=3, limit=20 → 3rd page, 1 item on last page
        repo.SearchAsync(Arg.Any<string>(), Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>())
            .Returns((pageItems, 41));

        SearchDocsUseCase useCase = new(repo);
        SearchDocsQuery query = new() { Search = "query", Page = 3, Limit = 20 };

        // Act
        PaginatedEnvelope<DocSearchResultDto> envelope = await useCase.ExecuteAsync(query);

        // Assert
        Assert.Equal(41, envelope.TotalCount);
        Assert.Equal(3, envelope.Page);
        Assert.Equal(20, envelope.Limit);
        Assert.Equal(3, envelope.TotalPages);  // ceil(41/20) = 3
        Assert.Single(envelope.Data);
    }

    // -------------------------------------------------------------------------
    // GetDocPageUseCase — happy path: returns page with markdown + lastUpdated
    // Spec: "GET /api/v1/docs/{slug} → 200 doc page"
    //       "Response: { slug, title, content (markdown), last_updated }"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetDocPage_ExistingSlug_ReturnsDocPageWithMarkdownAndLastUpdated()
    {
        // Arrange
        IDocsRepositoryPort repo = Substitute.For<IDocsRepositoryPort>();
        DateTimeOffset lastUpdated = new(2025, 3, 15, 10, 0, 0, TimeSpan.Zero);
        DocPageDto page = MakeDocPage(
            "getting-started",
            "Getting Started",
            contentMarkdown: "# Getting Started\nInstall the CLI...",
            lastUpdated: lastUpdated);
        repo.GetBySlugAsync("getting-started", Arg.Any<CancellationToken>()).Returns(page);

        GetDocPageUseCase useCase = new(repo);

        // Act
        DocPageDto result = await useCase.ExecuteAsync("getting-started");

        // Assert
        Assert.Equal("getting-started", result.Slug);
        Assert.Equal("Getting Started", result.Title);
        Assert.Equal("# Getting Started\nInstall the CLI...", result.ContentMarkdown);
        Assert.Equal(lastUpdated, result.LastUpdated);
        Assert.Equal("Getting Started", result.Category);
    }

    [Fact]
    public async Task GetDocPage_ExistingSlug_PassesCancellationTokenToRepository()
    {
        // Arrange
        IDocsRepositoryPort repo = Substitute.For<IDocsRepositoryPort>();
        repo.GetBySlugAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(MakeDocPage("some-slug", "Some Page"));

        GetDocPageUseCase useCase = new(repo);
        using CancellationTokenSource cts = new();

        // Act
        await useCase.ExecuteAsync("some-slug", cts.Token);

        // Assert
        await repo.Received(1).GetBySlugAsync(
            "some-slug",
            Arg.Is<CancellationToken>(t => t == cts.Token));
    }

    // -------------------------------------------------------------------------
    // GetDocPageUseCase — unknown slug → DocNotFoundException (404)
    // Spec: "GET /api/v1/docs/{slug} → 404/placeholder per spec"
    //       "Broken or missing README handled gracefully"
    //       Note: The spec scenario "Plugin lacks documentation displays placeholder"
    //       says "No documentation available yet" — we surface this via DocNotFoundException
    //       with StatusCode=404 so the HTTP layer can render a placeholder response.
    //       The ProblemDetails detail string is "Documentation page not found".
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetDocPage_UnknownSlug_ThrowsDocNotFoundException()
    {
        // Arrange
        IDocsRepositoryPort repo = Substitute.For<IDocsRepositoryPort>();
        repo.GetBySlugAsync("nonexistent-slug", Arg.Any<CancellationToken>())
            .Returns((DocPageDto?)null);

        GetDocPageUseCase useCase = new(repo);

        // Act & Assert
        DocNotFoundException ex = await Assert.ThrowsAsync<DocNotFoundException>(
            () => useCase.ExecuteAsync("nonexistent-slug"));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task GetDocPage_UnknownSlug_ExceptionDetailMatchesSpecString()
    {
        // Spec: graceful missing-docs handling; 404 ProblemDetails detail string
        IDocsRepositoryPort repo = Substitute.For<IDocsRepositoryPort>();
        repo.GetBySlugAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((DocPageDto?)null);

        GetDocPageUseCase useCase = new(repo);

        // Act & Assert
        DocNotFoundException ex = await Assert.ThrowsAsync<DocNotFoundException>(
            () => useCase.ExecuteAsync("missing-page"));

        Assert.Equal("Documentation page not found", ex.Message);
    }

    [Theory]
    [InlineData("nonexistent")]
    [InlineData("plugin-readme-missing")]
    [InlineData("unknown-guide-xyz")]
    public async Task GetDocPage_VariousUnknownSlugs_AllThrowDocNotFoundException(string slug)
    {
        // Arrange
        IDocsRepositoryPort repo = Substitute.For<IDocsRepositoryPort>();
        repo.GetBySlugAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((DocPageDto?)null);

        GetDocPageUseCase useCase = new(repo);

        // Act & Assert
        await Assert.ThrowsAsync<DocNotFoundException>(() => useCase.ExecuteAsync(slug));
    }

    // -------------------------------------------------------------------------
    // DocNotFoundException — is a ProblemDetailsException with StatusCode 404
    // Ensures global exception handler maps it to 404 Not Found
    // -------------------------------------------------------------------------

    [Fact]
    public void DocNotFoundException_IsAProblemDetailsException()
    {
        DocNotFoundException ex = new();
        Assert.IsAssignableFrom<ProblemDetailsException>(ex);
    }

    [Fact]
    public void DocNotFoundException_HasStatusCode404()
    {
        DocNotFoundException ex = new();
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public void DocNotFoundException_HasExpectedDetailMessage()
    {
        DocNotFoundException ex = new();
        Assert.Equal("Documentation page not found", ex.Message);
    }

    // -------------------------------------------------------------------------
    // DocSearchResultDto — shape validation via reflection
    // Spec DTO: { Slug, Title, Category, Snippet, RelevanceScore }
    // -------------------------------------------------------------------------

    [Fact]
    public void DocSearchResultDto_HasRequiredProperties()
    {
        System.Reflection.PropertyInfo[] properties =
            typeof(DocSearchResultDto).GetProperties(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        string[] propertyNames = properties.Select(p => p.Name).ToArray();

        Assert.Contains("Slug", propertyNames);
        Assert.Contains("Title", propertyNames);
        Assert.Contains("Category", propertyNames);
        Assert.Contains("Snippet", propertyNames);
        Assert.Contains("RelevanceScore", propertyNames);
    }

    // -------------------------------------------------------------------------
    // DocPageDto — shape validation via reflection
    // Spec DTO: { Slug, Title, Category, ContentMarkdown, LastUpdated }
    // Design §7: "Response: { slug, title, content (markdown), last_updated }"
    // -------------------------------------------------------------------------

    [Fact]
    public void DocPageDto_HasRequiredProperties()
    {
        System.Reflection.PropertyInfo[] properties =
            typeof(DocPageDto).GetProperties(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        string[] propertyNames = properties.Select(p => p.Name).ToArray();

        Assert.Contains("Slug", propertyNames);
        Assert.Contains("Title", propertyNames);
        Assert.Contains("Category", propertyNames);
        Assert.Contains("ContentMarkdown", propertyNames);
        Assert.Contains("LastUpdated", propertyNames);
    }

    // -------------------------------------------------------------------------
    // SearchDocsQuery — default values per spec (Page=1, Limit=20)
    // -------------------------------------------------------------------------

    [Fact]
    public void SearchDocsQuery_DefaultValues_MatchSpec()
    {
        SearchDocsQuery query = new();

        Assert.Equal(1, query.Page);
        Assert.Equal(20, query.Limit);
        Assert.Null(query.Search);
    }
}
