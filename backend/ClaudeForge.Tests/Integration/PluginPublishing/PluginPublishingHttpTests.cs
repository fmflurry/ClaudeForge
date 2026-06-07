using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Tests.Integration.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeForge.Tests.Integration.PluginPublishing;

/// <summary>
/// HTTP integration tests for Group 5: Plugin Publishing API endpoints.
///
/// Uses WebApplicationFactory&lt;Program&gt; with a real PostgreSQL 16 container.
/// Tests the full HTTP stack: routing, multipart parsing, exception middleware.
///
/// Endpoints under test:
///   POST /api/v1/plugins/upload               — multipart upload → 201 {pluginId, version}
///   POST /api/v1/plugins/{pluginId}/versions  — add new version → 201
///   GET  /api/v1/plugins/{pluginId}/versions  — paginated version history (semver desc)
///   GET  /api/v1/plugins/{pluginId}/versions/{version} — single version detail
///   PATCH /api/v1/plugins/{pluginId}/versions/{version} → 405 Method Not Allowed
///
/// Verbatim spec error strings used:
///   "Package file is required"                                         (plugin-upload/spec.md)
///   "Required field missing: name"                                     (plugin-upload/spec.md)
///   "initialVersion must be a valid semantic version (e.g., 1.0.0)"   (plugin-upload/spec.md)
///   "A plugin with name 'DupPlugin' already exists"                    (plugin-upload/spec.md)
///   "Version 1.5.0 already exists"                                     (plugin-versioning/spec.md)
///   "Plugin not found"                                                 (plugin-versioning/spec.md)
///   "Version not found"                                                (plugin-versioning/spec.md)
///   "Version must be in format MAJOR.MINOR.PATCH (e.g., 1.2.3)"       (plugin-versioning/spec.md)
/// </summary>
[Collection(PostgresFixture.CollectionName)]
public sealed class PluginPublishingHttpTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public PluginPublishingHttpTests(PostgresFixture fixture)
    {
        _fixture = fixture;

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace the DbContext registration with the test container connection
                    ServiceDescriptor? descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<MarketplaceDbContext>));
                    if (descriptor is not null)
                        services.Remove(descriptor);

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
    // Per-test isolation: truncate all marketplace tables before each test.
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

    // =========================================================================
    // Archive builders (BCL only — no SharpZipLib)
    // =========================================================================

    private static MemoryStream BuildTarGz(IEnumerable<(string name, string content)> entries)
    {
        MemoryStream output = new();

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

        output.Position = 0;
        return output;
    }

    private static MemoryStream BuildValidPluginTarGz(
        string name = "test-plugin",
        string version = "1.0.0",
        string description = "Test plugin description",
        string author = "Test Author",
        string readme = "# Test Plugin") =>
        BuildTarGz([
            ("plugin.json",
                $$"""
                {
                  "name": "{{name}}",
                  "version": "{{version}}",
                  "description": "{{description}}",
                  "author": "{{author}}",
                  "types": ["skill"],
                  "languages": ["typescript"]
                }
                """),
            ("README.md", readme),
        ]);

    // =========================================================================
    // Multipart helper
    // =========================================================================

    private static MultipartFormDataContent BuildUploadForm(
        MemoryStream? archiveStream = null,
        string fileName = "plugin-1.0.0.tar.gz",
        string name = "TestPlugin",
        string description = "Test description",
        string author = "Test Author",
        string initialVersion = "1.0.0",
        string releaseNotes = "Initial release",
        bool includePackage = true)
    {
        MultipartFormDataContent form = new();

        if (includePackage)
        {
            archiveStream ??= BuildValidPluginTarGz(
                name: name.ToLowerInvariant(), version: initialVersion);
            archiveStream.Position = 0;
            StreamContent packageContent = new(archiveStream);
            packageContent.Headers.ContentType =
                new MediaTypeHeaderValue("application/gzip");
            form.Add(packageContent, "package", fileName);
        }

        form.Add(new StringContent(name), "name");
        form.Add(new StringContent(description), "description");
        form.Add(new StringContent(author), "author");
        form.Add(new StringContent(initialVersion), "initialVersion");
        form.Add(new StringContent(releaseNotes), "releaseNotes");

        return form;
    }

    private static MultipartFormDataContent BuildVersionForm(
        MemoryStream? archiveStream = null,
        string fileName = "plugin-1.1.0.tar.gz",
        string version = "1.1.0",
        string releaseNotes = "New version",
        bool includePackage = true)
    {
        MultipartFormDataContent form = new();

        if (includePackage)
        {
            archiveStream ??= BuildValidPluginTarGz(version: version);
            archiveStream.Position = 0;
            StreamContent packageContent = new(archiveStream);
            packageContent.Headers.ContentType =
                new MediaTypeHeaderValue("application/gzip");
            form.Add(packageContent, "package", fileName);
        }

        form.Add(new StringContent(version), "versionNumber");
        form.Add(new StringContent(releaseNotes), "releaseNotes");

        return form;
    }

    // =========================================================================
    // POST /api/v1/plugins/upload — happy path
    // =========================================================================

    [Fact]
    public async Task PostUpload_ValidMultipartTarGz_Returns201WithPluginIdAndVersion()
    {
        // Arrange
        using MultipartFormDataContent form = BuildUploadForm(
            name: "MyGreatPlugin", initialVersion: "1.0.0");

        // Act
        HttpResponseMessage response = await _client.PostAsync("/api/v1/plugins/upload", form);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        Assert.True(root.TryGetProperty("pluginId", out JsonElement pluginId),
            "Response must have 'pluginId' property");
        Assert.True(root.TryGetProperty("version", out JsonElement version),
            "Response must have 'version' property");

        // pluginId must be a valid non-empty Guid string
        Assert.True(Guid.TryParse(pluginId.GetString(), out Guid parsedId));
        Assert.NotEqual(Guid.Empty, parsedId);
        Assert.Equal("1.0.0", version.GetString());
    }

    [Fact]
    public async Task PostUpload_ValidPackage_CreatesPluginWithInitialVersionIsLatestTrue()
    {
        // Arrange
        using MultipartFormDataContent form = BuildUploadForm(
            name: "IsLatestHttpPlugin", initialVersion: "1.0.0");

        // Act
        HttpResponseMessage response = await _client.PostAsync("/api/v1/plugins/upload", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument responseDoc = JsonDocument.Parse(body);
        Guid pluginId = Guid.Parse(responseDoc.RootElement.GetProperty("pluginId").GetString()!);

        // Assert — verify the version was created with isLatest=true in DB
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        Infrastructure.Persistence.Entities.PluginVersionEntity? version = await ctx.PluginVersions
            .FirstOrDefaultAsync(v => v.PluginId == pluginId);

        Assert.NotNull(version);
        Assert.True(version!.IsLatest);
    }

    [Fact]
    public async Task PostUpload_ValidPackage_ReadmeExtractedAndStoredInDatabase()
    {
        // Arrange
        const string readmeContent = "# My Awesome Plugin\n\nThis README is extracted.";
        MemoryStream archive = BuildValidPluginTarGz(
            name: "readme-extract-plugin",
            version: "1.0.0",
            readme: readmeContent);

        using MultipartFormDataContent form = BuildUploadForm(
            archiveStream: archive,
            name: "ReadmeExtractPlugin",
            initialVersion: "1.0.0");

        // Act
        HttpResponseMessage response = await _client.PostAsync("/api/v1/plugins/upload", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument responseDoc = JsonDocument.Parse(body);
        Guid pluginId = Guid.Parse(responseDoc.RootElement.GetProperty("pluginId").GetString()!);

        // Assert — README persisted
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        Infrastructure.Persistence.Entities.PluginVersionEntity? version = await ctx.PluginVersions
            .FirstOrDefaultAsync(v => v.PluginId == pluginId);

        Assert.NotNull(version);
        Assert.Equal(readmeContent, version!.ReadmeText);
    }

    [Fact]
    public async Task PostUpload_CustomInitialVersion_AcceptsNon100Version()
    {
        // Arrange
        using MultipartFormDataContent form = BuildUploadForm(
            name: "CustomVersionPlugin",
            initialVersion: "2.5.3",
            archiveStream: BuildValidPluginTarGz(version: "2.5.3"));

        // Act
        HttpResponseMessage response = await _client.PostAsync("/api/v1/plugins/upload", form);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        Assert.Equal("2.5.3", doc.RootElement.GetProperty("version").GetString());
    }

    [Fact]
    public async Task PostUpload_WithoutReleaseNotes_DefaultsToEmpty()
    {
        // Arrange
        using MultipartFormDataContent form = BuildUploadForm(
            name: "NoReleaseNotesPlugin",
            releaseNotes: "");

        // Act
        HttpResponseMessage response = await _client.PostAsync("/api/v1/plugins/upload", form);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // =========================================================================
    // POST /api/v1/plugins/upload — missing package file → 400
    // VERBATIM spec string: "Package file is required"
    // =========================================================================

    [Fact]
    public async Task PostUpload_MissingPackageFile_Returns400WithSpecExactDetail()
    {
        // Arrange — no file included
        using MultipartFormDataContent form = BuildUploadForm(includePackage: false);

        // Act
        HttpResponseMessage response = await _client.PostAsync("/api/v1/plugins/upload", form);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement detail = doc.RootElement.GetProperty("detail");

        Assert.Equal("Package file is required", detail.GetString());
    }

    // =========================================================================
    // POST /api/v1/plugins/upload — missing required metadata field → 400
    // VERBATIM spec string: "Required field missing: name"
    // =========================================================================

    [Fact]
    public async Task PostUpload_ManifestMissingName_Returns400WithSpecExactDetail()
    {
        // Arrange — manifest JSON has no "name" field
        MemoryStream archive = BuildTarGz([
            ("plugin.json",
                """{"version":"1.0.0","description":"Desc","author":"Author","types":["skill"],"languages":["typescript"]}"""),
        ]);

        using MultipartFormDataContent form = BuildUploadForm(
            archiveStream: archive,
            name: "SomePlugin");

        // Act
        HttpResponseMessage response = await _client.PostAsync("/api/v1/plugins/upload", form);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement detail = doc.RootElement.GetProperty("detail");

        Assert.Equal("Required field missing: name", detail.GetString());
    }

    // =========================================================================
    // POST /api/v1/plugins/upload — invalid semver → 400
    // VERBATIM spec string: "initialVersion must be a valid semantic version (e.g., 1.0.0)"
    // =========================================================================

    [Fact]
    public async Task PostUpload_InvalidSemVer_Returns400WithSpecExactDetail()
    {
        // Arrange — invalid version in manifest
        MemoryStream archive = BuildTarGz([
            ("plugin.json",
                """{"name":"test","version":"not-a-version","description":"Desc","author":"Author","types":["skill"],"languages":["typescript"]}"""),
        ]);

        using MultipartFormDataContent form = BuildUploadForm(
            archiveStream: archive,
            name: "SomPlugin",
            initialVersion: "not-a-version");

        // Act
        HttpResponseMessage response = await _client.PostAsync("/api/v1/plugins/upload", form);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement detail = doc.RootElement.GetProperty("detail");

        Assert.Equal("initialVersion must be a valid semantic version (e.g., 1.0.0)",
            detail.GetString());
    }

    // =========================================================================
    // POST /api/v1/plugins/upload — duplicate name → 409
    // VERBATIM spec string: "A plugin with name 'DupPlugin' already exists"
    // =========================================================================

    [Fact]
    public async Task PostUpload_DuplicateName_Returns409WithSpecExactDetail()
    {
        // Arrange — upload "DupPlugin" first
        using MultipartFormDataContent firstForm = BuildUploadForm(
            name: "DupPlugin",
            archiveStream: BuildValidPluginTarGz(name: "dupplugin", version: "1.0.0"));
        HttpResponseMessage first = await _client.PostAsync("/api/v1/plugins/upload", firstForm);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Arrange — attempt to upload with same name
        using MultipartFormDataContent dupForm = BuildUploadForm(
            name: "DupPlugin",
            archiveStream: BuildValidPluginTarGz(name: "dupplugin", version: "1.0.0"));

        // Act
        HttpResponseMessage response = await _client.PostAsync("/api/v1/plugins/upload", dupForm);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement detail = doc.RootElement.GetProperty("detail");

        Assert.Equal("A plugin with name 'DupPlugin' already exists", detail.GetString());
    }

    [Fact]
    public async Task PostUpload_DuplicateNameDifferentCasing_Returns409()
    {
        // Arrange — upload "myplugin" first
        using MultipartFormDataContent firstForm = BuildUploadForm(
            name: "myplugin",
            archiveStream: BuildValidPluginTarGz(name: "myplugin", version: "1.0.0"));
        HttpResponseMessage first = await _client.PostAsync("/api/v1/plugins/upload", firstForm);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Attempt with different casing "MyPlugin"
        using MultipartFormDataContent dupForm = BuildUploadForm(
            name: "MyPlugin",
            archiveStream: BuildValidPluginTarGz(name: "myplugin", version: "1.0.0"));

        // Act
        HttpResponseMessage response = await _client.PostAsync("/api/v1/plugins/upload", dupForm);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // =========================================================================
    // POST /api/v1/plugins/{pluginId}/versions — happy path
    // =========================================================================

    [Fact]
    public async Task PostVersion_ValidNewVersion_Returns201WithVersionRecord()
    {
        // Arrange — upload initial plugin
        using MultipartFormDataContent uploadForm = BuildUploadForm(
            name: "VersionedPlugin",
            initialVersion: "1.0.0",
            archiveStream: BuildValidPluginTarGz(name: "versionedplugin", version: "1.0.0"));
        HttpResponseMessage uploadResp = await _client.PostAsync("/api/v1/plugins/upload", uploadForm);
        Assert.Equal(HttpStatusCode.Created, uploadResp.StatusCode);

        string uploadBody = await uploadResp.Content.ReadAsStringAsync();
        using JsonDocument uploadDoc = JsonDocument.Parse(uploadBody);
        Guid pluginId = Guid.Parse(uploadDoc.RootElement.GetProperty("pluginId").GetString()!);

        // Arrange — version form
        using MultipartFormDataContent versionForm = BuildVersionForm(
            archiveStream: BuildValidPluginTarGz(name: "versionedplugin", version: "1.1.0"),
            version: "1.1.0",
            releaseNotes: "Added new feature X");

        // Act
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/plugins/{pluginId}/versions", versionForm);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        Assert.True(root.TryGetProperty("version", out JsonElement versionProp));
        Assert.Equal("1.1.0", versionProp.GetString());
    }

    [Fact]
    public async Task PostVersion_NewVersion_FlipsPriorIsLatest()
    {
        // Arrange — create plugin with 1.0.0
        using MultipartFormDataContent uploadForm = BuildUploadForm(
            name: "FlipLatestHttpPlugin",
            initialVersion: "1.0.0",
            archiveStream: BuildValidPluginTarGz(name: "fliplatesththtpplugin", version: "1.0.0"));
        HttpResponseMessage uploadResp = await _client.PostAsync("/api/v1/plugins/upload", uploadForm);
        Assert.Equal(HttpStatusCode.Created, uploadResp.StatusCode);

        string uploadBody = await uploadResp.Content.ReadAsStringAsync();
        using JsonDocument uploadDoc = JsonDocument.Parse(uploadBody);
        Guid pluginId = Guid.Parse(uploadDoc.RootElement.GetProperty("pluginId").GetString()!);

        // Act — publish 2.0.0
        using MultipartFormDataContent versionForm = BuildVersionForm(
            archiveStream: BuildValidPluginTarGz(version: "2.0.0"),
            version: "2.0.0");
        await _client.PostAsync($"/api/v1/plugins/{pluginId}/versions", versionForm);

        // Assert — only 2.0.0 is latest in DB
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        List<Infrastructure.Persistence.Entities.PluginVersionEntity> versions =
            await ctx.PluginVersions.Where(v => v.PluginId == pluginId).ToListAsync();

        Assert.Equal(2, versions.Count);
        Assert.Equal(1, versions.Count(v => v.IsLatest));
        Assert.Equal("2.0.0", versions.First(v => v.IsLatest).Version);
    }

    // =========================================================================
    // POST /api/v1/plugins/{pluginId}/versions — duplicate version → 409
    // VERBATIM spec string: "Version 1.5.0 already exists"
    // =========================================================================

    [Fact]
    public async Task PostVersion_DuplicateVersion_Returns409WithSpecExactDetail()
    {
        // Arrange — upload 1.0.0 then publish 1.5.0
        using MultipartFormDataContent uploadForm = BuildUploadForm(
            name: "DupVersionHttpPlugin",
            initialVersion: "1.0.0",
            archiveStream: BuildValidPluginTarGz(name: "dupversionhttpplugin", version: "1.0.0"));
        HttpResponseMessage uploadResp = await _client.PostAsync("/api/v1/plugins/upload", uploadForm);
        Assert.Equal(HttpStatusCode.Created, uploadResp.StatusCode);

        string uploadBody = await uploadResp.Content.ReadAsStringAsync();
        using JsonDocument uploadDoc = JsonDocument.Parse(uploadBody);
        Guid pluginId = Guid.Parse(uploadDoc.RootElement.GetProperty("pluginId").GetString()!);

        // First publish 1.5.0
        using MultipartFormDataContent first150 = BuildVersionForm(
            archiveStream: BuildValidPluginTarGz(version: "1.5.0"), version: "1.5.0");
        HttpResponseMessage first = await _client.PostAsync(
            $"/api/v1/plugins/{pluginId}/versions", first150);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Attempt to publish 1.5.0 again
        using MultipartFormDataContent dup150 = BuildVersionForm(
            archiveStream: BuildValidPluginTarGz(version: "1.5.0"), version: "1.5.0");

        // Act
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/plugins/{pluginId}/versions", dup150);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement detail = doc.RootElement.GetProperty("detail");

        Assert.Equal("Version 1.5.0 already exists", detail.GetString());
    }

    // =========================================================================
    // POST /api/v1/plugins/{pluginId}/versions — plugin not found → 404
    // VERBATIM spec string: "Plugin not found"
    // =========================================================================

    [Fact]
    public async Task PostVersion_UnknownPlugin_Returns404WithSpecExactDetail()
    {
        // Arrange
        Guid unknownPluginId = Guid.NewGuid();
        using MultipartFormDataContent form = BuildVersionForm(
            archiveStream: BuildValidPluginTarGz(version: "1.0.0"), version: "1.0.0");

        // Act
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/plugins/{unknownPluginId}/versions", form);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement detail = doc.RootElement.GetProperty("detail");

        Assert.Equal("Plugin not found", detail.GetString());
    }

    // =========================================================================
    // POST /api/v1/plugins/{pluginId}/versions — invalid version format → 400
    // VERBATIM spec string: "Version must be in format MAJOR.MINOR.PATCH (e.g., 1.2.3)"
    // =========================================================================

    [Theory]
    [InlineData("2.3")]
    [InlineData("v2.3.4")]
    [InlineData("2.3.4-beta")]
    public async Task PostVersion_InvalidVersionFormat_Returns400WithSpecExactDetail(
        string badVersion)
    {
        // Arrange — create a plugin first
        using MultipartFormDataContent uploadForm = BuildUploadForm(
            name: "InvalidVerFormatPlugin",
            initialVersion: "1.0.0",
            archiveStream: BuildValidPluginTarGz(name: "invalidverformatplugin", version: "1.0.0"));
        HttpResponseMessage uploadResp = await _client.PostAsync("/api/v1/plugins/upload", uploadForm);
        Assert.Equal(HttpStatusCode.Created, uploadResp.StatusCode);

        string uploadBody = await uploadResp.Content.ReadAsStringAsync();
        using JsonDocument uploadDoc = JsonDocument.Parse(uploadBody);
        Guid pluginId = Guid.Parse(uploadDoc.RootElement.GetProperty("pluginId").GetString()!);

        using MultipartFormDataContent form = BuildVersionForm(
            archiveStream: BuildTarGz([
                ("plugin.json",
                    $$"""{"name":"test","version":"{{badVersion}}","description":"Desc","author":"Author","types":["skill"],"languages":["typescript"]}"""),
            ]),
            version: badVersion,
            fileName: $"plugin-{badVersion}.tar.gz");

        // Act
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/plugins/{pluginId}/versions", form);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement detail = doc.RootElement.GetProperty("detail");

        Assert.Equal("Version must be in format MAJOR.MINOR.PATCH (e.g., 1.2.3)",
            detail.GetString());
    }

    // =========================================================================
    // GET /api/v1/plugins/{pluginId}/versions — paginated version history, semver desc
    // =========================================================================

    [Fact]
    public async Task GetVersionHistory_WithMultipleVersions_Returns200PaginatedSemVerDesc()
    {
        // Arrange — create plugin and two more versions
        using MultipartFormDataContent uploadForm = BuildUploadForm(
            name: "VersionHistHttpPlugin",
            initialVersion: "1.0.0",
            archiveStream: BuildValidPluginTarGz(name: "versionhisthttpplugin", version: "1.0.0"));
        HttpResponseMessage uploadResp = await _client.PostAsync("/api/v1/plugins/upload", uploadForm);
        Assert.Equal(HttpStatusCode.Created, uploadResp.StatusCode);

        string uploadBody = await uploadResp.Content.ReadAsStringAsync();
        using JsonDocument uploadDoc = JsonDocument.Parse(uploadBody);
        Guid pluginId = Guid.Parse(uploadDoc.RootElement.GetProperty("pluginId").GetString()!);

        foreach (string ver in new[] { "1.1.0", "2.0.0" })
        {
            using MultipartFormDataContent vf = BuildVersionForm(
                archiveStream: BuildValidPluginTarGz(version: ver), version: ver);
            HttpResponseMessage vr = await _client.PostAsync(
                $"/api/v1/plugins/{pluginId}/versions", vf);
            Assert.Equal(HttpStatusCode.Created, vr.StatusCode);
        }

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/plugins/{pluginId}/versions");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        // Paginated envelope shape
        Assert.True(root.TryGetProperty("data", out JsonElement data));
        Assert.True(root.TryGetProperty("totalCount", out JsonElement totalCount));
        Assert.True(root.TryGetProperty("page", out JsonElement page));
        Assert.True(root.TryGetProperty("limit", out JsonElement limit));
        Assert.True(root.TryGetProperty("totalPages", out _));

        Assert.Equal(3, totalCount.GetInt32());
        Assert.Equal(3, data.GetArrayLength());

        // SemVer descending: 2.0.0, 1.1.0, 1.0.0
        Assert.Equal("2.0.0", data[0].GetProperty("version").GetString());
        Assert.Equal("1.1.0", data[1].GetProperty("version").GetString());
        Assert.Equal("1.0.0", data[2].GetProperty("version").GetString());

        // isLatest only on 2.0.0
        Assert.True(data[0].GetProperty("isLatest").GetBoolean());
        Assert.False(data[1].GetProperty("isLatest").GetBoolean());
        Assert.False(data[2].GetProperty("isLatest").GetBoolean());
    }

    [Fact]
    public async Task GetVersionHistory_WithPaginationParams_ReturnsCorrectSubset()
    {
        // Arrange
        using MultipartFormDataContent uploadForm = BuildUploadForm(
            name: "PaginatedHistPlugin",
            initialVersion: "1.0.0",
            archiveStream: BuildValidPluginTarGz(name: "paginatedhistplugin", version: "1.0.0"));
        HttpResponseMessage uploadResp = await _client.PostAsync("/api/v1/plugins/upload", uploadForm);
        Assert.Equal(HttpStatusCode.Created, uploadResp.StatusCode);

        string uploadBody = await uploadResp.Content.ReadAsStringAsync();
        using JsonDocument uploadDoc = JsonDocument.Parse(uploadBody);
        Guid pluginId = Guid.Parse(uploadDoc.RootElement.GetProperty("pluginId").GetString()!);

        foreach (string ver in new[] { "1.1.0", "1.2.0", "1.3.0", "1.4.0" })
        {
            using MultipartFormDataContent vf = BuildVersionForm(
                archiveStream: BuildValidPluginTarGz(version: ver), version: ver);
            await _client.PostAsync($"/api/v1/plugins/{pluginId}/versions", vf);
        }

        // Act — page 1, limit 10 (should return all 5 in semver desc)
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/plugins/{pluginId}/versions?page=1&limit=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        Assert.Equal(5, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(10, root.GetProperty("limit").GetInt32());
        Assert.Equal(1, root.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task GetVersionHistory_NoPageParams_DefaultsToPage1Limit20()
    {
        // Arrange
        using MultipartFormDataContent uploadForm = BuildUploadForm(
            name: "DefaultPaginationPlugin",
            initialVersion: "1.0.0",
            archiveStream: BuildValidPluginTarGz(name: "defaultpaginationplugin", version: "1.0.0"));
        HttpResponseMessage uploadResp = await _client.PostAsync("/api/v1/plugins/upload", uploadForm);
        Assert.Equal(HttpStatusCode.Created, uploadResp.StatusCode);

        string uploadBody = await uploadResp.Content.ReadAsStringAsync();
        using JsonDocument uploadDoc = JsonDocument.Parse(uploadBody);
        Guid pluginId = Guid.Parse(uploadDoc.RootElement.GetProperty("pluginId").GetString()!);

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/plugins/{pluginId}/versions");

        // Assert defaults
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(20, root.GetProperty("limit").GetInt32());
    }

    [Fact]
    public async Task GetVersionHistory_PageBeyondAvailable_Returns200EmptyDataWithCorrectMeta()
    {
        // Arrange — 1 version, ask for page 3 with limit 10
        using MultipartFormDataContent uploadForm = BuildUploadForm(
            name: "BeyondRangeHttpPlugin",
            initialVersion: "1.0.0",
            archiveStream: BuildValidPluginTarGz(name: "beyondrangehttpplugin", version: "1.0.0"));
        HttpResponseMessage uploadResp = await _client.PostAsync("/api/v1/plugins/upload", uploadForm);
        Assert.Equal(HttpStatusCode.Created, uploadResp.StatusCode);

        string uploadBody = await uploadResp.Content.ReadAsStringAsync();
        using JsonDocument uploadDoc = JsonDocument.Parse(uploadBody);
        Guid pluginId = Guid.Parse(uploadDoc.RootElement.GetProperty("pluginId").GetString()!);

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/plugins/{pluginId}/versions?page=3&limit=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        Assert.Equal(0, root.GetProperty("data").GetArrayLength());
        Assert.Equal(3, root.GetProperty("page").GetInt32());
        Assert.Equal(1, root.GetProperty("totalCount").GetInt32()); // 1 version exists
        Assert.Equal(1, root.GetProperty("totalPages").GetInt32()); // ceil(1/10) = 1
    }

    [Fact]
    public async Task GetVersionHistory_VersionEntryContainsRequiredFields()
    {
        // Arrange
        using MultipartFormDataContent uploadForm = BuildUploadForm(
            name: "FieldsPlugin",
            initialVersion: "1.0.0",
            releaseNotes: "Initial release",
            archiveStream: BuildValidPluginTarGz(name: "fieldsplugin", version: "1.0.0"));
        HttpResponseMessage uploadResp = await _client.PostAsync("/api/v1/plugins/upload", uploadForm);
        Assert.Equal(HttpStatusCode.Created, uploadResp.StatusCode);

        string uploadBody = await uploadResp.Content.ReadAsStringAsync();
        using JsonDocument uploadDoc = JsonDocument.Parse(uploadBody);
        Guid pluginId = Guid.Parse(uploadDoc.RootElement.GetProperty("pluginId").GetString()!);

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/plugins/{pluginId}/versions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement versionEntry = doc.RootElement.GetProperty("data")[0];

        // Assert — required fields per spec: versionNumber, releasedAt, releaseNotes, downloadCount, isLatest
        Assert.True(versionEntry.TryGetProperty("version", out _),
            "Version entry must have 'version'");
        Assert.True(versionEntry.TryGetProperty("releasedAt", out _),
            "Version entry must have 'releasedAt'");
        Assert.True(versionEntry.TryGetProperty("releaseNotes", out _),
            "Version entry must have 'releaseNotes'");
        Assert.True(versionEntry.TryGetProperty("downloadCount", out _),
            "Version entry must have 'downloadCount'");
        Assert.True(versionEntry.TryGetProperty("isLatest", out _),
            "Version entry must have 'isLatest'");
    }

    // =========================================================================
    // GET /api/v1/plugins/{pluginId}/versions/{version} — single version detail
    // =========================================================================

    [Fact]
    public async Task GetVersion_ExistingVersion_Returns200WithVersionDetail()
    {
        // Arrange
        using MultipartFormDataContent uploadForm = BuildUploadForm(
            name: "GetVersionPlugin",
            initialVersion: "1.2.3",
            releaseNotes: "Specific release notes",
            archiveStream: BuildValidPluginTarGz(name: "getversionplugin", version: "1.2.3"));
        HttpResponseMessage uploadResp = await _client.PostAsync("/api/v1/plugins/upload", uploadForm);
        Assert.Equal(HttpStatusCode.Created, uploadResp.StatusCode);

        string uploadBody = await uploadResp.Content.ReadAsStringAsync();
        using JsonDocument uploadDoc = JsonDocument.Parse(uploadBody);
        Guid pluginId = Guid.Parse(uploadDoc.RootElement.GetProperty("pluginId").GetString()!);

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/plugins/{pluginId}/versions/1.2.3");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        Assert.Equal("1.2.3", root.GetProperty("version").GetString());
        Assert.True(root.GetProperty("isLatest").GetBoolean());
        Assert.True(root.TryGetProperty("releasedAt", out _));
        Assert.True(root.TryGetProperty("releaseNotes", out _));
        Assert.True(root.TryGetProperty("downloadCount", out _));
    }

    [Fact]
    public async Task GetVersion_NonExistentVersion_Returns404WithSpecExactDetail()
    {
        // Arrange
        using MultipartFormDataContent uploadForm = BuildUploadForm(
            name: "NotFoundVersionPlugin",
            initialVersion: "1.0.0",
            archiveStream: BuildValidPluginTarGz(name: "notfoundversionplugin", version: "1.0.0"));
        HttpResponseMessage uploadResp = await _client.PostAsync("/api/v1/plugins/upload", uploadForm);
        Assert.Equal(HttpStatusCode.Created, uploadResp.StatusCode);

        string uploadBody = await uploadResp.Content.ReadAsStringAsync();
        using JsonDocument uploadDoc = JsonDocument.Parse(uploadBody);
        Guid pluginId = Guid.Parse(uploadDoc.RootElement.GetProperty("pluginId").GetString()!);

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/plugins/{pluginId}/versions/9.9.9");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement detail = doc.RootElement.GetProperty("detail");

        // Spec: "Version not found" (plugin-versioning/spec.md)
        Assert.Equal("Version not found", detail.GetString());
    }

    [Fact]
    public async Task GetVersion_UnknownPlugin_Returns404()
    {
        // Arrange
        Guid unknownId = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/plugins/{unknownId}/versions/1.0.0");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // =========================================================================
    // PATCH /api/v1/plugins/{pluginId}/versions/{version} → 405 Method Not Allowed
    // Spec: versions are immutable once published
    // =========================================================================

    [Fact]
    public async Task PatchVersion_AnyVersion_Returns405MethodNotAllowed()
    {
        // Arrange
        using MultipartFormDataContent uploadForm = BuildUploadForm(
            name: "ImmutableVersionPlugin",
            initialVersion: "1.2.0",
            archiveStream: BuildValidPluginTarGz(name: "immutableversionplugin", version: "1.2.0"));
        HttpResponseMessage uploadResp = await _client.PostAsync("/api/v1/plugins/upload", uploadForm);
        Assert.Equal(HttpStatusCode.Created, uploadResp.StatusCode);

        string uploadBody = await uploadResp.Content.ReadAsStringAsync();
        using JsonDocument uploadDoc = JsonDocument.Parse(uploadBody);
        Guid pluginId = Guid.Parse(uploadDoc.RootElement.GetProperty("pluginId").GetString()!);

        // Act — PATCH is not allowed (versions are immutable)
        using StringContent patchBody = new(
            """{"releaseNotes": "Attempted update"}""",
            System.Text.Encoding.UTF8,
            "application/json");
        HttpResponseMessage response = await _client.PatchAsync(
            $"/api/v1/plugins/{pluginId}/versions/1.2.0", patchBody);

        // Assert
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task PatchVersion_UnknownPlugin_Returns405MethodNotAllowed()
    {
        // PATCH must return 405 regardless of whether the plugin/version exists
        using StringContent patchBody = new(
            """{"releaseNotes": "test"}""",
            System.Text.Encoding.UTF8,
            "application/json");
        HttpResponseMessage response = await _client.PatchAsync(
            $"/api/v1/plugins/{Guid.NewGuid()}/versions/1.0.0", patchBody);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    // =========================================================================
    // ProblemDetails envelope shape for all error responses
    // =========================================================================

    [Fact]
    public async Task ErrorResponse_AlwaysHasRfc7807Shape()
    {
        // Arrange — trigger a 400 (missing package)
        using MultipartFormDataContent form = BuildUploadForm(includePackage: false);

        // Act
        HttpResponseMessage response = await _client.PostAsync("/api/v1/plugins/upload", form);

        // Assert — RFC 7807 fields
        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        Assert.True(root.TryGetProperty("detail", out _),
            "Error response must have RFC 7807 'detail' property");
    }
}
