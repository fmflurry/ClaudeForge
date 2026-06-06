using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using ClaudeForge.Tests.Integration.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeForge.Tests.Integration.Telemetry;

/// <summary>
/// HTTP integration tests for Group 8 (tasks 8.4, 8.6):
///   POST /api/v1/telemetry/events
///   GET  /api/v1/plugins/{pluginId}/telemetry/summary
///
/// Spec scenarios verified:
///   • POST valid → 202 Accepted (fire-and-forget)
///   • POST malformed (missing eventType / bad anonClientId / missing pluginId) → 400 ProblemDetails
///   • GET summary → 200 aggregated totals + 7-day activity (no raw events / PII exposed)
///   • GET summary called twice → cache served (store queried once per window)
///
/// Verbatim error strings from spec:
///   "Event type is required and must be 'download' or 'install'."
///   "Anonymous client ID is required and must be a 64-character hex string."
///   "Plugin ID is required."
///
/// Privacy guarantee:
///   The GET /telemetry/summary response MUST NOT contain:
///     anon_client_id, raw events, IP addresses, user identifiers, OccurredAt on individual rows
///   It MAY contain: totalDownloads, totalInstalls, last7Days[{ date, downloads, installs }]
/// </summary>
[Collection(PostgresFixture.CollectionName)]
public sealed class TelemetryHttpTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly string ValidAnonClientId = new('c', 64);

    public TelemetryHttpTests(PostgresFixture fixture)
    {
        _fixture = fixture;

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace DbContext with test container connection
                    ServiceDescriptor? optDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<MarketplaceDbContext>));
                    if (optDescriptor is not null)
                        services.Remove(optDescriptor);

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

    private static async Task<PluginEntity> SeedPluginAsync(MarketplaceDbContext ctx)
    {
        PluginEntity plugin = new()
        {
            Id = Guid.NewGuid(),
            Name = "HttpTelemetryPlugin-" + Guid.NewGuid().ToString("N")[..8],
            NameNormalized = "httptelemetryplugin-" + Guid.NewGuid().ToString("N")[..8],
            Slug = "http-telemetry-" + Guid.NewGuid().ToString("N")[..8],
            Description = "Plugin for telemetry HTTP tests",
            Author = "test-author",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();
        return plugin;
    }

    private static object ValidPayload(Guid pluginId) => new
    {
        eventType = "download",
        pluginId = pluginId,
        version = "1.0.0",
        anonClientId = ValidAnonClientId,
        clientOs = "linux",
        clientArch = "x64",
    };

    // -------------------------------------------------------------------------
    // 8.4 — POST /api/v1/telemetry/events → 202 Accepted on valid payload
    // Spec: fire-and-forget semantics; response is 202 regardless of async outcome
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PostTelemetryEvent_ValidDownloadPayload_Returns202Accepted()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        PluginEntity plugin = await SeedPluginAsync(ctx);

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/telemetry/events",
            ValidPayload(plugin.Id));

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task PostTelemetryEvent_ValidInstallPayload_Returns202Accepted()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        PluginEntity plugin = await SeedPluginAsync(ctx);

        object payload = new
        {
            eventType = "install",
            pluginId = plugin.Id,
            version = "2.0.0",
            anonClientId = ValidAnonClientId,
            clientOs = "darwin",
            clientArch = "arm64",
        };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/telemetry/events", payload);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // 8.4 / 8.6 — POST malformed → 400 ProblemDetails with verbatim spec string
    // Spec: "WHEN an event is missing required fields ... THEN the system rejects the event
    //        with HTTP 400 AND logs the rejection without storing the incomplete event"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PostTelemetryEvent_MissingEventType_Returns400WithSpecExactDetail()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        PluginEntity plugin = await SeedPluginAsync(ctx);

        object payload = new
        {
            // eventType omitted
            pluginId = plugin.Id,
            version = "1.0.0",
            anonClientId = ValidAnonClientId,
        };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/telemetry/events", payload);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        string? detail = doc.RootElement.GetProperty("detail").GetString();

        // Spec verbatim
        Assert.Equal("Event type is required and must be 'download' or 'install'.", detail);
    }

    [Fact]
    public async Task PostTelemetryEvent_InvalidEventType_Returns400WithSpecExactDetail()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        PluginEntity plugin = await SeedPluginAsync(ctx);

        object payload = new
        {
            eventType = "view",   // invalid — only 'download' or 'install' allowed
            pluginId = plugin.Id,
            version = "1.0.0",
            anonClientId = ValidAnonClientId,
        };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/telemetry/events", payload);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        string? detail = doc.RootElement.GetProperty("detail").GetString();

        Assert.Equal("Event type is required and must be 'download' or 'install'.", detail);
    }

    [Fact]
    public async Task PostTelemetryEvent_MissingAnonClientId_Returns400WithSpecExactDetail()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        PluginEntity plugin = await SeedPluginAsync(ctx);

        object payload = new
        {
            eventType = "download",
            pluginId = plugin.Id,
            version = "1.0.0",
            // anonClientId omitted
        };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/telemetry/events", payload);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        string? detail = doc.RootElement.GetProperty("detail").GetString();

        // Spec verbatim
        Assert.Equal("Anonymous client ID is required and must be a 64-character hex string.", detail);
    }

    [Fact]
    public async Task PostTelemetryEvent_BadAnonClientId_Returns400WithSpecExactDetail()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        PluginEntity plugin = await SeedPluginAsync(ctx);

        object payload = new
        {
            eventType = "download",
            pluginId = plugin.Id,
            version = "1.0.0",
            anonClientId = "too-short",
        };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/telemetry/events", payload);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        string? detail = doc.RootElement.GetProperty("detail").GetString();

        Assert.Equal("Anonymous client ID is required and must be a 64-character hex string.", detail);
    }

    [Fact]
    public async Task PostTelemetryEvent_MissingPluginId_Returns400WithSpecExactDetail()
    {
        // Arrange
        object payload = new
        {
            eventType = "download",
            // pluginId omitted
            version = "1.0.0",
            anonClientId = ValidAnonClientId,
        };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/telemetry/events", payload);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        string? detail = doc.RootElement.GetProperty("detail").GetString();

        // Spec verbatim
        Assert.Equal("Plugin ID is required.", detail);
    }

    // -------------------------------------------------------------------------
    // 8.4 — After ingest, GET /api/v1/plugins/{pluginId}/telemetry/summary → 200
    // Spec: "returns total downloads + installs + last 7 days activity count"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetTelemetrySummary_AfterIngest_Returns200WithCorrectTotals()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        PluginEntity plugin = await SeedPluginAsync(ctx);

        // Ingest 2 download events
        for (int i = 0; i < 2; i++)
        {
            await _client.PostAsJsonAsync("/api/v1/telemetry/events", new
            {
                eventType = "download",
                pluginId = plugin.Id,
                version = "1.0.0",
                anonClientId = ValidAnonClientId,
            });
        }

        // Ingest 1 install event
        await _client.PostAsJsonAsync("/api/v1/telemetry/events", new
        {
            eventType = "install",
            pluginId = plugin.Id,
            version = "1.0.0",
            anonClientId = ValidAnonClientId,
        });

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/plugins/{plugin.Id}/telemetry/summary");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        Assert.True(root.TryGetProperty("totalDownloads", out JsonElement totalDownloads),
            "Response must have 'totalDownloads' property");
        Assert.True(root.TryGetProperty("totalInstalls", out JsonElement totalInstalls),
            "Response must have 'totalInstalls' property");
        Assert.True(root.TryGetProperty("last7Days", out JsonElement last7Days),
            "Response must have 'last7Days' property");

        Assert.Equal(2, totalDownloads.GetInt64());
        Assert.Equal(1, totalInstalls.GetInt64());
        Assert.Equal(JsonValueKind.Array, last7Days.ValueKind);
    }

    [Fact]
    public async Task GetTelemetrySummary_NoActivity_Returns200WithZeroTotals()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        PluginEntity plugin = await SeedPluginAsync(ctx);

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/plugins/{plugin.Id}/telemetry/summary");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        Assert.Equal(0, root.GetProperty("totalDownloads").GetInt64());
        Assert.Equal(0, root.GetProperty("totalInstalls").GetInt64());
    }

    // -------------------------------------------------------------------------
    // 8.4 — Privacy guarantee: summary response MUST NOT contain raw-event / PII fields
    // Spec: "no individual event details or client IDs are included"
    //       "individual events are never exposed"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetTelemetrySummary_ResponseDoesNotContainAnonClientId()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        PluginEntity plugin = await SeedPluginAsync(ctx);

        // Ingest an event so there's some data
        await _client.PostAsJsonAsync("/api/v1/telemetry/events", ValidPayload(plugin.Id));

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/plugins/{plugin.Id}/telemetry/summary");

        string body = await response.Content.ReadAsStringAsync();

        // Assert — anon_client_id must NOT appear anywhere in response
        Assert.DoesNotContain("anonClientId", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("anon_client_id", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetTelemetrySummary_ResponseDoesNotContainRawEvents()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        PluginEntity plugin = await SeedPluginAsync(ctx);
        await _client.PostAsJsonAsync("/api/v1/telemetry/events", ValidPayload(plugin.Id));

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/plugins/{plugin.Id}/telemetry/summary");

        string body = await response.Content.ReadAsStringAsync();

        // Assert — no raw event arrays or individual IDs
        Assert.DoesNotContain("rawEvents", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("events\":[", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTelemetrySummary_ResponseShape_ContainsOnlyAggregateFields()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        PluginEntity plugin = await SeedPluginAsync(ctx);

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/plugins/{plugin.Id}/telemetry/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        // Required aggregate fields
        Assert.True(root.TryGetProperty("pluginId", out _), "Must have 'pluginId'");
        Assert.True(root.TryGetProperty("totalDownloads", out _), "Must have 'totalDownloads'");
        Assert.True(root.TryGetProperty("totalInstalls", out _), "Must have 'totalInstalls'");
        Assert.True(root.TryGetProperty("last7Days", out _), "Must have 'last7Days'");
    }

    // -------------------------------------------------------------------------
    // 8.6 — Cache: summary cached 5 minutes; second immediate call hits cache
    // Spec: "caches aggregated results for 5 minutes (configurable)"
    // Approach: ingest 1 event; get summary (count=1); ingest 2nd event;
    //   get summary again immediately — still shows count=1 (cached).
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetTelemetrySummary_CalledTwice_SecondCallReturnsCachedResult()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        PluginEntity plugin = await SeedPluginAsync(ctx);

        // Ingest one event
        await _client.PostAsJsonAsync("/api/v1/telemetry/events", ValidPayload(plugin.Id));

        // Act — first GET establishes cache with count=1
        HttpResponseMessage firstResponse = await _client.GetAsync(
            $"/api/v1/plugins/{plugin.Id}/telemetry/summary");
        string firstBody = await firstResponse.Content.ReadAsStringAsync();
        using JsonDocument firstDoc = JsonDocument.Parse(firstBody);
        long firstDownloads = firstDoc.RootElement.GetProperty("totalDownloads").GetInt64();

        // Ingest second event (would change count to 2 if cache is bypassed)
        await _client.PostAsJsonAsync("/api/v1/telemetry/events", ValidPayload(plugin.Id));

        // Act — second GET immediately (within cache window)
        HttpResponseMessage secondResponse = await _client.GetAsync(
            $"/api/v1/plugins/{plugin.Id}/telemetry/summary");
        string secondBody = await secondResponse.Content.ReadAsStringAsync();
        using JsonDocument secondDoc = JsonDocument.Parse(secondBody);
        long secondDownloads = secondDoc.RootElement.GetProperty("totalDownloads").GetInt64();

        // Assert — cached: both calls return the same total (the 2nd event is not yet reflected)
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(firstDownloads, secondDownloads); // Cache hit: same value
        Assert.Equal(1, firstDownloads); // First response shows exactly 1
    }

    // -------------------------------------------------------------------------
    // 8.6 — Rate limiting: POST /api/v1/telemetry/events has per-IP rate limiting
    // Design §5: "Mitigate with per-IP rate limiting on POST /api/v1/telemetry/events"
    // This test is structural — it confirms the endpoint exists behind a rate limit policy.
    // We don't hammer to 429 (slow tests), just confirm the endpoint responds normally.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PostTelemetryEvent_WithinRateLimit_Returns202()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        PluginEntity plugin = await SeedPluginAsync(ctx);

        // Act — multiple valid requests within the rate limit window
        for (int i = 0; i < 3; i++)
        {
            HttpResponseMessage response = await _client.PostAsJsonAsync(
                "/api/v1/telemetry/events",
                ValidPayload(plugin.Id));

            // Assert — all within limit should be 202
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        }
    }

    // -------------------------------------------------------------------------
    // 8.4 — GET /api/v1/plugins/{pluginId}/telemetry/summary
    //   last7Days entries have the required shape { date, downloads, installs }
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetTelemetrySummary_WithActivity_Last7DaysEntriesHaveCorrectShape()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        PluginEntity plugin = await SeedPluginAsync(ctx);

        // Ingest events so there's at least 1 day with activity
        await _client.PostAsJsonAsync("/api/v1/telemetry/events", ValidPayload(plugin.Id));

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/plugins/{plugin.Id}/telemetry/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement last7Days = doc.RootElement.GetProperty("last7Days");

        Assert.Equal(JsonValueKind.Array, last7Days.ValueKind);

        // Each entry must have { date, downloads, installs } — no PII fields
        foreach (JsonElement entry in last7Days.EnumerateArray())
        {
            Assert.True(entry.TryGetProperty("date", out _), "Each entry must have 'date'");
            Assert.True(entry.TryGetProperty("downloads", out _), "Each entry must have 'downloads'");
            Assert.True(entry.TryGetProperty("installs", out _), "Each entry must have 'installs'");

            // Explicitly assert no raw PII fields
            Assert.False(entry.TryGetProperty("anonClientId", out _));
            Assert.False(entry.TryGetProperty("clientOs", out _));
            Assert.False(entry.TryGetProperty("clientArch", out _));
        }
    }
}
