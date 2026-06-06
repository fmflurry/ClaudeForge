using System.Net;
using System.Text.Json;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Tests.Integration.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeForge.Tests.Integration.OpenApi;

/// <summary>
/// Integration tests for Group 10 (tasks 10.1–10.2): OpenAPI document publication.
///
/// Uses WebApplicationFactory&lt;Program&gt; with a real PostgreSQL 16 container —
/// reusing the same Postgres collection as every other HTTP integration test so the
/// app boots identically (DB connection required by Program startup).
///
/// Endpoint under test: GET /openapi/v1.json
///   - Must return HTTP 200 with Content-Type application/json (or application/openapi+json).
///   - Must produce a parseable OpenAPI document.
///   - Must expose an 'openapi' version field and an 'info.title' field.
///   - Must contain every /api/v1 operation documented in design §7.
///
/// Why this test is RED on first run
/// -----------------------------------
/// Program.cs does not yet call AddOpenApi() / MapOpenApi() (built-in .NET 10
/// Microsoft.AspNetCore.OpenApi package).  The /openapi/v1.json endpoint does not
/// exist, so the GET returns 404 Not Found instead of 200 OK — which causes
/// OpenApiDocument_IsServedAt_OpenApiV1Json to fail immediately at the status-code
/// assertion, and all subsequent tests that depend on the parsed document to fail as
/// well.
///
/// What the coder must wire (GREEN spec)
/// --------------------------------------
/// 1. In Program.cs (or a module helper), add:
///      builder.Services.AddOpenApi();          // registers OpenAPI document generation
///    and after app.Build():
///      app.MapOpenApi();                        // serves GET /openapi/v1.json
///    (Both are in Microsoft.AspNetCore.OpenApi which ships with the .NET 10 SDK —
///     no extra NuGet package required for the Api project.)
///
/// 2. Each Minimal API endpoint registered via MapGet/MapPost/etc. should already be
///    discovered automatically.  No individual .WithOpenApi() calls are strictly needed
///    for presence tests, but the coder should add metadata tags/summaries as
///    appropriate for accurate schema documentation (task 10.1).
///
/// 3. For the static openapi.json artifact (task 10.2 CI step), add a dotnet-openapi
///    or Microsoft.Extensions.ApiDescription.Server MSBuild target to emit the file
///    during build.  This test only covers the runtime endpoint; the CI artifact step
///    is covered by the CI workflow config (not a C# test).
///
/// Path normalization note
/// -----------------------
/// Route template constraints (e.g. {pluginId:guid}) are stripped before comparison
/// so that "{pluginId}" and "{pluginId:guid}" both match the expected path pattern.
/// HTTP method comparison is case-insensitive.
/// </summary>
[Collection(PostgresFixture.CollectionName)]
public sealed class OpenApiDocumentTests : IAsyncLifetime
{
    /// <summary>The canonical endpoint path for the built-in .NET 10 OpenAPI document.</summary>
    private const string OpenApiEndpoint = "/openapi/v1.json";

