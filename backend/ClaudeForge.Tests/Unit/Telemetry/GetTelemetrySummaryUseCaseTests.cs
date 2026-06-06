using ClaudeForge.Application.Modules.Telemetry.Ports;
using ClaudeForge.Application.Modules.Telemetry.UseCases;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;

namespace ClaudeForge.Tests.Unit.Telemetry;

/// <summary>
/// Unit tests for Group 8 (task 8.3): GetTelemetrySummaryUseCase.
///
/// Uses NSubstitute mocks for both ITelemetryStorePort and IMemoryCache.
///
/// Expected production types (coder MUST match these names exactly):
///
///   Namespace: ClaudeForge.Application.Modules.Telemetry.UseCases
///     GetTelemetrySummaryUseCase(ITelemetryStorePort store, IMemoryCache cache)
///       Task&lt;TelemetrySummaryDto&gt; ExecuteAsync(Guid pluginId, CancellationToken ct = default)
///
/// Spec scenarios:
///   "Metrics response cached for performance"
///     WHEN multiple concurrent requests ask for the same plugin's metrics
///     THEN the system caches aggregated results for 5 minutes (configurable)
///     AND subsequent requests within the cache window return instantly
///
///   "individual events are never exposed"
///   "aggregates in telemetry_aggregates table: only thing read endpoints touch"
///
/// TelemetrySummaryDto shape (design.md §7 + spec):
///   { PluginId, TotalDownloads, TotalInstalls, Last7Days: [{ Date, Downloads, Installs }] }
///   — MUST NOT contain anon_client_id, raw events, or any PII
/// </summary>
public sealed class GetTelemetrySummaryUseCaseTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static TelemetrySummaryDto MakeSummary(
        Guid pluginId,
        long totalDownloads = 10,
        long totalInstalls = 3) =>
        new()
        {
            PluginId = pluginId,
            TotalDownloads = totalDownloads,
            TotalInstalls = totalInstalls,
            Last7Days = [],
        };

    private static TelemetrySummaryDto MakeSummaryWithActivity(Guid pluginId) =>
        new()
        {
            PluginId = pluginId,
            TotalDownloads = 42,
            TotalInstalls = 7,
            Last7Days =
            [
                new DailyActivityDto { Date = DateOnly.FromDateTime(DateTime.UtcNow), Downloads = 5, Installs = 1 },
                new DailyActivityDto { Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), Downloads = 3, Installs = 2 },
            ],
        };

    // -------------------------------------------------------------------------
    // Returns aggregates-only DTO
    // Spec: "individual events are never exposed"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ExistingPlugin_ReturnsSummaryFromStore()
    {
        // Arrange
        Guid pluginId = Guid.NewGuid();
        ITelemetryStorePort store = Substitute.For<ITelemetryStorePort>();
        IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
        TelemetrySummaryDto expected = MakeSummary(pluginId, totalDownloads: 50, totalInstalls: 12);
        store.GetSummaryAsync(pluginId, Arg.Any<CancellationToken>()).Returns(expected);

        GetTelemetrySummaryUseCase useCase = new(store, cache);

        // Act
        TelemetrySummaryDto result = await useCase.ExecuteAsync(pluginId);

        // Assert
        Assert.Equal(pluginId, result.PluginId);
        Assert.Equal(50, result.TotalDownloads);
        Assert.Equal(12, result.TotalInstalls);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnedDto_ContainsLast7DaysActivity()
    {
        // Arrange
        Guid pluginId = Guid.NewGuid();
        ITelemetryStorePort store = Substitute.For<ITelemetryStorePort>();
        IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
        TelemetrySummaryDto expected = MakeSummaryWithActivity(pluginId);
        store.GetSummaryAsync(pluginId, Arg.Any<CancellationToken>()).Returns(expected);

        GetTelemetrySummaryUseCase useCase = new(store, cache);

        // Act
        TelemetrySummaryDto result = await useCase.ExecuteAsync(pluginId);

        // Assert — 7-day breakdown present
        Assert.NotNull(result.Last7Days);
        Assert.Equal(2, result.Last7Days.Count);
        Assert.Equal(5, result.Last7Days[0].Downloads);
        Assert.Equal(1, result.Last7Days[0].Installs);
    }

    // -------------------------------------------------------------------------
    // 8.3 — Cache: second call within 5 min served from cache (store queried once)
    // Spec: "caches aggregated results for 5 minutes (configurable)"
    //       "subsequent requests within the cache window return instantly"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_SecondCallSamePlugin_StoreQueriedOnlyOnce()
    {
        // Arrange
        Guid pluginId = Guid.NewGuid();
        ITelemetryStorePort store = Substitute.For<ITelemetryStorePort>();
        IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
        store.GetSummaryAsync(pluginId, Arg.Any<CancellationToken>()).Returns(MakeSummary(pluginId));

        GetTelemetrySummaryUseCase useCase = new(store, cache);

        // Act — two calls back-to-back (within the same test = within the 5-minute window)
        TelemetrySummaryDto first = await useCase.ExecuteAsync(pluginId);
        TelemetrySummaryDto second = await useCase.ExecuteAsync(pluginId);

        // Assert — store queried exactly once; second result served from cache
        await store.Received(1).GetSummaryAsync(pluginId, Arg.Any<CancellationToken>());
        Assert.Equal(first.TotalDownloads, second.TotalDownloads);
        Assert.Equal(first.TotalInstalls, second.TotalInstalls);
    }

    [Fact]
    public async Task ExecuteAsync_DifferentPluginIds_StoreQueriedForEachPlugin()
    {
        // Arrange
        Guid pluginId1 = Guid.NewGuid();
        Guid pluginId2 = Guid.NewGuid();
        ITelemetryStorePort store = Substitute.For<ITelemetryStorePort>();
        IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());

        store.GetSummaryAsync(pluginId1, Arg.Any<CancellationToken>()).Returns(MakeSummary(pluginId1, 10));
        store.GetSummaryAsync(pluginId2, Arg.Any<CancellationToken>()).Returns(MakeSummary(pluginId2, 20));

        GetTelemetrySummaryUseCase useCase = new(store, cache);

        // Act
        TelemetrySummaryDto result1 = await useCase.ExecuteAsync(pluginId1);
        TelemetrySummaryDto result2 = await useCase.ExecuteAsync(pluginId2);

        // Assert — store called once per distinct pluginId
        await store.Received(1).GetSummaryAsync(pluginId1, Arg.Any<CancellationToken>());
        await store.Received(1).GetSummaryAsync(pluginId2, Arg.Any<CancellationToken>());
        Assert.Equal(10, result1.TotalDownloads);
        Assert.Equal(20, result2.TotalDownloads);
    }

    // -------------------------------------------------------------------------
    // 8.3 — Never returns raw events
    // Spec: "Aggregation is server-side only; individual events never exposed to end users."
    //       "aggregates in telemetry_aggregates table: only thing read endpoints touch"
    // Assert via reflection: TelemetrySummaryDto has no raw-event or PII fields
    // -------------------------------------------------------------------------

    [Fact]
    public void TelemetrySummaryDto_HasNoRawEventOrPiiFields()
    {
        string[] forbiddenFields =
        [
            "AnonClientId", "ClientOs", "ClientArch", "RawEvents", "Events",
            "IpAddress", "Email", "UserName", "UserId", "OccurredAt",
            "EventType",   // individual events only; aggregate totals are fine
        ];

        System.Reflection.PropertyInfo[] properties =
            typeof(TelemetrySummaryDto).GetProperties(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        string[] propertyNames = properties.Select(p => p.Name).ToArray();

        foreach (string forbidden in forbiddenFields)
        {
            Assert.DoesNotContain(forbidden, propertyNames, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void TelemetrySummaryDto_HasRequiredAggregateFields()
    {
        // Summary must contain the fields shown in design.md §7 API surface
        System.Reflection.PropertyInfo[] properties =
            typeof(TelemetrySummaryDto).GetProperties(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        string[] propertyNames = properties.Select(p => p.Name).ToArray();

        Assert.Contains("PluginId", propertyNames);
        Assert.Contains("TotalDownloads", propertyNames);
        Assert.Contains("TotalInstalls", propertyNames);
        Assert.Contains("Last7Days", propertyNames);
    }

    [Fact]
    public void DailyActivityDto_HasRequiredFields()
    {
        System.Reflection.PropertyInfo[] properties =
            typeof(DailyActivityDto).GetProperties(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        string[] propertyNames = properties.Select(p => p.Name).ToArray();

        Assert.Contains("Date", propertyNames);
        Assert.Contains("Downloads", propertyNames);
        Assert.Contains("Installs", propertyNames);
    }

    // -------------------------------------------------------------------------
    // 8.3 — Cache passes CancellationToken correctly on cache miss
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_CacheMiss_PassesCancellationTokenToStore()
    {
        // Arrange
        Guid pluginId = Guid.NewGuid();
        ITelemetryStorePort store = Substitute.For<ITelemetryStorePort>();
        IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
        store.GetSummaryAsync(pluginId, Arg.Any<CancellationToken>()).Returns(MakeSummary(pluginId));

        GetTelemetrySummaryUseCase useCase = new(store, cache);
        using CancellationTokenSource cts = new();

        // Act
        await useCase.ExecuteAsync(pluginId, cts.Token);

        // Assert
        await store.Received(1).GetSummaryAsync(
            pluginId,
            Arg.Is<CancellationToken>(t => t == cts.Token));
    }
}
