using ClaudeForge.Application.Modules.PluginSearch.Ports;
using ClaudeForge.Application.Modules.PluginSearch.UseCases;
using ClaudeForge.Core.Shared.Model;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.PluginSearch;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace ClaudeForge.Tests.Unit.PluginSearch;

/// <summary>
/// Unit tests for Group 7 (task 7.6): Qdrant adapter seam.
///
/// Tests the adapter selection logic driven by config flag Features:QdrantEnabled.
///
/// Expected production types (coder MUST match these names exactly):
///
///   Namespace:   ClaudeForge.Infrastructure.PluginSearch
///     PostgresSearchAdapter : ISearchIndexPort
///       PostgresSearchAdapter(MarketplaceDbContext context)
///
///     QdrantSearchAdapter : ISearchIndexPort
///       QdrantSearchAdapter(ISearchIndexPort ftsFallback, ILogger&lt;QdrantSearchAdapter&gt; logger)
///       — Stub/seam only; throws NotImplementedException for full vector ops.
///       — When Qdrant is disabled or unreachable, delegates to ftsFallback and logs the event.
///
///     SearchAdapterSelector : ISearchIndexPort
///       SearchAdapterSelector(
///           ISearchIndexPort postgresAdapter,
///           ISearchIndexPort qdrantAdapter,
///           bool qdrantEnabled)
///       — Selects adapter based on flag; delegates to postgres when disabled.
///
///   Extension method (or module registration):
///     IServiceCollection AddPluginSearchAdapters(IServiceCollection services, IConfiguration configuration)
///     — When Features:QdrantEnabled = false  → registers PostgresSearchAdapter as ISearchIndexPort
///     — When Features:QdrantEnabled = true   → registers QdrantSearchAdapter wrapping PostgresSearchAdapter
///
///   Config path: "Features:QdrantEnabled"  (bool, default false)
/// </summary>
public sealed class QdrantSeamTests
{
    // -------------------------------------------------------------------------
    // 7.6 — Flag OFF → Postgres adapter used
    // -------------------------------------------------------------------------

