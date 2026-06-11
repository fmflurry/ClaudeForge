using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using ClaudeForge.Core.Domain.Plugins;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using ClaudeForge.Infrastructure.Storage;
using ClaudeForge.Tests.Integration.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeForge.Tests.Integration.AddOnDistribution;

/// <summary>
/// HTTP integration tests for Group 6 — Plugin Distribution API endpoint.
///
/// Uses WebApplicationFactory&lt;Program&gt; with a real PostgreSQL 16 container.
/// Tests the full HTTP stack: routing, streaming, exception middleware, header values.
///
/// Endpoint under test:
///   GET /api/v1/plugins/{pluginId:guid}/download?version=
///
/// Verbatim spec error strings (plugin-download/spec.md):
///   "Plugin not found"
///   "Plugin version 9.9.9 not found"      (pattern: "Plugin version {version} not found")
///   "Invalid version format. Expected semver (e.g., 1.0.0)"
///
/// Spec: response headers on 200 —
///   Content-Type: application/gzip  (or application/zip for zip packages)
///   Content-Disposition: attachment; filename="{name}-{version}.tar.gz"
///   Content-Length: &lt;file-size-in-bytes&gt;
///   ETag: &lt;sha256&gt;
/// </summary>
[Collection(PostgresFixture.CollectionName)]
public sealed class PluginDistributionHttpTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _storageRoot;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public PluginDistributionHttpTests(PostgresFixture fixture)
    {
        _fixture = fixture;

        // Isolated temp storage directory for each test class instance
        _storageRoot = Path.Combine(Path.GetTempPath(),
            "claudeforge-dist-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_storageRoot);

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace DbContext
                    ServiceDescriptor? optionsDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<MarketplaceDbContext>));
                    if (optionsDescriptor is not null)
                        services.Remove(optionsDescriptor);

                    ServiceDescriptor? ctxDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(MarketplaceDbContext));
                    if (ctxDescriptor is not null)
                        services.Remove(ctxDescriptor);

                    services.AddDbContext<MarketplaceDbContext>(options =>
                        options.UseNpgsql(fixture.ConnectionString));

                    // Replace package storage with an isolated local adapter for this test run
                    ServiceDescriptor? storageDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(ClaudeForge.Core.Ports.IPackageStoragePort));
                    if (storageDescriptor is not null)
                        services.Remove(storageDescriptor);

                    services.AddSingleton<ClaudeForge.Core.Ports.IPackageStoragePort>(
                        _ => new LocalFileSystemPackageStorageAdapter(_storageRoot));
                });
            });

        _client = _factory.CreateClient();
    }

    // -------------------------------------------------------------------------
    // Per-test isolation: truncate all marketplace tables + clean storage.
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

        // Clean any previously stored test packages
        if (Directory.Exists(_storageRoot))
        {
            foreach (string dir in Directory.GetDirectories(_storageRoot))
                Directory.Delete(dir, recursive: true);
        }
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();

        if (Directory.Exists(_storageRoot))
        {
            try { Directory.Delete(_storageRoot, recursive: true); }
            catch { /* best effort cleanup */ }
        }
    }

    // =========================================================================
    // Archive builders (BCL only)
    // =========================================================================

    private static byte[] BuildTarGzBytes(
        IEnumerable<(string name, string content)> entries)
    {
        using MemoryStream output = new();

        using (GZipStream gzip = new(output, CompressionMode.Compress, leaveOpen: true))
        using (TarWriter tar = new(gzip, TarEntryFormat.Pax, leaveOpen: false))
        {
            foreach ((string name, string content) in entries)
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(content);
                PaxTarEntry entry = new(TarEntryType.RegularFile, name)
                {
                    DataStream = new MemoryStream(bytes),
                };
                tar.WriteEntry(entry);
            }
        }

        return output.ToArray();
    }

    private static byte[] BuildValidPackageBytes(
        string name = "test-plugin",
        string version = "1.0.0") =>
        BuildTarGzBytes([
            ("plugin.json",
                $$"""{"name":"{{name}}","version":"{{version}}","description":"Test","author":"Author","types":["skill"],"languages":["typescript"]}"""),
            ("README.md", $"# {name}"),
        ]);

    // =========================================================================
    // Seed helpers — directly insert DB rows + write package bytes to storage
    // =========================================================================

    private async Task<(Guid pluginId, string version)> SeedPluginWithStoredPackageAsync(
        string pluginName = "test-plugin",
        string version = "1.0.0",
        byte[]? packageBytes = null)
    {
        packageBytes ??= BuildValidPackageBytes(pluginName, version);

        Guid pluginId = Guid.NewGuid();
        string packageKey = $"plugins/{pluginId}/{version}/package.tar.gz";

        // Write package to local storage
        string packagePath = Path.Combine(
            _storageRoot,
            packageKey.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(packagePath)!);
        await File.WriteAllBytesAsync(packagePath, packageBytes);

        // Compute sha256
        byte[] hash = System.Security.Cryptography.SHA256.HashData(packageBytes);
        string sha256 = Convert.ToHexStringLower(hash);

        // Seed DB
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        AddOnEntity plugin = new()
        {
            Id = pluginId,
            Name = pluginName,
            NameNormalized = pluginName.ToLowerInvariant(),
            Slug = pluginName.ToLowerInvariant().Replace(" ", "-"),
            Description = "Integration test plugin",
            Author = "Test Author",
            DownloadCount = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        AddOnVersionEntity versionEntity = new()
        {
            Id = Guid.NewGuid(),
            PluginId = pluginId,
            Version = version,
            VersionSort = SemVer.Parse(version).ToVersionSort(),
            ReleaseNotes = string.Empty,
            IsLatest = true,
            PackageKey = packageKey,
            PackageFormat = "tar.gz",
            SizeBytes = packageBytes.LongLength,
            Sha256 = sha256,
            DownloadCount = 0,
            ReleasedAt = DateTimeOffset.UtcNow,
        };
        ctx.PluginVersions.Add(versionEntity);
        await ctx.SaveChangesAsync();

        return (pluginId, version);
    }

    // =========================================================================
    // GET /api/v1/plugins/{pluginId}/download — no version → 200 latest
    // =========================================================================

    [Fact]
    public async Task GetDownload_NoVersionParam_Returns200WithCorrectContentType()
    {
        // Arrange
        (Guid pluginId, _) = await SeedPluginWithStoredPackageAsync(
            "my-plugin", "1.0.0");

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/plugins/{pluginId}/download");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/gzip", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetDownload_NoVersionParam_Returns200WithContentDispositionAttachment()
    {
        // Arrange
        (Guid pluginId, _) = await SeedPluginWithStoredPackageAsync(
            "my-plugin", "2.0.0");

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/plugins/{pluginId}/download");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Spec: Content-Disposition: attachment; filename="{name}-{version}.tar.gz"
        string? disposition = response.Content.Headers.ContentDisposition?.ToString();
        Assert.NotNull(disposition);
        Assert.Contains("attachment", disposition, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("my-plugin-2.0.0.tar.gz", disposition, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetDownload_NoVersionParam_Returns200WithContentLength()
    {
        // Arrange
        byte[] packageBytes = BuildValidPackageBytes("sized-plugin", "1.0.0");
        (Guid pluginId, _) = await SeedPluginWithStoredPackageAsync(
            "sized-plugin", "1.0.0", packageBytes);

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/plugins/{pluginId}/download");

        // Assert — spec: Content-Length must equal the file size
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(packageBytes.LongLength, response.Content.Headers.ContentLength);
    }

    [Fact]
    public async Task GetDownload_NoVersionParam_ResponseBodyMatchesStoredPackageBytes()
    {
        // Arrange
        byte[] originalBytes = BuildValidPackageBytes("body-plugin", "1.0.0");
        (Guid pluginId, _) = await SeedPluginWithStoredPackageAsync(
            "body-plugin", "1.0.0", originalBytes);

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/plugins/{pluginId}/download");

        // Assert — body bytes must exactly match the stored package
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        byte[] responseBytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(originalBytes, responseBytes);
    }

    [Fact]
    public async Task GetDownload_NoVersionParam_Returns200WithETagHeader()
    {
        // Arrange
        byte[] packageBytes = BuildValidPackageBytes("etag-plugin", "1.0.0");
        (Guid pluginId, _) = await SeedPluginWithStoredPackageAsync(
            "etag-plugin", "1.0.0", packageBytes);

        string expectedSha256 = Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(packageBytes));

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/plugins/{pluginId}/download");

        // Assert — spec: ETag header contains sha256
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string? etag = response.Headers.ETag?.Tag;
        Assert.NotNull(etag);
        Assert.Contains(expectedSha256, etag, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetDownload_NoVersionParam_DownloadCountIncrementedBy1()
    {
        // Arrange
        (Guid pluginId, _) = await SeedPluginWithStoredPackageAsync(
            "counter-plugin", "1.0.0");

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/plugins/{pluginId}/download");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Consume stream so download completes
        _ = await response.Content.ReadAsByteArrayAsync();

        // Assert — all three counters bumped by 1
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        AddOnEntity? plugin = await ctx.Plugins.FindAsync(pluginId);
        Assert.NotNull(plugin);
        Assert.Equal(1L, plugin!.DownloadCount);

        AddOnVersionEntity? version = await ctx.PluginVersions
            .FirstOrDefaultAsync(pv => pv.PluginId == pluginId && pv.Version == "1.0.0");
        Assert.NotNull(version);
        Assert.Equal(1L, version!.DownloadCount);

        long aggCount = await ctx.TelemetryAggregates
            .Where(ta => ta.PluginId == pluginId
                      && ta.Version == "1.0.0"
                      && ta.EventType == "download")
            .SumAsync(ta => ta.Count);
        Assert.Equal(1L, aggCount);
    }

    // =========================================================================
    // GET /api/v1/plugins/{pluginId}/download?version=explicit → 200
    // =========================================================================

    [Fact]
    public async Task GetDownload_ExplicitVersion_Returns200WithCorrectHeaders()
    {
        // Arrange
        (Guid pluginId, _) = await SeedPluginWithStoredPackageAsync(
            "explicit-plugin", "1.5.0");

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/plugins/{pluginId}/download?version=1.5.0");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/gzip", response.Content.Headers.ContentType?.MediaType);

        string? disposition = response.Content.Headers.ContentDisposition?.ToString();
        Assert.NotNull(disposition);
        Assert.Contains("attachment", disposition, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("explicit-plugin-1.5.0.tar.gz", disposition, StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // 404 — unknown plugin
    // VERBATIM spec string: "Plugin not found"
    // =========================================================================

    [Fact]
    public async Task GetDownload_UnknownPlugin_Returns404ProblemDetails()
    {
        // Arrange — no plugin seeded
        Guid unknownId = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/plugins/{unknownId}/download");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        // RFC 7807 ProblemDetails — "detail" must match verbatim spec string
        Assert.True(root.TryGetProperty("detail", out JsonElement detail),
            "Response body must have 'detail' field (ProblemDetails)");
        Assert.Equal("Plugin not found", detail.GetString());
    }

    [Fact]
    public async Task GetDownload_UnknownPlugin_DownloadCountNotIncremented()
    {
        // Arrange — seed an unrelated plugin, then request a different pluginId
        (Guid seededPluginId, _) = await SeedPluginWithStoredPackageAsync(
            "unrelated-plugin", "1.0.0");
        Guid unknownId = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/plugins/{unknownId}/download");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // Assert — seeded plugin counter must stay at 0
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        AddOnEntity? seeded = await ctx.Plugins.FindAsync(seededPluginId);
        Assert.NotNull(seeded);
        Assert.Equal(0L, seeded!.DownloadCount);
    }

    // =========================================================================
    // 404 — unknown explicit version
    // VERBATIM spec string: "Plugin version 9.9.9 not found"
    // =========================================================================

    [Fact]
    public async Task GetDownload_UnknownVersion_Returns404WithVerbatimSpecString()
    {
        // Arrange — plugin exists but version 9.9.9 does not
        (Guid pluginId, _) = await SeedPluginWithStoredPackageAsync(
            "known-plugin", "1.0.0");

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/plugins/{pluginId}/download?version=9.9.9");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement detail = doc.RootElement.GetProperty("detail");

        // VERBATIM spec string
        Assert.Equal("Plugin version 9.9.9 not found", detail.GetString());
    }

    [Fact]
    public async Task GetDownload_UnknownVersion_DownloadCountNotIncremented()
    {
        // Arrange
        (Guid pluginId, _) = await SeedPluginWithStoredPackageAsync(
            "nocount-plugin", "1.0.0");

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/plugins/{pluginId}/download?version=9.9.9");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // Assert — plugin counter remains 0
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        AddOnEntity? plugin = await ctx.Plugins.FindAsync(pluginId);
        Assert.NotNull(plugin);
        Assert.Equal(0L, plugin!.DownloadCount);
    }

    // =========================================================================
    // 400 — invalid version format
    // VERBATIM spec string: "Invalid version format. Expected semver (e.g., 1.0.0)"
    // =========================================================================

    [Theory]
    [InlineData("not-a-version")]
    [InlineData("v1.0.0")]
    [InlineData("1.0")]
    [InlineData("1.0.0-beta")]
    public async Task GetDownload_InvalidVersionFormat_Returns400WithVerbatimSpecString(
        string badVersion)
    {
        // Arrange — plugin exists; bad version format should be caught before DB lookup
        (Guid pluginId, _) = await SeedPluginWithStoredPackageAsync(
            "format-plugin", "1.0.0");

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/plugins/{pluginId}/download?version={Uri.EscapeDataString(badVersion)}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement detail = doc.RootElement.GetProperty("detail");

        // VERBATIM spec string
        Assert.Equal(
            "Invalid version format. Expected semver (e.g., 1.0.0)",
            detail.GetString());
    }

    // =========================================================================
    // Concurrency — N concurrent GET /download requests → count == N (no race)
    // SPEC: "100 concurrent requests → downloadCount incremented exactly 100 times"
    // =========================================================================

    [Fact]
    public async Task GetDownload_50ConcurrentRequests_DownloadCountEqualsExactly50()
    {
        // Arrange — smaller concurrency in HTTP test to avoid test-infrastructure overhead
        const int concurrency = 50;

        (Guid pluginId, _) = await SeedPluginWithStoredPackageAsync(
            "concurrent-http-plugin", "1.0.0");

        // Act
        Task<HttpResponseMessage>[] tasks = Enumerable.Range(0, concurrency)
            .Select(_ => _client.GetAsync($"/api/v1/plugins/{pluginId}/download"))
            .ToArray();

        HttpResponseMessage[] responses = await Task.WhenAll(tasks);

        // Consume all response bodies to ensure streaming completes
        await Task.WhenAll(responses.Select(r => r.Content.ReadAsByteArrayAsync()));

        // Assert — all requests succeeded
        foreach (HttpResponseMessage response in responses)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // Assert — plugin counter equals exactly concurrency (no lost updates)
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        AddOnEntity? plugin = await ctx.Plugins.FindAsync(pluginId);
        Assert.NotNull(plugin);
        Assert.Equal((long)concurrency, plugin!.DownloadCount);

        AddOnVersionEntity? version = await ctx.PluginVersions
            .FirstOrDefaultAsync(pv => pv.PluginId == pluginId && pv.Version == "1.0.0");
        Assert.NotNull(version);
        Assert.Equal((long)concurrency, version!.DownloadCount);
    }
}
