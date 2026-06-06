using ClaudeForge.Application.Modules.PluginSearch.Ports;
using ClaudeForge.Application.Modules.PluginSearch.UseCases;
using ClaudeForge.Core.Shared.Model;
using NSubstitute;

namespace ClaudeForge.Tests.Unit.PluginSearch;

/// <summary>
/// Unit tests for Group 7 (tasks 7.3): SearchPluginsUseCase.
///
/// Uses NSubstitute mocks — no real database.
///
/// Expected production types (coder MUST match these names exactly):
///
///   Namespace:   ClaudeForge.Application.Modules.PluginSearch.Ports
///     ISearchIndexPort
///       Task&lt;(IReadOnlyList&lt;SearchResultDto&gt; Items, int TotalCount)&gt; SearchAsync(
///           SearchCriteria criteria,
///           PaginationRequest pagination,
///           CancellationToken ct = default)
///
///   Namespace:   ClaudeForge.Application.Modules.PluginSearch.UseCases
///     SearchResultDto
///       Guid Id; string Name; string Slug; string Description; float RelevanceScore;
///       long DownloadCount; string? LatestVersion; DateTimeOffset CreatedAt;
///       IReadOnlyList&lt;string&gt; Types; IReadOnlyList&lt;string&gt; Languages; IReadOnlyList&lt;string&gt; UseCases;
///
///     SearchCriteria
///       string? Query; IReadOnlyList&lt;string&gt;? TypeFilter; IReadOnlyList&lt;string&gt;? LanguageFilter;
///       IReadOnlyList&lt;string&gt;? UseCaseFilter;
///
///     SearchPluginsQuery
///       string? Q; IReadOnlyList&lt;string&gt;? TypeFilter; IReadOnlyList&lt;string&gt;? LanguageFilter;
///       IReadOnlyList&lt;string&gt;? UseCaseFilter; int Page = 1; int Limit = 20;
///
///     SearchPluginsUseCase(ISearchIndexPort index)
///       Task&lt;SearchPluginsResult&gt; ExecuteAsync(SearchPluginsQuery query, CancellationToken ct = default)
///
///     SearchPluginsResult
///       PaginatedEnvelope&lt;SearchResultDto&gt; Envelope;
///       IReadOnlyList&lt;string&gt; CategorySuggestions;    // Populated when Envelope.Data is empty
///
///     InvalidPaginationException : ProblemDetailsException
///       Message must contain "Page and limit must be greater than 0"
///       (spec §Pagination: "Page and limit must be greater than 0")
/// </summary>
public sealed class SearchPluginsUseCaseTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static SearchResultDto MakeResult(
        string name,
        float relevanceScore = 0.8f,
        long downloadCount = 0) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Slug = name.ToLowerInvariant(),
        Description = $"Description of {name}",
        RelevanceScore = relevanceScore,
        DownloadCount = downloadCount,
        LatestVersion = "1.0.0",
        CreatedAt = DateTimeOffset.UtcNow,
        Types = [],
        Languages = [],
        UseCases = [],
    };

    // -------------------------------------------------------------------------
    // 7.3 — Query passthrough to port
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SearchPlugins_QueryPassthrough_SendsCorrectCriteriaToPort()
    {
        // Arrange
        ISearchIndexPort index = Substitute.For<ISearchIndexPort>();
        index.SearchAsync(Arg.Any<SearchCriteria>(), Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<SearchResultDto>)[MakeResult("AuthHelper")], 1));

        SearchPluginsUseCase useCase = new(index);
        SearchPluginsQuery query = new()
        {
            Q = "auth",
            TypeFilter = ["skill"],
            LanguageFilter = ["typescript"],
            Page = 1,
            Limit = 10,
        };

        // Act
        SearchPluginsResult result = await useCase.ExecuteAsync(query);

        // Assert — port was called with matching criteria
        await index.Received(1).SearchAsync(
            Arg.Is<SearchCriteria>(c =>
                c.Query == "auth" &&
                c.TypeFilter != null && c.TypeFilter.Contains("skill") &&
                c.LanguageFilter != null && c.LanguageFilter.Contains("typescript")),
            Arg.Is<PaginationRequest>(p => p.Page == 1 && p.Limit == 10),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchPlugins_NullQuery_PassesNullQueryThroughToPort()
    {
        // Arrange
        ISearchIndexPort index = Substitute.For<ISearchIndexPort>();
        index.SearchAsync(Arg.Any<SearchCriteria>(), Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<SearchResultDto>)[], 0));

        SearchPluginsUseCase useCase = new(index);
        SearchPluginsQuery query = new() { Q = null };

        // Act
        SearchPluginsResult result = await useCase.ExecuteAsync(query);

        // Assert
        await index.Received(1).SearchAsync(
            Arg.Is<SearchCriteria>(c => c.Query == null),
            Arg.Any<PaginationRequest>(),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // 7.3 — Filter combination: OR within a dimension, AND across dimensions
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SearchPlugins_TypeAndLanguageFilters_BothPassedToCriteriaAsSpecified()
    {
        // Arrange
        ISearchIndexPort index = Substitute.For<ISearchIndexPort>();
        index.SearchAsync(Arg.Any<SearchCriteria>(), Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<SearchResultDto>)[MakeResult("CombinedPlugin")], 1));

        SearchPluginsUseCase useCase = new(index);
        // Multiple types = OR within type dimension
        // Type AND Language = AND across dimensions
        SearchPluginsQuery query = new()
        {
            Q = "auth",
            TypeFilter = ["skill", "hook"],        // OR within type
            LanguageFilter = ["typescript"],         // AND with above
        };

        // Act
        SearchPluginsResult result = await useCase.ExecuteAsync(query);

        // Assert — both filters forwarded; semantics implemented in adapter
        await index.Received(1).SearchAsync(
            Arg.Is<SearchCriteria>(c =>
                c.TypeFilter != null && c.TypeFilter.Count == 2 &&
                c.TypeFilter.Contains("skill") && c.TypeFilter.Contains("hook") &&
                c.LanguageFilter != null && c.LanguageFilter.Contains("typescript")),
            Arg.Any<PaginationRequest>(),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // 7.3 — Invalid pagination → 400
    // Spec: "Page and limit must be greater than 0"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SearchPlugins_PageZero_ThrowsInvalidPaginationException()
    {
        // Arrange
        ISearchIndexPort index = Substitute.For<ISearchIndexPort>();
        SearchPluginsUseCase useCase = new(index);
        SearchPluginsQuery query = new() { Q = "auth", Page = 0 };

        // Act & Assert
        InvalidPaginationException ex = await Assert.ThrowsAsync<InvalidPaginationException>(
            () => useCase.ExecuteAsync(query));

        // Spec verbatim: "Page and limit must be greater than 0"
        Assert.Contains("Page and limit must be greater than 0", ex.Message);
    }

    [Fact]
    public async Task SearchPlugins_LimitZero_ThrowsInvalidPaginationException()
    {
        // Arrange
        ISearchIndexPort index = Substitute.For<ISearchIndexPort>();
        SearchPluginsUseCase useCase = new(index);
        SearchPluginsQuery query = new() { Q = "auth", Limit = 0 };

        // Act & Assert
        InvalidPaginationException ex = await Assert.ThrowsAsync<InvalidPaginationException>(
            () => useCase.ExecuteAsync(query));

        Assert.Contains("Page and limit must be greater than 0", ex.Message);
    }

    [Fact]
    public async Task SearchPlugins_NegativePage_ThrowsInvalidPaginationException()
    {
        // Arrange
        ISearchIndexPort index = Substitute.For<ISearchIndexPort>();
        SearchPluginsUseCase useCase = new(index);
        SearchPluginsQuery query = new() { Q = "test", Page = -1 };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidPaginationException>(
            () => useCase.ExecuteAsync(query));
    }

    // -------------------------------------------------------------------------
    // 7.3 — Empty result: returns empty envelope + category suggestions
    // Spec: "No plugins found matching your search"
    //       "result includes suggested categories or popular plugins to explore instead"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SearchPlugins_NoResults_ReturnsEmptyEnvelopeWithCategorySuggestions()
    {
        // Arrange
        ISearchIndexPort index = Substitute.For<ISearchIndexPort>();
        index.SearchAsync(Arg.Any<SearchCriteria>(), Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<SearchResultDto>)[], 0));

        SearchPluginsUseCase useCase = new(index);
        SearchPluginsQuery query = new() { Q = "nonexistent-xyz-12345" };

        // Act
        SearchPluginsResult result = await useCase.ExecuteAsync(query);

        // Assert — empty data AND suggestions present (spec requirement)
        Assert.Empty(result.Envelope.Data);
        Assert.Equal(0, result.Envelope.TotalCount);
        Assert.NotEmpty(result.CategorySuggestions); // spec: "suggested categories or popular plugins"
    }

    [Fact]
    public async Task SearchPlugins_WithResults_CategorySuggestionsIsEmpty()
    {
        // Arrange
        ISearchIndexPort index = Substitute.For<ISearchIndexPort>();
        index.SearchAsync(Arg.Any<SearchCriteria>(), Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<SearchResultDto>)[MakeResult("AuthHelper")], 1));

        SearchPluginsUseCase useCase = new(index);
        SearchPluginsQuery query = new() { Q = "auth" };

        // Act
        SearchPluginsResult result = await useCase.ExecuteAsync(query);

        // Assert — results present; no suggestions needed
        Assert.NotEmpty(result.Envelope.Data);
        Assert.Empty(result.CategorySuggestions);
    }

    // -------------------------------------------------------------------------
    // 7.3 — Paginated envelope shape
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SearchPlugins_WithResults_ReturnsCorrectPaginatedEnvelopeShape()
    {
        // Arrange
        ISearchIndexPort index = Substitute.For<ISearchIndexPort>();
        IReadOnlyList<SearchResultDto> items = [MakeResult("Plugin1"), MakeResult("Plugin2")];
        index.SearchAsync(Arg.Any<SearchCriteria>(), Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>())
            .Returns((items, 10));

        SearchPluginsUseCase useCase = new(index);
        SearchPluginsQuery query = new() { Q = "plugin", Page = 2, Limit = 2 };

        // Act
        SearchPluginsResult result = await useCase.ExecuteAsync(query);

        // Assert
        Assert.Equal(2, result.Envelope.Data.Count);
        Assert.Equal(10, result.Envelope.TotalCount);
        Assert.Equal(2, result.Envelope.Page);
        Assert.Equal(2, result.Envelope.Limit);
        Assert.Equal(5, result.Envelope.TotalPages); // ceil(10/2) = 5
    }
}
