using ClaudeForge.Application.Modules.Marketplace.Ports;
using ClaudeForge.Application.Modules.Marketplace.UseCases;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;

namespace ClaudeForge.Tests.Unit.Marketplace;

/// <summary>
/// Unit tests for GetMarketplaceStatsUseCase.
///
/// Mirrors the pattern in GetTelemetrySummaryUseCaseTests:
///   - IMemoryCache is a real MemoryCache instance (not a mock) so cache-hit semantics work.
///   - IMarketplaceStatsPort is an NSubstitute mock so call-count assertions work.
///
/// Expected production types (coder MUST match these names exactly):
///
///   Namespace: ClaudeForge.Application.Modules.Marketplace.Ports
///     IMarketplaceStatsPort
///       Task&lt;MarketplaceStatsDto&gt; GetStatsAsync(CancellationToken ct = default)
///     MarketplaceStatsDto (sealed record, all fields required)
///       long TotalPlugins
///       long TotalDownloads
///       long PublisherCount
///       long CategoryCount
///
///   Namespace: ClaudeForge.Application.Modules.Marketplace.UseCases
///     GetMarketplaceStatsUseCase(IMarketplaceStatsPort port, IMemoryCache cache)
///       Task&lt;MarketplaceStatsDto&gt; ExecuteAsync(CancellationToken ct = default)
///     cache key: "marketplace-stats" (constant, no dynamic segment)
///     TTL: 5-minute absolute expiration (matches spec "5-10 minutes"; pick 5)
/// </summary>
public sealed class GetMarketplaceStatsUseCaseTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static MarketplaceStatsDto MakeDto(
        long totalPlugins = 10,
        long totalDownloads = 500,
        long publisherCount = 4,
        long categoryCount = 6) =>
        new()
        {
            TotalPlugins = totalPlugins,
            TotalDownloads = totalDownloads,
            PublisherCount = publisherCount,
            CategoryCount = categoryCount,
        };

    // -------------------------------------------------------------------------
    // Happy-path: use-case returns all 4 DTO fields correctly
    // Spec: "WHEN the stats use-case is invoked THEN it returns a stats DTO
    //        with all four aggregate fields populated"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_CacheMiss_ReturnsDtoFromPort()
    {
        // Arrange
        IMarketplaceStatsPort port = Substitute.For<IMarketplaceStatsPort>();
        IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
        MarketplaceStatsDto expected = MakeDto(totalPlugins: 7, totalDownloads: 350,
            publisherCount: 3, categoryCount: 5);
        port.GetStatsAsync(Arg.Any<CancellationToken>()).Returns(expected);

        GetMarketplaceStatsUseCase useCase = new(port, cache);

        // Act
        MarketplaceStatsDto result = await useCase.ExecuteAsync();

        // Assert — all 4 fields propagated
        Assert.Equal(7L, result.TotalPlugins);
        Assert.Equal(350L, result.TotalDownloads);
        Assert.Equal(3L, result.PublisherCount);
        Assert.Equal(5L, result.CategoryCount);
    }

    // -------------------------------------------------------------------------
    // Cache hit: second call within TTL does NOT re-invoke the port
    // Spec: "WHEN the same request is made twice within the cache TTL
    //        THEN the second response is served from cache without querying the database"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_SecondCallWithinTtl_PortInvokedExactlyOnce()
    {
        // Arrange
        IMarketplaceStatsPort port = Substitute.For<IMarketplaceStatsPort>();
        IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
        port.GetStatsAsync(Arg.Any<CancellationToken>()).Returns(MakeDto());

        GetMarketplaceStatsUseCase useCase = new(port, cache);

        // Act — two consecutive calls (within the same process = well within the 5-minute window)
        MarketplaceStatsDto first = await useCase.ExecuteAsync();
        MarketplaceStatsDto second = await useCase.ExecuteAsync();

        // Assert — port queried exactly once; second result comes from cache
        await port.Received(1).GetStatsAsync(Arg.Any<CancellationToken>());
        Assert.Equal(first.TotalPlugins, second.TotalPlugins);
        Assert.Equal(first.TotalDownloads, second.TotalDownloads);
        Assert.Equal(first.PublisherCount, second.PublisherCount);
        Assert.Equal(first.CategoryCount, second.CategoryCount);
    }

    // -------------------------------------------------------------------------
    // CancellationToken is forwarded to the port on cache miss
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_CacheMiss_PassesCancellationTokenToPort()
    {
        // Arrange
        IMarketplaceStatsPort port = Substitute.For<IMarketplaceStatsPort>();
        IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
        port.GetStatsAsync(Arg.Any<CancellationToken>()).Returns(MakeDto());

        GetMarketplaceStatsUseCase useCase = new(port, cache);
        using CancellationTokenSource cts = new();

        // Act
        await useCase.ExecuteAsync(cts.Token);

        // Assert — the exact token was forwarded
        await port.Received(1).GetStatsAsync(
            Arg.Is<CancellationToken>(t => t == cts.Token));
    }

    // -------------------------------------------------------------------------
    // DTO shape: MarketplaceStatsDto has exactly the 4 required fields
    // and NONE of the forbidden fields (raw events, PII, per-plugin detail)
    // -------------------------------------------------------------------------

    [Fact]
    public void MarketplaceStatsDto_HasRequiredFields()
    {
        System.Reflection.PropertyInfo[] properties =
            typeof(MarketplaceStatsDto).GetProperties(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        string[] propertyNames = properties.Select(p => p.Name).ToArray();

        Assert.Contains("TotalPlugins", propertyNames);
        Assert.Contains("TotalDownloads", propertyNames);
        Assert.Contains("PublisherCount", propertyNames);
        Assert.Contains("CategoryCount", propertyNames);
    }

    [Fact]
    public void MarketplaceStatsDto_HasNoForbiddenFields()
    {
        // Must NOT expose per-plugin breakdown, raw events, or PII
        string[] forbidden =
        [
            "PluginId", "AnonClientId", "RawEvents", "Events",
            "IpAddress", "Email", "UserId", "OccurredAt", "Last7Days",
        ];

        System.Reflection.PropertyInfo[] properties =
            typeof(MarketplaceStatsDto).GetProperties(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        string[] propertyNames = properties.Select(p => p.Name).ToArray();

        foreach (string name in forbidden)
        {
            Assert.DoesNotContain(name, propertyNames, StringComparer.OrdinalIgnoreCase);
        }
    }

    // -------------------------------------------------------------------------
    // Isolated cache: each GetMarketplaceStatsUseCase instance has its own
    // IMemoryCache — a fresh instance starts with a cache miss.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_FreshCache_AlwaysInvokesPort()
    {
        // Arrange
        IMarketplaceStatsPort port = Substitute.For<IMarketplaceStatsPort>();
        port.GetStatsAsync(Arg.Any<CancellationToken>()).Returns(MakeDto());

        // Two distinct use-case instances with separate caches → each gets a cache miss
        IMemoryCache cache1 = new MemoryCache(new MemoryCacheOptions());
        IMemoryCache cache2 = new MemoryCache(new MemoryCacheOptions());

        GetMarketplaceStatsUseCase useCase1 = new(port, cache1);
        GetMarketplaceStatsUseCase useCase2 = new(port, cache2);

        // Act
        await useCase1.ExecuteAsync();
        await useCase2.ExecuteAsync();

        // Assert — port called once per distinct cache instance
        await port.Received(2).GetStatsAsync(Arg.Any<CancellationToken>());
    }
}
