using System.Net;
using System.Text.Json;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using ClaudeForge.Tests.Integration.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeForge.Tests.Integration.Marketplace;

/// <summary>
/// HTTP integration tests for GET /api/v1/stats.
///
/// Uses WebApplicationFactory&lt;Program&gt; + the shared Postgres container.
/// Verifies routing, serialisation, anonymous access, RFC 7807 error shape, and cached
/// response behaviour — mirroring the pattern in TelemetryHttpTests.
///
/// Expected production wiring (coder MUST match exactly):
///   Route:   GET /api/v1/stats
///   Module:  registered in TelemetryModule (or a new StatsModule) via the IModule mechanism
///   Auth:    anonymous (no [Authorize] / RequireAuthorization); AllowAnonymous implied
///   Response 200: application/json
///     { "totalPlugins": long, "totalDownloads": long, "publisherCount": long, "categoryCount": long }
///   Response 500: RFC 7807 ProblemDetails via GlobalExceptionHandler
///     { "type": string, "title": string, "detail": string, "instance": string }
///   OpenAPI: endpoint tagged, documented with 200 + 500 response schemas and example values
/// </summary>
[Collection(PostgresFixture.CollectionName)]
public sealed class MarketplaceStatsHttpTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public MarketplaceStatsHttpTests(PostgresFixture fixture)
    {
        _fixture = fixture;

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace DbContext with the test container connection
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

    private static PluginEntity MakePublicPlugin(string author, long downloadCount = 0) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = $"StatsHttp-{Guid.NewGuid().ToString("N")[..8]}",
            NameNormalized = $"statshttp-{Guid.NewGuid().ToString("N")[..8]}",
            Slug = $"stats-http-{Guid.NewGuid().ToString("N")[..8]}",
            Description = "Plugin for stats HTTP tests",
            Author = author,
            DownloadCount = downloadCount,
            Visibility = "public",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    private static PluginEntity MakePrivatePlugin(Guid orgId, string author, long downloadCount = 0) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = $"StatsHttpPriv-{Guid.NewGuid().ToString("N")[..8]}",
            NameNormalized = $"statshttppriv-{Guid.NewGuid().ToString("N")[..8]}",
            Slug = $"stats-http-priv-{Guid.NewGuid().ToString("N")[..8]}",
            Description = "Private plugin for stats HTTP tests",
            Author = author,
            DownloadCount = downloadCount,
            Visibility = "private",
            OwnerOrgId = orgId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    private static CategoryEntity MakeCategory(string dimension, string value) =>
        new()
        {
            Dimension = dimension,
            Value = value,
            DisplayName = $"{dimension}:{value}",
        };

    /// <summary>Seeds a User + Organization row to satisfy the FK on private plugins.</summary>
    private static async Task<Guid> SeedOrgAsync(MarketplaceDbContext ctx)
    {
        Guid userId = Guid.NewGuid();
        ctx.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"http-stats-{userId:N}@test.local",
            DisplayName = "HTTP Stats Test User",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        Guid orgId = Guid.NewGuid();
        ctx.Organizations.Add(new OrganizationEntity
        {
            Id = orgId,
            Name = $"HttpStatsOrg-{orgId:N}",
            NameNormalized = $"httpstatsorg-{orgId:N}",
            Slug = $"http-stats-org-{orgId:N}",
            CreatedBy = userId,
            CreatedAt = DateTimeOffset.UtcNow,
        });

        await ctx.SaveChangesAsync();
        return orgId;
    }

    // -------------------------------------------------------------------------
    // Happy-path: GET /api/v1/stats → 200 with all 4 fields
    // Spec: "WHEN a client requests GET /api/v1/stats
    //        THEN the system returns HTTP 200 with JSON containing totalPlugins,
    //        totalDownloads, publisherCount, and categoryCount"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetStats_EmptyDatabase_Returns200WithAllZeros()
    {
        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/stats");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        Assert.True(root.TryGetProperty("totalPlugins", out JsonElement totalPlugins),
            "Response must contain 'totalPlugins'");
        Assert.True(root.TryGetProperty("totalDownloads", out JsonElement totalDownloads),
            "Response must contain 'totalDownloads'");
        Assert.True(root.TryGetProperty("publisherCount", out JsonElement publisherCount),
            "Response must contain 'publisherCount'");
        Assert.True(root.TryGetProperty("categoryCount", out JsonElement categoryCount),
            "Response must contain 'categoryCount'");

        Assert.Equal(0L, totalPlugins.GetInt64());
        Assert.Equal(0L, totalDownloads.GetInt64());
        Assert.Equal(0L, publisherCount.GetInt64());
        Assert.Equal(0L, categoryCount.GetInt64());
    }

    [Fact]
    public async Task GetStats_WithSeededData_Returns200WithCorrectValues()
    {
        // Arrange — seed: 2 public plugins (alice=100, bob=200), 1 private (carol=9999), 3 categories
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        Guid orgId = await SeedOrgAsync(ctx);

        await using MarketplaceDbContext seedCtx = _fixture.CreateContext();
        seedCtx.Plugins.Add(MakePublicPlugin("alice", 100));
        seedCtx.Plugins.Add(MakePublicPlugin("bob", 200));
        seedCtx.Plugins.Add(MakePrivatePlugin(orgId, "carol", 9999));
        seedCtx.Categories.Add(MakeCategory("type", "skill"));
        seedCtx.Categories.Add(MakeCategory("type", "agent"));
        seedCtx.Categories.Add(MakeCategory("language", "typescript"));
        await seedCtx.SaveChangesAsync();

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/stats");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        Assert.Equal(2L, root.GetProperty("totalPlugins").GetInt64());
        Assert.Equal(300L, root.GetProperty("totalDownloads").GetInt64());
        Assert.Equal(2L, root.GetProperty("publisherCount").GetInt64());
        Assert.Equal(3L, root.GetProperty("categoryCount").GetInt64());
    }

    // -------------------------------------------------------------------------
    // Anonymous access: no Authorization header required
    // Spec: "publicly accessible REST endpoint"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetStats_NoAuthorizationHeader_Returns200NotUnauthorized()
    {
        // Act — client has no Authorization header (default factory client = anonymous)
        HttpResponseMessage response = await _client.GetAsync("/api/v1/stats");

        // Assert — must not be 401 Unauthorized or 403 Forbidden
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // Response content-type is application/json
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetStats_Returns200_ContentTypeIsApplicationJson()
    {
        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/stats");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.Contains("application/json",
            response.Content.Headers.ContentType.MediaType,
            StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------
    // Response shape: response must NOT expose raw plugin detail, per-plugin data, or PII
    // (stats are aggregate-only)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetStats_ResponseBody_DoesNotContainPerPluginFields()
    {
        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/stats");

        string body = await response.Content.ReadAsStringAsync();

        // Assert — aggregate endpoint must not leak individual plugin data
        Assert.DoesNotContain("\"pluginId\"", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"plugins\":[", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"anonClientId\"", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"last7Days\"", body, StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------
    // Cache: second call within TTL is served from cache (total values unchanged
    // even after seeding more data post-first-call)
    // Spec: "WHEN the same request is made twice within the cache TTL
    //        THEN the second response is served from cache without querying the database"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetStats_CalledTwiceWithinTtl_SecondCallReturnsCachedResult()
    {
        // Arrange — seed 1 public plugin
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        ctx.Plugins.Add(MakePublicPlugin("alice", 50));
        await ctx.SaveChangesAsync();

        // Act — first call establishes cache (totalPlugins = 1)
        HttpResponseMessage first = await _client.GetAsync("/api/v1/stats");
        string firstBody = await first.Content.ReadAsStringAsync();
        using JsonDocument firstDoc = JsonDocument.Parse(firstBody);
        long firstTotal = firstDoc.RootElement.GetProperty("totalPlugins").GetInt64();

        // Seed one more plugin directly (bypasses use-case)
        await using MarketplaceDbContext seedCtx2 = _fixture.CreateContext();
        seedCtx2.Plugins.Add(MakePublicPlugin("bob", 100));
        await seedCtx2.SaveChangesAsync();

        // Act — second call, still within the 5-minute window
        HttpResponseMessage second = await _client.GetAsync("/api/v1/stats");
        string secondBody = await second.Content.ReadAsStringAsync();
        using JsonDocument secondDoc = JsonDocument.Parse(secondBody);
        long secondTotal = secondDoc.RootElement.GetProperty("totalPlugins").GetInt64();

        // Assert — cached: second total equals first (new plugin not reflected yet)
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(firstTotal, secondTotal);
        Assert.Equal(1L, firstTotal); // first call saw exactly 1 plugin
    }

    // -------------------------------------------------------------------------
    // Error path: RFC 7807 ProblemDetails on 500 (verified via OpenAPI contract;
    // we can only smoke-test the GlobalExceptionHandler here since we cannot
    // make the real DB fail mid-test without DI override).
    //
    // This test verifies that a 500 produces the right ProblemDetails shape
    // by registering a broken IMarketplaceStatsPort stub.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetStats_PortThrowsException_Returns500WithRfc7807ProblemDetails()
    {
        // Arrange — override the port with a stub that always throws
        await using WebApplicationFactory<Program> brokenFactory =
            new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        // Replace DbContext with test Postgres so the app can start
                        ServiceDescriptor? optDescriptor = services.SingleOrDefault(
                            d => d.ServiceType == typeof(DbContextOptions<MarketplaceDbContext>));
                        if (optDescriptor is not null)
                            services.Remove(optDescriptor);

                        ServiceDescriptor? ctxDescriptor = services.SingleOrDefault(
                            d => d.ServiceType == typeof(MarketplaceDbContext));
                        if (ctxDescriptor is not null)
                            services.Remove(ctxDescriptor);

                        services.AddDbContext<MarketplaceDbContext>(options =>
                            options.UseNpgsql(_fixture.ConnectionString));

                        // Override the stats port with a broken stub that simulates DB failure
                        services.AddScoped<ClaudeForge.Application.Modules.Marketplace.Ports.IMarketplaceStatsPort>(
                            _ => new BrokenMarketplaceStatsPort());
                    });
                });

        using HttpClient brokenClient = brokenFactory.CreateClient();

        // Act
        HttpResponseMessage response = await brokenClient.GetAsync("/api/v1/stats");

        // Assert — 500 with RFC 7807 fields
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        // RFC 7807 mandates: type, title, detail, instance (status optional)
        Assert.True(root.TryGetProperty("type", out _), "RFC 7807 requires 'type' field");
        Assert.True(root.TryGetProperty("title", out _), "RFC 7807 requires 'title' field");
        Assert.True(root.TryGetProperty("detail", out _), "RFC 7807 requires 'detail' field");
        Assert.True(root.TryGetProperty("instance", out _), "RFC 7807 requires 'instance' field");
    }

    // -------------------------------------------------------------------------
    // Stub: simulates a broken IMarketplaceStatsPort (DB unavailable)
    // -------------------------------------------------------------------------

    private sealed class BrokenMarketplaceStatsPort
        : ClaudeForge.Application.Modules.Marketplace.Ports.IMarketplaceStatsPort
    {
        public Task<ClaudeForge.Application.Modules.Marketplace.Ports.MarketplaceStatsDto> GetStatsAsync(
            CancellationToken ct = default) =>
            throw new InvalidOperationException("Simulated database failure for test");
    }
}