    [Fact]
    public void AddPluginSearchAdapters_QdrantDisabled_ResolvesPostgresAdapter()
    {
        // Arrange
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Features:QdrantEnabled"] = "false",
            })
            .Build();

        ServiceCollection services = new();
        services.AddLogging();

        // Register an in-memory DbContext so GetRequiredService<MarketplaceDbContext> succeeds.
        services.AddDbContext<MarketplaceDbContext>(opts =>
            opts.UseInMemoryDatabase("qdrant-seam-test-disabled"));

        // Act — register adapters with Qdrant disabled
        services.AddPluginSearchAdapters(config);

        ServiceProvider provider = services.BuildServiceProvider();
        ISearchIndexPort adapter = provider.GetRequiredService<ISearchIndexPort>();

        // Assert — resolved adapter is the Postgres implementation (not Qdrant seam)
        Assert.IsNotType<QdrantSearchAdapter>(adapter);
    }

    [Fact]
    public void AddPluginSearchAdapters_QdrantEnabled_ResolvesQdrantAdapter()
    {
        // Arrange
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Features:QdrantEnabled"] = "true",
            })
            .Build();

        ServiceCollection services = new();
        services.AddLogging();

        // Register an in-memory DbContext so GetRequiredService<MarketplaceDbContext> succeeds.
        services.AddDbContext<MarketplaceDbContext>(opts =>
            opts.UseInMemoryDatabase("qdrant-seam-test-enabled"));

        // Act
        services.AddPluginSearchAdapters(config);

        ServiceProvider provider = services.BuildServiceProvider();
        ISearchIndexPort adapter = provider.GetRequiredService<ISearchIndexPort>();

        // Assert — the Qdrant seam adapter is resolved when flag is on
        Assert.IsType<QdrantSearchAdapter>(adapter);
    }

    // -------------------------------------------------------------------------
    // 7.6 — Fallback path: QdrantSearchAdapter delegates to FTS when disabled/down
    // Spec: "backend logs the fallback event for monitoring"
    //       "system falls back to full-text search without error"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task QdrantSearchAdapter_WhenQdrantUnavailable_FallsBackToFtsAndReturnsFtsResults()
    {
        // Arrange
        ISearchIndexPort ftsFallback = Substitute.For<ISearchIndexPort>();
        SearchResultDto ftsResult = new()
        {
            Id = Guid.NewGuid(),
            Name = "FtsResult",
            Slug = "fts-result",
            Description = "Returned by FTS fallback",
            RelevanceScore = 0.7f,
            DownloadCount = 10,
            LatestVersion = "1.0.0",
            CreatedAt = DateTimeOffset.UtcNow,
            Types = [],
            Languages = [],
            UseCases = [],
        };

        ftsFallback.SearchAsync(Arg.Any<SearchCriteria>(), Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<SearchResultDto>)[ftsResult], 1));

        ILogger<QdrantSearchAdapter> logger = Substitute.For<ILogger<QdrantSearchAdapter>>();

        // QdrantSearchAdapter stub: Qdrant is disabled → falls through to FTS
        QdrantSearchAdapter adapter = new(ftsFallback, logger);
        SearchCriteria criteria = new() { Query = "test" };
        PaginationRequest pagination = PaginationRequest.Default;

        // Act — should NOT throw; should return FTS results
        (IReadOnlyList<SearchResultDto> items, int total) =
            await adapter.SearchAsync(criteria, pagination);

        // Assert — FTS fallback was called and results returned transparently
        Assert.Single(items);
        Assert.Equal("FtsResult", items[0].Name);
        Assert.Equal(1, total);

        // Verify FTS delegate was invoked
        await ftsFallback.Received(1).SearchAsync(
            Arg.Any<SearchCriteria>(),
            Arg.Any<PaginationRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QdrantSearchAdapter_FallbackPath_LogsFallbackEvent()
    {
        // Arrange
        ISearchIndexPort ftsFallback = Substitute.For<ISearchIndexPort>();
        ftsFallback.SearchAsync(Arg.Any<SearchCriteria>(), Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<SearchResultDto>)[], 0));

        ILogger<QdrantSearchAdapter> logger = Substitute.For<ILogger<QdrantSearchAdapter>>();

        QdrantSearchAdapter adapter = new(ftsFallback, logger);
        SearchCriteria criteria = new() { Query = "test" };

        // Act
        await adapter.SearchAsync(criteria, PaginationRequest.Default);

        // Assert — logger was called (spec: "backend logs the fallback event")
        logger.Received().Log(
            Arg.Any<LogLevel>(),
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task QdrantSearchAdapter_DiscoverFallback_ReturnsFtsResults()
    {
        // Arrange
        ISearchIndexPort ftsFallback = Substitute.For<ISearchIndexPort>();
        DiscoveryResultDto discoveryResult = new()
        {
            Id = Guid.NewGuid(),
            Name = "FtsDiscovery",
            Description = "FTS discovery result",
            LatestVersion = "1.0.0",
            Types = ["skill"],
            Languages = ["typescript"],
            UseCases = [],
            RelevanceScore = 0.8f,
            DownloadCount = 50,
            LastUpdated = DateTimeOffset.UtcNow,
            Author = "fts-author",
            MaturityIndicator = "stable",
        };

        ftsFallback.DiscoverAsync(Arg.Any<SearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<DiscoveryResultDto>)[discoveryResult], 1));

        ILogger<QdrantSearchAdapter> logger = Substitute.For<ILogger<QdrantSearchAdapter>>();
        QdrantSearchAdapter adapter = new(ftsFallback, logger);
        SearchCriteria criteria = new() { Query = "skill" };

        // Act
        (IReadOnlyList<DiscoveryResultDto> items, _) = await adapter.DiscoverAsync(criteria);

        // Assert
        Assert.Single(items);
        Assert.Equal("FtsDiscovery", items[0].Name);
        await ftsFallback.Received(1).DiscoverAsync(Arg.Any<SearchCriteria>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // 7.6 — SearchAdapterSelector picks based on flag
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SearchAdapterSelector_QdrantDisabled_DelegatesToPostgresAdapter()
    {
        // Arrange
        ISearchIndexPort postgresAdapter = Substitute.For<ISearchIndexPort>();
        ISearchIndexPort qdrantAdapter = Substitute.For<ISearchIndexPort>();

        postgresAdapter.SearchAsync(Arg.Any<SearchCriteria>(), Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<SearchResultDto>)[], 0));

        // qdrantEnabled = false
        SearchAdapterSelector selector = new(postgresAdapter, qdrantAdapter, qdrantEnabled: false);
        SearchCriteria criteria = new() { Query = "test" };

        // Act
        await selector.SearchAsync(criteria, PaginationRequest.Default);

        // Assert — only Postgres was called
        await postgresAdapter.Received(1).SearchAsync(Arg.Any<SearchCriteria>(), Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>());
        await qdrantAdapter.DidNotReceive().SearchAsync(Arg.Any<SearchCriteria>(), Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchAdapterSelector_QdrantEnabled_DelegatesToQdrantAdapter()
    {
        // Arrange
        ISearchIndexPort postgresAdapter = Substitute.For<ISearchIndexPort>();
        ISearchIndexPort qdrantAdapter = Substitute.For<ISearchIndexPort>();

        qdrantAdapter.SearchAsync(Arg.Any<SearchCriteria>(), Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<SearchResultDto>)[], 0));

        // qdrantEnabled = true
        SearchAdapterSelector selector = new(postgresAdapter, qdrantAdapter, qdrantEnabled: true);
        SearchCriteria criteria = new() { Query = "test" };

        // Act
        await selector.SearchAsync(criteria, PaginationRequest.Default);

        // Assert — only Qdrant was called
        await qdrantAdapter.Received(1).SearchAsync(Arg.Any<SearchCriteria>(), Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>());
        await postgresAdapter.DidNotReceive().SearchAsync(Arg.Any<SearchCriteria>(), Arg.Any<PaginationRequest>(), Arg.Any<CancellationToken>());
    }
}
