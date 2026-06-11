using ClaudeForge.Application.Modules.AddOnSearch.Ports;
using ClaudeForge.Application.Modules.AddOnSearch.UseCases;
using ClaudeForge.Core.Shared.Exceptions;
using NSubstitute;

namespace ClaudeForge.Tests.Unit.AddOnSearch;

/// <summary>
/// Unit tests for Group 7 (task 7.4): DiscoverAddOnsUseCase.
///
/// Uses NSubstitute mocks — no real database.
///
/// Expected production types (coder MUST match these names exactly):
///
///   Namespace:   ClaudeForge.Application.Modules.AddOnSearch.UseCases
///     DiscoverAddOnsQuery
///       string? Keyword; IReadOnlyList&lt;string&gt;? LanguageFilter; IReadOnlyList&lt;string&gt;? UseCaseFilter;
///       IReadOnlyList&lt;string&gt;? TypeFilter;
///
///     DiscoveryResultDto
///       Guid Id; string Name; string Description; string? LatestVersion;
///       IReadOnlyList&lt;string&gt; Types; IReadOnlyList&lt;string&gt; Languages; IReadOnlyList&lt;string&gt; UseCases;
///       float RelevanceScore;         // 0.0 .. 1.0 (spec: "relevance score 0-100 or 0-1.0")
///       long DownloadCount; DateTimeOffset LastUpdated; string Author;
///       string MaturityIndicator;     // spec: "new" | "stable" | "deprecated"
///
///     DiscoverAddOnsResult
///       IReadOnlyList&lt;DiscoveryResultDto&gt; Items;
///       IReadOnlyList&lt;string&gt; CriteriaEchoed;  // applied criteria echoed on empty results (spec)
///
///     DiscoverAddOnsUseCase(ISearchIndexPort index)
///       Task&lt;DiscoverAddOnsResult&gt; ExecuteAsync(DiscoverAddOnsQuery query, CancellationToken ct = default)
///
///     BlankKeywordException : ProblemDetailsException
///       Message: "Keyword cannot be empty"    (spec verbatim)
///       StatusCode: 400
///
///   ISearchIndexPort (same interface as for search) must also expose:
///     Task&lt;(IReadOnlyList&lt;DiscoveryResultDto&gt; Items, int TotalCount)&gt; DiscoverAsync(
///         SearchCriteria criteria,
///         CancellationToken ct = default)
/// </summary>
public sealed class DiscoverPluginsUseCaseTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static DiscoveryResultDto MakeDiscoveryResult(
        string name,
        float relevanceScore = 0.75f,
        string maturityIndicator = "stable") => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = $"Discovery result for {name}",
            LatestVersion = "1.0.0",
            Types = ["skill"],
            Languages = ["typescript"],
            UseCases = ["dev-team"],
            RelevanceScore = relevanceScore,
            DownloadCount = 100,
            LastUpdated = DateTimeOffset.UtcNow,
            Author = "test-author",
            MaturityIndicator = maturityIndicator,
        };

    // -------------------------------------------------------------------------
    // 7.4 — Blank keyword → 400 "Keyword cannot be empty"
    // Spec verbatim: "Keyword cannot be empty"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DiscoverPlugins_EmptyKeyword_ThrowsBlankKeywordException()
    {
        // Arrange
        ISearchIndexPort index = Substitute.For<ISearchIndexPort>();
        DiscoverAddOnsUseCase useCase = new(index);
        DiscoverAddOnsQuery query = new() { Keyword = "" };

        // Act & Assert
        BlankKeywordException ex = await Assert.ThrowsAsync<BlankKeywordException>(
            () => useCase.ExecuteAsync(query));

        // Spec verbatim string:
        Assert.Equal("Keyword cannot be empty", ex.Message);
    }

    [Fact]
    public async Task DiscoverPlugins_WhitespaceOnlyKeyword_ThrowsBlankKeywordException()
    {
        // Arrange
        ISearchIndexPort index = Substitute.For<ISearchIndexPort>();
        DiscoverAddOnsUseCase useCase = new(index);
        DiscoverAddOnsQuery query = new() { Keyword = "   " };

        // Act & Assert
        BlankKeywordException ex = await Assert.ThrowsAsync<BlankKeywordException>(
            () => useCase.ExecuteAsync(query));

        Assert.Equal("Keyword cannot be empty", ex.Message);
    }

    [Fact]
    public async Task DiscoverPlugins_NullKeyword_ThrowsBlankKeywordException()
    {
        // Arrange
        ISearchIndexPort index = Substitute.For<ISearchIndexPort>();
        DiscoverAddOnsUseCase useCase = new(index);
        DiscoverAddOnsQuery query = new() { Keyword = null };

        // Act & Assert
        BlankKeywordException ex = await Assert.ThrowsAsync<BlankKeywordException>(
            () => useCase.ExecuteAsync(query));

        Assert.Equal("Keyword cannot be empty", ex.Message);
    }

    [Fact]
    public async Task DiscoverPlugins_BlankKeywordException_IsA400ProblemDetailsException()
    {
        // Assert the exception hierarchy maps to 400
        ISearchIndexPort index = Substitute.For<ISearchIndexPort>();
        DiscoverAddOnsUseCase useCase = new(index);
        DiscoverAddOnsQuery query = new() { Keyword = "" };

        ProblemDetailsException ex = await Assert.ThrowsAsync<BlankKeywordException>(
            () => useCase.ExecuteAsync(query));

        Assert.Equal(400, ex.StatusCode);
    }

    // -------------------------------------------------------------------------
    // 7.4 — Relevance score 0..1
    // Spec: "each result includes a relevance score (0-100 or 0-1.0)"
    // We normalise to 0..1 (float) per design §4.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DiscoverPlugins_WithResults_RelevanceScoreIsBetweenZeroAndOne()
    {
        // Arrange
        ISearchIndexPort index = Substitute.For<ISearchIndexPort>();
        index.DiscoverAsync(Arg.Any<SearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<DiscoveryResultDto>)
                [
                    MakeDiscoveryResult("LogHelper", relevanceScore: 0.95f),
                    MakeDiscoveryResult("LogWriter", relevanceScore: 0.60f),
                ], 2));

        DiscoverAddOnsUseCase useCase = new(index);
        DiscoverAddOnsQuery query = new() { Keyword = "logging" };

        // Act
        DiscoverAddOnsResult result = await useCase.ExecuteAsync(query);

        // Assert — all scores within [0, 1]
        Assert.All(result.Items, item =>
        {
            Assert.InRange(item.RelevanceScore, 0.0f, 1.0f);
        });
    }

    [Fact]
    public async Task DiscoverPlugins_ResultsSortedByRelevanceDescending()
    {
        // Arrange — port returns items in arbitrary order; use case must sort by score desc
        ISearchIndexPort index = Substitute.For<ISearchIndexPort>();
        index.DiscoverAsync(Arg.Any<SearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<DiscoveryResultDto>)
                [
                    MakeDiscoveryResult("LowRelevance", relevanceScore: 0.30f),
                    MakeDiscoveryResult("HighRelevance", relevanceScore: 0.95f),
                    MakeDiscoveryResult("MidRelevance", relevanceScore: 0.65f),
                ], 3));

        DiscoverAddOnsUseCase useCase = new(index);
        DiscoverAddOnsQuery query = new() { Keyword = "logging" };

        // Act
        DiscoverAddOnsResult result = await useCase.ExecuteAsync(query);

        // Assert — descending order
        Assert.Equal("HighRelevance", result.Items[0].Name);
        Assert.Equal("MidRelevance", result.Items[1].Name);
        Assert.Equal("LowRelevance", result.Items[2].Name);
    }

    // -------------------------------------------------------------------------
    // 7.4 — Criteria echo on empty results
    // Spec: "response includes which criteria were applied"
    //       "suggests relaxing one or more criteria to expand results"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DiscoverPlugins_NoResults_CriteriaAreEchoedInResponse()
    {
        // Arrange
        ISearchIndexPort index = Substitute.For<ISearchIndexPort>();
        index.DiscoverAsync(Arg.Any<SearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<DiscoveryResultDto>)[], 0));

        DiscoverAddOnsUseCase useCase = new(index);
        DiscoverAddOnsQuery query = new()
        {
            Keyword = "nonexistent-xyz",
            LanguageFilter = ["python"],
            UseCaseFilter = ["devops"],
            TypeFilter = ["hook"],
        };

        // Act
        DiscoverAddOnsResult result = await useCase.ExecuteAsync(query);

        // Assert — empty results + criteria echoed (spec requirement)
        Assert.Empty(result.Items);
        Assert.NotEmpty(result.CriteriaEchoed);
        // The echoed criteria should reference the keyword and filters
        Assert.Contains(result.CriteriaEchoed, s => s.Contains("nonexistent-xyz"));
    }

    [Fact]
    public async Task DiscoverPlugins_WithResults_CriteriaEchoedIsEmpty()
    {
        // Arrange
        ISearchIndexPort index = Substitute.For<ISearchIndexPort>();
        index.DiscoverAsync(Arg.Any<SearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<DiscoveryResultDto>)[MakeDiscoveryResult("LogHelper")], 1));

        DiscoverAddOnsUseCase useCase = new(index);
        DiscoverAddOnsQuery query = new() { Keyword = "logging" };

        // Act
        DiscoverAddOnsResult result = await useCase.ExecuteAsync(query);

        // Assert — results found; no criteria echo needed
        Assert.NotEmpty(result.Items);
        Assert.Empty(result.CriteriaEchoed);
    }

    // -------------------------------------------------------------------------
    // 7.4 — Contextual metadata: all languages + maturity indicator
    // Spec: "each result displays all supported languages, not just the filtered one"
    //       "includes an indicator of plugin maturity (new, stable, deprecated)"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DiscoverPlugins_Results_IncludeAllSupportedLanguages()
    {
        // Arrange
        ISearchIndexPort index = Substitute.For<ISearchIndexPort>();
        DiscoveryResultDto resultWithMultipleLangs = new()
        {
            Id = Guid.NewGuid(),
            Name = "MultiLangPlugin",
            Description = "Supports many languages",
            LatestVersion = "2.0.0",
            Types = ["skill"],
            // Filtered by TypeScript but result shows ALL languages
            Languages = ["typescript", "python", "go"],
            UseCases = ["dev-team"],
            RelevanceScore = 0.9f,
            DownloadCount = 500,
            LastUpdated = DateTimeOffset.UtcNow,
            Author = "polyglot-author",
            MaturityIndicator = "stable",
        };

        index.DiscoverAsync(Arg.Any<SearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<DiscoveryResultDto>)[resultWithMultipleLangs], 1));

        DiscoverAddOnsUseCase useCase = new(index);
        DiscoverAddOnsQuery query = new() { Keyword = "auth", LanguageFilter = ["typescript"] };

        // Act
        DiscoverAddOnsResult result = await useCase.ExecuteAsync(query);

        // Assert — all 3 languages present (not just the filtered one)
        Assert.Single(result.Items);
        DiscoveryResultDto item = result.Items[0];
        Assert.Equal(3, item.Languages.Count);
        Assert.Contains("typescript", item.Languages);
        Assert.Contains("python", item.Languages);
        Assert.Contains("go", item.Languages);
    }

    [Theory]
    [InlineData("new")]
    [InlineData("stable")]
    [InlineData("deprecated")]
    public async Task DiscoverPlugins_Results_IncludeMaturityIndicator(string maturity)
    {
        // Arrange
        ISearchIndexPort index = Substitute.For<ISearchIndexPort>();
        index.DiscoverAsync(Arg.Any<SearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<DiscoveryResultDto>)
                [MakeDiscoveryResult("TestPlugin", maturityIndicator: maturity)], 1));

        DiscoverAddOnsUseCase useCase = new(index);
        DiscoverAddOnsQuery query = new() { Keyword = "test" };

        // Act
        DiscoverAddOnsResult result = await useCase.ExecuteAsync(query);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal(maturity, result.Items[0].MaturityIndicator);
    }

    // -------------------------------------------------------------------------
    // 7.4 — Filter criteria forwarded to ISearchIndexPort
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DiscoverPlugins_WithFilters_ForwardsAllCriteriaToPort()
    {
        // Arrange
        ISearchIndexPort index = Substitute.For<ISearchIndexPort>();
        index.DiscoverAsync(Arg.Any<SearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<DiscoveryResultDto>)[], 0));

        DiscoverAddOnsUseCase useCase = new(index);
        DiscoverAddOnsQuery query = new()
        {
            Keyword = "testing",
            LanguageFilter = ["python"],
            UseCaseFilter = ["dev-team"],
            TypeFilter = ["skill"],
        };

        // Act
        await useCase.ExecuteAsync(query);

        // Assert — criteria forwarded
        await index.Received(1).DiscoverAsync(
            Arg.Is<SearchCriteria>(c =>
                c.Query == "testing" &&
                c.LanguageFilter != null && c.LanguageFilter.Contains("python") &&
                c.UseCaseFilter != null && c.UseCaseFilter.Contains("dev-team") &&
                c.TypeFilter != null && c.TypeFilter.Contains("skill")),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // 7.4 — Essential metadata fields present
    // Spec: "plugin name, description, version, types, languages, use cases,
    //        download count, last updated timestamp, author name"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DiscoverPlugins_Results_ContainEssentialMetadataFields()
    {
        // Arrange
        DateTimeOffset expectedLastUpdated = new(2025, 3, 1, 0, 0, 0, TimeSpan.Zero);
        DiscoveryResultDto dto = new()
        {
            Id = Guid.NewGuid(),
            Name = "EssentialMetaPlugin",
            Description = "Has all the metadata",
            LatestVersion = "3.1.0",
            Types = ["agent"],
            Languages = ["go"],
            UseCases = ["devops"],
            RelevanceScore = 0.85f,
            DownloadCount = 1234,
            LastUpdated = expectedLastUpdated,
            Author = "metadata-author",
            MaturityIndicator = "stable",
        };

        ISearchIndexPort index = Substitute.For<ISearchIndexPort>();
        index.DiscoverAsync(Arg.Any<SearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<DiscoveryResultDto>)[dto], 1));

        DiscoverAddOnsUseCase useCase = new(index);
        DiscoverAddOnsQuery query = new() { Keyword = "agent" };

        // Act
        DiscoverAddOnsResult result = await useCase.ExecuteAsync(query);

        // Assert all essential fields
        Assert.Single(result.Items);
        DiscoveryResultDto item = result.Items[0];

        Assert.Equal("EssentialMetaPlugin", item.Name);
        Assert.Equal("Has all the metadata", item.Description);
        Assert.Equal("3.1.0", item.LatestVersion);
        Assert.Contains("agent", item.Types);
        Assert.Contains("go", item.Languages);
        Assert.Contains("devops", item.UseCases);
        Assert.Equal(1234, item.DownloadCount);
        Assert.Equal(expectedLastUpdated, item.LastUpdated);
        Assert.Equal("metadata-author", item.Author);
    }
}
