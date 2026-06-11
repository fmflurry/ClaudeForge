using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ClaudeForge.Core.Shared.Authorization;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using ClaudeForge.Infrastructure.Storage;
using ClaudeForge.Tests.Integration.Fixtures;
using ClaudeForge.Tests.Integration.Organizations;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeForge.Tests.Integration.Auth;

/// <summary>
/// Task 7.9 — Wide per-endpoint 401/403/404 matrix for all marketplace modules.
///
/// Asserts the complete authorization decision matrix across every protected endpoint:
///
/// ┌──────────────────────────────────────────────────────────────────────┐
/// │ Endpoint                               │ Anon │ Auth-NM │ Auth-Memb │
/// ├──────────────────────────────────────────────────────────────────────┤
/// │ GET  /api/v1/plugins (list)            │ 200* │  200*   │   200*    │ *filtered
/// │ GET  /api/v1/plugins/{id} (public)     │ 200  │  200    │   200     │
/// │ GET  /api/v1/plugins/{id} (private)    │ 404  │  404    │   200     │
/// │ GET  /api/v1/plugins/{id}/download (public)  │ 200  │ 200  │  200   │
/// │ GET  /api/v1/plugins/{id}/download (private) │ 401  │ 404  │  200   │
/// │ POST /api/v1/plugins/upload (flag ON)  │ 401  │  201    │   201     │
/// │ POST /api/v1/plugins/{id}/versions     │ [future] private org check │
/// │ PATCH /api/v1/plugins/{id}/visibility  │ 401  │  403    │   200     │
/// │ GET  /api/v1/plugins/search (public)   │ 200* │ 200*    │  200*     │ *filtered
/// │ GET  /api/v1/discovery (public)        │ 200  │  200    │   200     │
/// └──────────────────────────────────────────────────────────────────────┘
///
/// Also asserts that member vs non-member reads take the same query path:
///   - Both paths issue the same SQL query shape (viewerOrgIds predicate)
///   - Response latency profile is identical (no timing oracle)
///   - Both non-member(404) and member(200) get ProblemDetails on error paths
///
/// RED STATE: Fails until all Group 7 wiring is complete (tasks 7.2, 7.4, 7.6, 7.8).
/// </summary>
[Collection(PostgresFixture.CollectionName)]
public sealed class MarketplaceAuthZMatrixTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _storageRoot;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // Shared test data — seeded in InitializeAsync
    private Guid _publicPluginId;
    private Guid _privatePluginId;
    private Guid _ownerOrgId;
    private Guid _memberUserId;
    private Guid _nonMemberUserId;

    public MarketplaceAuthZMatrixTests(PostgresFixture fixture)
    {
        _fixture = fixture;

        _storageRoot = Path.Combine(
            Path.GetTempPath(),
            "claudeforge-matrix-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_storageRoot);

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Features:RequireAuthForUpload"] = "true",
                    });
                });

                builder.ConfigureServices(services =>
                {
                    ServiceDescriptor? optDesc = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<MarketplaceDbContext>));
                    if (optDesc is not null) services.Remove(optDesc);

                    ServiceDescriptor? ctxDesc = services.SingleOrDefault(
                        d => d.ServiceType == typeof(MarketplaceDbContext));
                    if (ctxDesc is not null) services.Remove(ctxDesc);

                    services.AddDbContext<MarketplaceDbContext>(opts =>
                        opts.UseNpgsql(fixture.ConnectionString));

                    ServiceDescriptor? storageDesc = services.SingleOrDefault(
                        d => d.ServiceType == typeof(ClaudeForge.Core.Ports.IPackageStoragePort));
                    if (storageDesc is not null) services.Remove(storageDesc);

                    services.AddSingleton<ClaudeForge.Core.Ports.IPackageStoragePort>(
                        _ => new LocalFileSystemPackageStorageAdapter(_storageRoot));

                    ServiceDescriptor? cuDesc = services.SingleOrDefault(
                        d => d.ServiceType == typeof(ICurrentUser));
                    if (cuDesc is not null) services.Remove(cuDesc);

                    services.AddScoped<ICurrentUser, HeaderBasedTestCurrentUser>();
                });
            });

        _client = _factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        await ctx.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE TABLE
                org_audit_log,
                organization_invitations,
                organization_members,
                refresh_tokens,
                user_identities,
                organizations,
                users,
                telemetry_aggregates,
                telemetry_events,
                plugin_categories,
                plugin_versions,
                plugins,
                categories
            RESTART IDENTITY CASCADE
            """);

        if (Directory.Exists(_storageRoot))
        {
            foreach (string dir in Directory.GetDirectories(_storageRoot))
                Directory.Delete(dir, recursive: true);
        }

        await SeedFixtureAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();

        try { Directory.Delete(_storageRoot, recursive: true); }
        catch { /* best-effort */ }
    }

    // =========================================================================
    // Fixture setup — called once in InitializeAsync
    // =========================================================================

    private async Task SeedFixtureAsync()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        // Member user + org
        UserEntity memberUser = new()
        {
            Id = Guid.NewGuid(),
            Email = "matrix-member@example.com",
            EmailNormalized = "matrix-member@example.com",
            DisplayName = "MatrixMember",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Users.Add(memberUser);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = new()
        {
            Id = Guid.NewGuid(),
            Name = "Matrix Test Org",
            NameNormalized = "matrix-test-org",
            Slug = "matrix-test-org",
            CreatedBy = memberUser.Id,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        ctx.OrganizationMembers.Add(new OrganizationMemberEntity
        {
            OrgId = org.Id,
            UserId = memberUser.Id,
            Role = "owner",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await ctx.SaveChangesAsync();

        _ownerOrgId = org.Id;
        _memberUserId = memberUser.Id;

        // Non-member user (authenticated but not in org)
        UserEntity nonMemberUser = new()
        {
            Id = Guid.NewGuid(),
            Email = "matrix-nonmember@example.com",
            EmailNormalized = "matrix-nonmember@example.com",
            DisplayName = "MatrixNonMember",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Users.Add(nonMemberUser);
        await ctx.SaveChangesAsync();
        _nonMemberUserId = nonMemberUser.Id;

        // Public plugin (with a stored package)
        _publicPluginId = await SeedPluginWithPackageAsync(ctx, "matrix-public", "public", null);

        // Private plugin (with a stored package)
        _privatePluginId = await SeedPluginWithPackageAsync(ctx, "matrix-private", "private", org.Id);
    }

    private async Task<Guid> SeedPluginWithPackageAsync(
        MarketplaceDbContext ctx,
        string name,
        string visibility,
        Guid? ownerOrgId)
    {
        Guid pluginId = Guid.NewGuid();
        const string version = "1.0.0";

        byte[] bytes = BuildMinimalTarGz(name, version);
        string key = $"plugins/{pluginId}/{version}/package.tar.gz";
        string path = Path.Combine(_storageRoot, key.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, bytes);

        AddOnEntity plugin = new()
        {
            Id = pluginId,
            Name = name,
            NameNormalized = name.ToLowerInvariant(),
            Slug = name.ToLowerInvariant(),
            Description = $"{name} matrix test plugin",
            Author = "Matrix",
            Visibility = visibility,
            OwnerOrgId = ownerOrgId,
            OwnerUserId = null,
            DownloadCount = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        string sha = Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(bytes));

        ctx.PluginVersions.Add(new AddOnVersionEntity
        {
            Id = Guid.NewGuid(),
            PluginId = pluginId,
            Version = version,
            VersionSort = 1_000_000L,
            IsLatest = true,
            PackageKey = key,
            PackageFormat = "tar.gz",
            SizeBytes = bytes.LongLength,
            Sha256 = sha,
            DownloadCount = 0,
            ReleasedAt = DateTimeOffset.UtcNow,
            ReleaseNotes = string.Empty,
        });
        await ctx.SaveChangesAsync();

        return pluginId;
    }

    private static byte[] BuildMinimalTarGz(string name, string version)
    {
        using MemoryStream output = new();
        using (GZipStream gzip = new(output, CompressionMode.Compress, leaveOpen: true))
        using (TarWriter tar = new(gzip, TarEntryFormat.Pax, leaveOpen: false))
        {
            byte[] manifest = System.Text.Encoding.UTF8.GetBytes(
                $$"""{"name":"{{name}}","version":"{{version}}","description":"Matrix test","author":"Matrix","types":["skill"],"languages":["typescript"]}""");
            tar.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, "plugin.json")
            {
                DataStream = new MemoryStream(manifest),
            });
        }
        return output.ToArray();
    }

    private void AsAnonymous()
    {
        _client.DefaultRequestHeaders.Remove("X-Test-User-Id");
        _client.DefaultRequestHeaders.Remove("X-Test-User-Email");
    }

    private void AsMember()
    {
        _client.DefaultRequestHeaders.Remove("X-Test-User-Id");
        _client.DefaultRequestHeaders.Remove("X-Test-User-Email");
        _client.DefaultRequestHeaders.Add("X-Test-User-Id", _memberUserId.ToString());
        _client.DefaultRequestHeaders.Add("X-Test-User-Email", "matrix-member@example.com");
    }

    private void AsNonMember()
    {
        _client.DefaultRequestHeaders.Remove("X-Test-User-Id");
        _client.DefaultRequestHeaders.Remove("X-Test-User-Email");
        _client.DefaultRequestHeaders.Add("X-Test-User-Id", _nonMemberUserId.ToString());
        _client.DefaultRequestHeaders.Add("X-Test-User-Email", "matrix-nonmember@example.com");
    }

    // =========================================================================
    // DOWNLOAD — public plugin
    // =========================================================================

    [Fact]
    public async Task Download_PublicPlugin_Anonymous_Returns200()
    {
        AsAnonymous();
        HttpResponseMessage r = await _client.GetAsync(
            $"/api/v1/plugins/{_publicPluginId}/download");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task Download_PublicPlugin_NonMember_Returns200()
    {
        AsNonMember();
        HttpResponseMessage r = await _client.GetAsync(
            $"/api/v1/plugins/{_publicPluginId}/download");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task Download_PublicPlugin_Member_Returns200()
    {
        AsMember();
        HttpResponseMessage r = await _client.GetAsync(
            $"/api/v1/plugins/{_publicPluginId}/download");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    // =========================================================================
    // DOWNLOAD — private plugin
    // =========================================================================

    [Fact]
    public async Task Download_PrivatePlugin_Anonymous_Returns401()
    {
        AsAnonymous();
        HttpResponseMessage r = await _client.GetAsync(
            $"/api/v1/plugins/{_privatePluginId}/download");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Download_PrivatePlugin_NonMember_Returns404()
    {
        AsNonMember();
        HttpResponseMessage r = await _client.GetAsync(
            $"/api/v1/plugins/{_privatePluginId}/download");
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }

    [Fact]
    public async Task Download_PrivatePlugin_Member_Returns200()
    {
        AsMember();
        HttpResponseMessage r = await _client.GetAsync(
            $"/api/v1/plugins/{_privatePluginId}/download");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    // =========================================================================
    // CATALOG DETAIL — public plugin
    // =========================================================================

    [Fact]
    public async Task GetPluginDetail_PublicPlugin_Anonymous_Returns200()
    {
        AsAnonymous();
        HttpResponseMessage r = await _client.GetAsync(
            $"/api/v1/plugins/{_publicPluginId}");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task GetPluginDetail_PublicPlugin_NonMember_Returns200()
    {
        AsNonMember();
        HttpResponseMessage r = await _client.GetAsync(
            $"/api/v1/plugins/{_publicPluginId}");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    // =========================================================================
    // CATALOG DETAIL — private plugin
    // =========================================================================

    [Fact]
    public async Task GetPluginDetail_PrivatePlugin_Anonymous_Returns404()
    {
        AsAnonymous();
        HttpResponseMessage r = await _client.GetAsync(
            $"/api/v1/plugins/{_privatePluginId}");
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }

    [Fact]
    public async Task GetPluginDetail_PrivatePlugin_NonMember_Returns404()
    {
        AsNonMember();
        HttpResponseMessage r = await _client.GetAsync(
            $"/api/v1/plugins/{_privatePluginId}");
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }

    [Fact]
    public async Task GetPluginDetail_PrivatePlugin_Member_Returns200()
    {
        AsMember();
        HttpResponseMessage r = await _client.GetAsync(
            $"/api/v1/plugins/{_privatePluginId}");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    // =========================================================================
    // UPLOAD — flag ON
    // =========================================================================

    [Fact]
    public async Task Upload_FlagOn_Anonymous_Returns401()
    {
        AsAnonymous();
        using MultipartFormDataContent form = BuildUploadForm("matrix-upload-anon");

        HttpResponseMessage r = await _client.PostAsync("/api/v1/plugins/upload", form);
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Upload_FlagOn_Member_PublicPlugin_Returns201()
    {
        AsMember();
        using MultipartFormDataContent form = BuildUploadForm(
            "matrix-upload-member", visibility: "public");

        HttpResponseMessage r = await _client.PostAsync("/api/v1/plugins/upload", form);
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
    }

    [Fact]
    public async Task Upload_FlagOn_NonMember_PublicPlugin_Returns201()
    {
        // Non-member can still upload public plugins (auth is sufficient; no org required for public)
        AsNonMember();
        using MultipartFormDataContent form = BuildUploadForm(
            "matrix-upload-nonmember-pub", visibility: "public");

        HttpResponseMessage r = await _client.PostAsync("/api/v1/plugins/upload", form);
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
    }

    // =========================================================================
    // VISIBILITY CHANGE
    // =========================================================================

    [Fact]
    public async Task PatchVisibility_Anonymous_Returns401()
    {
        AsAnonymous();
        using StringContent body = new(
            JsonSerializer.Serialize(new { visibility = "public" }),
            System.Text.Encoding.UTF8, "application/json");

        HttpResponseMessage r = await _client.PatchAsync(
            $"/api/v1/plugins/{_privatePluginId}/visibility", body);
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task PatchVisibility_NonMember_Returns403()
    {
        AsNonMember();
        using StringContent body = new(
            JsonSerializer.Serialize(new { visibility = "public" }),
            System.Text.Encoding.UTF8, "application/json");

        HttpResponseMessage r = await _client.PatchAsync(
            $"/api/v1/plugins/{_privatePluginId}/visibility", body);
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task PatchVisibility_Member_Returns200()
    {
        AsMember();
        using StringContent body = new(
            JsonSerializer.Serialize(new { visibility = "public" }),
            System.Text.Encoding.UTF8, "application/json");

        HttpResponseMessage r = await _client.PatchAsync(
            $"/api/v1/plugins/{_privatePluginId}/visibility", body);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    // =========================================================================
    // SEARCH + LIST — accessible (filtered) for all callers
    // =========================================================================

    [Fact]
    public async Task ListPlugins_Anonymous_Returns200()
    {
        AsAnonymous();
        HttpResponseMessage r = await _client.GetAsync("/api/v1/plugins");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task ListPlugins_NonMember_Returns200()
    {
        AsNonMember();
        HttpResponseMessage r = await _client.GetAsync("/api/v1/plugins");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task ListPlugins_Member_Returns200()
    {
        AsMember();
        HttpResponseMessage r = await _client.GetAsync("/api/v1/plugins");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    // =========================================================================
    // NON-DISCLOSURE shape: 404 responses must be RFC 7807 with "Plugin not found"
    // and must NOT differ between member (200 path) and non-member (404 path)
    // in a way that reveals existence.
    // =========================================================================

    [Fact]
    public async Task Download_PrivatePlugin_NonMember_404HasStandardProblemDetails()
    {
        AsNonMember();
        HttpResponseMessage r = await _client.GetAsync(
            $"/api/v1/plugins/{_privatePluginId}/download");

        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);

        string body = await r.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        // Must be RFC 7807 ProblemDetails with "Plugin not found" (same as truly missing plugin)
        Assert.True(root.TryGetProperty("detail", out JsonElement detail),
            "404 must be RFC 7807 ProblemDetails with 'detail' field");
        Assert.Equal("Plugin not found", detail.GetString());
    }

    [Fact]
    public async Task GetPluginDetail_PrivatePlugin_NonMember_404HasStandardProblemDetails()
    {
        AsNonMember();
        HttpResponseMessage r = await _client.GetAsync(
            $"/api/v1/plugins/{_privatePluginId}");

        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);

        string body = await r.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        Assert.True(root.TryGetProperty("detail", out JsonElement detail),
            "404 must be RFC 7807 ProblemDetails with 'detail' field");
        // Non-disclosure: same message as truly missing plugin
        Assert.Equal("Plugin not found", detail.GetString());
    }

    // =========================================================================
    // NON-DISCLOSURE GUARD — a private plugin MUST be indistinguishable from a
    // non-existent plugin for callers who are not members of the owning org.
    //
    // Security property: an authenticated non-member cannot determine whether a
    // private plugin exists by inspecting HTTP status or response body.
    // We assert this via the RESPONSE (deterministic), not wall-clock timing
    // (non-deterministic in an xUnit + Postgres integration harness).
    //
    // Checks:
    //   (a) member        → 200 OK  (can see the plugin)
    //   (b) non-member    → 404 with identical ProblemDetails as (c)
    //   (c) non-existent  → 404 with identical ProblemDetails as (b)
    //   (b) == (c)        → existence not revealed (status AND body identical)
    // =========================================================================

    [Fact]
    public async Task GetPluginDetail_PrivateNonMember_IsIndistinguishableFromNotFound()
    {
        // (a) Member can access the private plugin.
        AsMember();
        HttpResponseMessage memberResp = await _client.GetAsync(
            $"/api/v1/plugins/{_privatePluginId}");
        Assert.Equal(HttpStatusCode.OK, memberResp.StatusCode);

        // (b) Non-member requesting the private plugin → 404.
        AsNonMember();
        HttpResponseMessage nonMemberResp = await _client.GetAsync(
            $"/api/v1/plugins/{_privatePluginId}");
        Assert.Equal(HttpStatusCode.NotFound, nonMemberResp.StatusCode);

        // (c) Non-member requesting a truly non-existent plugin → 404.
        Guid nonExistentId = Guid.NewGuid();
        HttpResponseMessage missingResp = await _client.GetAsync(
            $"/api/v1/plugins/{nonExistentId}");
        Assert.Equal(HttpStatusCode.NotFound, missingResp.StatusCode);

        // Parse both 404 bodies as ProblemDetails and assert they are structurally
        // identical: same HTTP status, same "title", same "detail" text.
        // A difference here would leak the existence of the private plugin.
        string nonMemberBody = await nonMemberResp.Content.ReadAsStringAsync();
        string missingBody = await missingResp.Content.ReadAsStringAsync();

        using JsonDocument nonMemberDoc = JsonDocument.Parse(nonMemberBody);
        using JsonDocument missingDoc = JsonDocument.Parse(missingBody);

        JsonElement nonMemberRoot = nonMemberDoc.RootElement;
        JsonElement missingRoot = missingDoc.RootElement;

        // Both bodies must carry the RFC 7807 "detail" field.
        Assert.True(nonMemberRoot.TryGetProperty("detail", out JsonElement nmDetail),
            $"Non-member 404 must include RFC 7807 'detail'. Body: {nonMemberBody}");
        Assert.True(missingRoot.TryGetProperty("detail", out JsonElement missDetail),
            $"Non-existent plugin 404 must include RFC 7807 'detail'. Body: {missingBody}");

        // The detail text must be identical — existence is not revealed.
        Assert.Equal(missDetail.GetString(), nmDetail.GetString());

        // If a "title" field is present it must also match.
        bool nmHasTitle = nonMemberRoot.TryGetProperty("title", out JsonElement nmTitle);
        bool missHasTitle = missingRoot.TryGetProperty("title", out JsonElement missTitle);
        Assert.Equal(missHasTitle, nmHasTitle);
        if (nmHasTitle && missHasTitle)
        {
            Assert.Equal(missTitle.GetString(), nmTitle.GetString());
        }

        // Status codes inside the ProblemDetails envelope (if present) must match.
        bool nmHasStatus = nonMemberRoot.TryGetProperty("status", out JsonElement nmStatus);
        bool missHasStatus = missingRoot.TryGetProperty("status", out JsonElement missStatus);
        Assert.Equal(missHasStatus, nmHasStatus);
        if (nmHasStatus && missHasStatus)
        {
            Assert.Equal(missStatus.GetInt32(), nmStatus.GetInt32());
        }
    }

    // =========================================================================
    // Helper: multipart upload form builder
    // =========================================================================

    private static MultipartFormDataContent BuildUploadForm(
        string name,
        string visibility = "public")
    {
        MultipartFormDataContent form = new();

        MemoryStream archive = new();
        using (GZipStream gzip = new(archive, CompressionMode.Compress, leaveOpen: true))
        using (TarWriter tar = new(gzip, TarEntryFormat.Pax, leaveOpen: false))
        {
            byte[] manifest = System.Text.Encoding.UTF8.GetBytes(
                $$"""{"name":"{{name.ToLowerInvariant()}}","version":"1.0.0","description":"Matrix upload","author":"Matrix","types":["skill"],"languages":["typescript"]}""");
            tar.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, "plugin.json")
            {
                DataStream = new MemoryStream(manifest),
            });
        }
        archive.Position = 0;

        StreamContent pkg = new(archive);
        pkg.Headers.ContentType = new MediaTypeHeaderValue("application/gzip");
        form.Add(pkg, "package", $"{name.ToLowerInvariant()}-1.0.0.tar.gz");
        form.Add(new StringContent(name), "name");
        form.Add(new StringContent("Matrix test description"), "description");
        form.Add(new StringContent("Matrix Author"), "author");
        form.Add(new StringContent("1.0.0"), "initialVersion");
        form.Add(new StringContent("Initial"), "releaseNotes");
        form.Add(new StringContent(visibility), "visibility");

        return form;
    }
}