    private readonly PostgresFixture _fixture;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public OpenApiDocumentTests(PostgresFixture fixture)
    {
        _fixture = fixture;

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace DbContext with the test container connection — identical
                    // pattern to every other HTTP integration test in this project.
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

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Normalizes an OpenAPI path key so that route-constraint suffixes such as
    /// ":guid" inside a template segment are stripped before comparison.
    /// e.g. "/api/v1/plugins/{pluginId:guid}" → "/api/v1/plugins/{pluginId}"
    /// </summary>
    private static string NormalizePath(string path)
    {
        // Strip ":constraint" from any "{param:constraint}" segment.
        System.Text.RegularExpressions.Regex constraintPattern =
            new(@"\{([^:}]+):[^}]+\}", System.Text.RegularExpressions.RegexOptions.Compiled);
        return constraintPattern.Replace(path, "{$1}");
    }

    /// <summary>
    /// Returns the set of (normalizedPath, method) tuples present in the parsed
    /// OpenAPI paths object.
    /// </summary>
    private static HashSet<(string Path, string Method)> ExtractOperations(JsonElement pathsElement)
    {
        HashSet<(string Path, string Method)> result = new HashSet<(string Path, string Method)>();

        foreach (JsonProperty pathProp in pathsElement.EnumerateObject())
        {
            string normalizedPath = NormalizePath(pathProp.Name);
            foreach (JsonProperty methodProp in pathProp.Value.EnumerateObject())
            {
                result.Add((normalizedPath, methodProp.Name.ToUpperInvariant()));
            }
        }

        return result;
    }

    // -------------------------------------------------------------------------
    // Test 1: The OpenAPI document endpoint is served and returns HTTP 200.
    // RED reason: /openapi/v1.json does not exist yet (404) — Program.cs has not
    // called AddOpenApi() + MapOpenApi().
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OpenApiDocument_IsServedAt_OpenApiV1Json_Returns200()
    {
        // Act
        HttpResponseMessage response = await _client.GetAsync(OpenApiEndpoint);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // Test 2: The response body is valid JSON parseable as an OpenAPI document.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OpenApiDocument_ResponseBody_IsValidJson()
    {
        // Act
        HttpResponseMessage response = await _client.GetAsync(OpenApiEndpoint);
        response.EnsureSuccessStatusCode();

        string body = await response.Content.ReadAsStringAsync();

        // Assert — must parse without throwing
        using JsonDocument doc = JsonDocument.Parse(body);
        Assert.NotNull(doc);
    }

    // -------------------------------------------------------------------------
    // Test 3: The document contains the required top-level OpenAPI fields.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OpenApiDocument_ContainsOpenApiVersionAndInfoTitle()
    {
        // Act
        HttpResponseMessage response = await _client.GetAsync(OpenApiEndpoint);
        response.EnsureSuccessStatusCode();

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        // Assert — 'openapi' field (e.g. "3.0.1" or "3.1.0")
        Assert.True(
            root.TryGetProperty("openapi", out JsonElement openApiVersion),
            "OpenAPI document must have an 'openapi' version field");
        string? versionString = openApiVersion.GetString();
        Assert.False(string.IsNullOrWhiteSpace(versionString),
            "The 'openapi' field must contain a non-empty version string");

        // Assert — 'info.title' field
        Assert.True(
            root.TryGetProperty("info", out JsonElement info),
            "OpenAPI document must have an 'info' object");
        Assert.True(
            info.TryGetProperty("title", out JsonElement title),
            "OpenAPI document 'info' must have a 'title' field");
        Assert.False(string.IsNullOrWhiteSpace(title.GetString()),
            "The 'info.title' field must be a non-empty string");
    }

    // -------------------------------------------------------------------------
    // Test 4: The document exposes a 'paths' object.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OpenApiDocument_ContainsPathsObject()
    {
        // Act
        HttpResponseMessage response = await _client.GetAsync(OpenApiEndpoint);
        response.EnsureSuccessStatusCode();

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        // Assert
        Assert.True(
            root.TryGetProperty("paths", out JsonElement paths),
            "OpenAPI document must contain a 'paths' object");
        Assert.Equal(JsonValueKind.Object, paths.ValueKind);
    }

    // -------------------------------------------------------------------------
    // Test 5: All documented /api/v1 operations are present in the spec.
    //
    // Design §7 full REST surface:
    //   GET  /api/v1/plugins
    //   GET  /api/v1/plugins/{pluginId}
    //   POST /api/v1/plugins/upload
    //   GET  /api/v1/plugins/{pluginId}/versions
    //   POST /api/v1/plugins/{pluginId}/versions
    //   GET  /api/v1/plugins/{pluginId}/versions/{version}
    //   GET  /api/v1/plugins/{pluginId}/download
    //   GET  /api/v1/plugins/search
    //   GET  /api/v1/search
    //   GET  /api/v1/discovery
    //   GET  /api/v1/categories
    //   POST /api/v1/telemetry/events
    //   GET  /api/v1/plugins/{pluginId}/telemetry/summary
    //   GET  /api/v1/docs
    //   GET  /api/v1/docs/{slug}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OpenApiDocument_ContainsAllDocumentedOperations_PluginCatalog()
    {
        // Arrange
        HashSet<(string Path, string Method)> operations = await GetOperationsAsync();

        // Assert — Plugin Catalog (design §7)
        AssertOperationPresent(operations, "/api/v1/plugins", "GET");
        AssertOperationPresent(operations, "/api/v1/plugins/{pluginId}", "GET");
        AssertOperationPresent(operations, "/api/v1/categories", "GET");
    }

    [Fact]
    public async Task OpenApiDocument_ContainsAllDocumentedOperations_PluginPublishing()
    {
        // Arrange
        HashSet<(string Path, string Method)> operations = await GetOperationsAsync();

        // Assert — Plugin Publishing (design §7)
        AssertOperationPresent(operations, "/api/v1/plugins/upload", "POST");
        AssertOperationPresent(operations, "/api/v1/plugins/{pluginId}/versions", "GET");
        AssertOperationPresent(operations, "/api/v1/plugins/{pluginId}/versions", "POST");
        AssertOperationPresent(operations, "/api/v1/plugins/{pluginId}/versions/{version}", "GET");
    }

    [Fact]
    public async Task OpenApiDocument_ContainsAllDocumentedOperations_PluginDistribution()
    {
        // Arrange
        HashSet<(string Path, string Method)> operations = await GetOperationsAsync();

        // Assert — Plugin Distribution (design §7)
        AssertOperationPresent(operations, "/api/v1/plugins/{pluginId}/download", "GET");
    }

    [Fact]
    public async Task OpenApiDocument_ContainsAllDocumentedOperations_SearchAndDiscovery()
    {
        // Arrange
        HashSet<(string Path, string Method)> operations = await GetOperationsAsync();

        // Assert — Search & Discovery (design §7)
        AssertOperationPresent(operations, "/api/v1/plugins/search", "GET");
        AssertOperationPresent(operations, "/api/v1/search", "GET");
        AssertOperationPresent(operations, "/api/v1/discovery", "GET");
    }

    [Fact]
    public async Task OpenApiDocument_ContainsAllDocumentedOperations_Telemetry()
    {
        // Arrange
        HashSet<(string Path, string Method)> operations = await GetOperationsAsync();

        // Assert — Telemetry (design §7)
        AssertOperationPresent(operations, "/api/v1/telemetry/events", "POST");
        AssertOperationPresent(operations, "/api/v1/plugins/{pluginId}/telemetry/summary", "GET");
    }

    [Fact]
    public async Task OpenApiDocument_ContainsAllDocumentedOperations_Docs()
    {
        // Arrange
        HashSet<(string Path, string Method)> operations = await GetOperationsAsync();

        // Assert — Docs (design §7)
        AssertOperationPresent(operations, "/api/v1/docs", "GET");
        AssertOperationPresent(operations, "/api/v1/docs/{slug}", "GET");
    }

    // -------------------------------------------------------------------------
    // Test 6: The document contains more than zero paths (smoke sanity guard).
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OpenApiDocument_ContainsAtLeastFifteenPaths()
    {
        // Act
        HashSet<(string Path, string Method)> operations = await GetOperationsAsync();

        // Assert — we have 15 operations documented in design §7 (+ /health is bonus)
        Assert.True(
            operations.Count >= 15,
            $"Expected at least 15 documented operations in /openapi/v1.json, found {operations.Count}. " +
            $"Ensure AddOpenApi() + MapOpenApi() are wired and all modules register their endpoints.");
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task<HashSet<(string Path, string Method)>> GetOperationsAsync()
    {
        HttpResponseMessage response = await _client.GetAsync(OpenApiEndpoint);
        response.EnsureSuccessStatusCode();

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        Assert.True(
            doc.RootElement.TryGetProperty("paths", out JsonElement paths),
            "OpenAPI document must contain a 'paths' object");

        return ExtractOperations(paths);
    }

    private static void AssertOperationPresent(
        HashSet<(string Path, string Method)> operations,
        string expectedPath,
        string expectedMethod)
    {
        string normalizedExpected = NormalizePath(expectedPath);
        string upperMethod = expectedMethod.ToUpperInvariant();

        bool found = operations.Any(op =>
            string.Equals(NormalizePath(op.Path), normalizedExpected, StringComparison.OrdinalIgnoreCase)
            && string.Equals(op.Method, upperMethod, StringComparison.OrdinalIgnoreCase));

        Assert.True(
            found,
            $"Expected operation {upperMethod} {normalizedExpected} was not found in the OpenAPI document. " +
            $"Available operations: {string.Join(", ", operations.Select(o => $"{o.Method} {o.Path}"))}");
    }
}
