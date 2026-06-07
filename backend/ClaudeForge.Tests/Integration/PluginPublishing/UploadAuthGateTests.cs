using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ClaudeForge.Core.Shared.Authorization;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using ClaudeForge.Tests.Integration.Fixtures;
using ClaudeForge.Tests.Integration.Organizations;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Formats.Tar;
using System.IO.Compression;

namespace ClaudeForge.Tests.Integration.PluginPublishing;

/// <summary>
/// Task 7.3 — Upload gate behind Features:RequireAuthForUpload.
/// Task 7.5 — Private publish + visibility change.
///
/// TASK 7.3 scenarios:
///   flag OFF (default/false) → anonymous upload → 201 (backward-compat Phase 2)
///   flag ON  → unauthenticated upload         → 401
///   flag ON  → authenticated upload (public)  → 201; owner_user_id set from ICurrentUser
///   default visibility is "public" when not specified
///
/// TASK 7.5 scenarios:
///   member publishes private for own org → 201 + owner_org_id set
///   non-member publishes private for org → 403 (write rule)
///   private upload without org           → 400 (private requires ownerOrgId)
///   owner/admin/publisher changes visibility org→public → 200 + owner_org_id cleared
///   owner changes visibility public→private for org → 200 + owner_org_id set
///   non-owner visibility change          → 403
///   unauthenticated visibility change    → 401
///
/// RED STATE: These tests will fail until:
///   - UploadPluginUseCase gains ICurrentUser + IOrgMembershipQueryPort + IPluginAccessPolicy
///   - POST /api/v1/plugins/upload gains [Authorize] gate behind Features:RequireAuthForUpload
///   - UploadPluginCommand gains Visibility + OwnerOrgId + OwnerUserId fields
///   - ChangePluginVisibilityUseCase + PATCH /api/v1/plugins/{pluginId}/visibility endpoint exist
///   - IPluginPublishingRepositoryPort gains UpdateVisibilityAsync(pluginId, visibility, ownerOrgId)
/// </summary>
[Collection(PostgresFixture.CollectionName)]
public sealed class UploadAuthGateTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public UploadAuthGateTests(PostgresFixture fixture)
    {
        _fixture = fixture;

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    // Flag ON by default for most tests; individual tests may reset.
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

                    // Replace ICurrentUser with header-based test stub
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
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    // =========================================================================
    // Archive + form helpers (copied from PluginPublishingHttpTests pattern)
    // =========================================================================

    private static MemoryStream BuildValidTarGz(string name = "test-plugin", string version = "1.0.0")
    {
        MemoryStream output = new();
        using (GZipStream gzip = new(output, CompressionMode.Compress, leaveOpen: true))
        using (TarWriter tar = new(gzip, TarEntryFormat.Pax, leaveOpen: false))
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(
                $$"""{"name":"{{name}}","version":"{{version}}","description":"Desc","author":"Author","types":["skill"],"languages":["typescript"]}""");
            PaxTarEntry entry = new(TarEntryType.RegularFile, "plugin.json")
            {
                DataStream = new MemoryStream(bytes),
            };
            tar.WriteEntry(entry);
        }
        output.Position = 0;
        return output;
    }

    private static MultipartFormDataContent BuildUploadForm(
        string name = "TestPlugin",
        string version = "1.0.0",
        string? visibility = null,
        string? ownerOrgId = null,
        bool includePackage = true)
    {
        MultipartFormDataContent form = new();

        if (includePackage)
        {
            MemoryStream archive = BuildValidTarGz(name.ToLowerInvariant(), version);
            StreamContent packageContent = new(archive);
            packageContent.Headers.ContentType = new MediaTypeHeaderValue("application/gzip");
            form.Add(packageContent, "package", $"{name.ToLowerInvariant()}-{version}.tar.gz");
        }

        form.Add(new StringContent(name), "name");
        form.Add(new StringContent("Test description"), "description");
        form.Add(new StringContent("Test Author"), "author");
        form.Add(new StringContent(version), "initialVersion");
        form.Add(new StringContent("Initial release"), "releaseNotes");

        // Visibility and ownerOrgId are NEW fields required by the wiring implementation
        if (visibility is not null)
            form.Add(new StringContent(visibility), "visibility");
        if (ownerOrgId is not null)
            form.Add(new StringContent(ownerOrgId), "ownerOrgId");

        return form;
    }

    private void SetAuthUser(Guid userId, string email)
    {
        _client.DefaultRequestHeaders.Remove("X-Test-User-Id");
        _client.DefaultRequestHeaders.Remove("X-Test-User-Email");
        _client.DefaultRequestHeaders.Add("X-Test-User-Id", userId.ToString());
        _client.DefaultRequestHeaders.Add("X-Test-User-Email", email);
    }

    private void ClearAuthUser()
    {
        _client.DefaultRequestHeaders.Remove("X-Test-User-Id");
        _client.DefaultRequestHeaders.Remove("X-Test-User-Email");
    }

    private async Task<(Guid userId, Guid orgId)> SeedUserAndOrgAsync(
        string email, string orgName, string role = "member")
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        UserEntity user = new()
        {
            Id = Guid.NewGuid(),
            Email = email,
            EmailNormalized = email.ToLowerInvariant(),
            DisplayName = email.Split('@')[0],
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = new()
        {
            Id = Guid.NewGuid(),
            Name = orgName,
            NameNormalized = orgName.ToLowerInvariant(),
            Slug = orgName.ToLowerInvariant().Replace(' ', '-'),
            CreatedBy = user.Id,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        ctx.OrganizationMembers.Add(new OrganizationMemberEntity
        {
            OrgId = org.Id,
            UserId = user.Id,
            Role = role,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await ctx.SaveChangesAsync();

        return (user.Id, org.Id);
    }

    // =========================================================================
    // Task 7.3 — flag ON: unauthenticated upload → 401
    // =========================================================================

    [Fact]
    public async Task PostUpload_FlagOn_Unauthenticated_Returns401()
    {
        // Arrange — flag is ON (set in factory config above); no auth headers
        ClearAuthUser();

        using MultipartFormDataContent form = BuildUploadForm(name: "AnonUploadPlugin");

        // Act
        HttpResponseMessage response = await _client.PostAsync("/api/v1/plugins/upload", form);

        // Assert — [Authorize] gate must reject anonymous requests with 401
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // =========================================================================
    // Task 7.3 — flag ON: authenticated public upload → 201
    // =========================================================================

    [Fact]
    public async Task PostUpload_FlagOn_AuthenticatedPublicUpload_Returns201()
    {
        // Arrange — flag ON; authenticated user
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity user = new()
        {
            Id = Guid.NewGuid(),
            Email = "authenticated-uploader@example.com",
            EmailNormalized = "authenticated-uploader@example.com",
            DisplayName = "AuthUploader",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        SetAuthUser(user.Id, user.Email);

        using MultipartFormDataContent form = BuildUploadForm(
            name: "AuthPublicPlugin",
            visibility: "public");

        // Act
        HttpResponseMessage response = await _client.PostAsync("/api/v1/plugins/upload", form);

        // Assert — authenticated public upload must succeed
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("pluginId", out JsonElement pluginIdEl));
        Guid pluginId = Guid.Parse(pluginIdEl.GetString()!);

        // Verify owner_user_id is set from ICurrentUser
        PluginEntity? plugin = await ctx.Plugins.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pluginId);
        Assert.NotNull(plugin);
        Assert.Equal(user.Id, plugin.OwnerUserId);
        Assert.Equal("public", plugin.Visibility);
    }

    // =========================================================================
    // Task 7.3 — default visibility is "public" when not specified
    // =========================================================================

    [Fact]
    public async Task PostUpload_FlagOn_NoVisibilitySpecified_DefaultsToPublic()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity user = new()
        {
            Id = Guid.NewGuid(),
            Email = "default-vis-uploader@example.com",
            EmailNormalized = "default-vis-uploader@example.com",
            DisplayName = "DefaultVisUser",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        SetAuthUser(user.Id, user.Email);

        // No "visibility" field in form — should default to "public"
        using MultipartFormDataContent form = BuildUploadForm(
            name: "DefaultVisPlugin",
            visibility: null);

        // Act
        HttpResponseMessage response = await _client.PostAsync("/api/v1/plugins/upload", form);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        Guid pluginId = Guid.Parse(doc.RootElement.GetProperty("pluginId").GetString()!);

        PluginEntity? plugin = await ctx.Plugins.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pluginId);
        Assert.NotNull(plugin);
        Assert.Equal("public", plugin.Visibility);
    }

    // =========================================================================
    // Task 7.3 — flag OFF: anonymous upload still works (Phase 2 backward-compat)
    // Uses a SEPARATE factory with flag=false to override the default flag=true factory.
    // =========================================================================

    [Fact]
    public async Task PostUpload_FlagOff_AnonymousUpload_Returns201()
    {
        // Arrange — separate factory with flag OFF
        WebApplicationFactory<Program> factoryFlagOff = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Features:RequireAuthForUpload"] = "false",
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
                        opts.UseNpgsql(_fixture.ConnectionString));

                    ServiceDescriptor? cuDesc = services.SingleOrDefault(
                        d => d.ServiceType == typeof(ICurrentUser));
                    if (cuDesc is not null) services.Remove(cuDesc);

                    services.AddScoped<ICurrentUser, HeaderBasedTestCurrentUser>();
                });
            });

        await using (factoryFlagOff)
        {
            HttpClient clientFlagOff = factoryFlagOff.CreateClient();
            // No auth headers — anonymous
            using MultipartFormDataContent form = BuildUploadForm(name: "FlagOffAnonPlugin");

            // Act
            HttpResponseMessage response = await clientFlagOff.PostAsync(
                "/api/v1/plugins/upload", form);

            // Assert — flag OFF means anonymous upload is still allowed (201)
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
    }

    // =========================================================================
    // Task 7.5 — member publishes private for own org → 201 + owner_org_id set
    // =========================================================================

    [Fact]
    public async Task PostUpload_FlagOn_MemberPublishesPrivateForOwnOrg_Returns201WithOwnerOrgId()
    {
        // Arrange
        (Guid userId, Guid orgId) = await SeedUserAndOrgAsync(
            "private-publisher@example.com", "Publisher Org", "owner");

        SetAuthUser(userId, "private-publisher@example.com");

        using MultipartFormDataContent form = BuildUploadForm(
            name: "PrivateOrgPlugin",
            visibility: "private",
            ownerOrgId: orgId.ToString());

        // Act
        HttpResponseMessage response = await _client.PostAsync("/api/v1/plugins/upload", form);

        // Assert — member publishing private for own org → 201
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        Guid pluginId = Guid.Parse(doc.RootElement.GetProperty("pluginId").GetString()!);

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        PluginEntity? plugin = await ctx.Plugins.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pluginId);
        Assert.NotNull(plugin);
        Assert.Equal("private", plugin.Visibility);
        Assert.Equal(orgId, plugin.OwnerOrgId);  // owner_org_id must be persisted
        Assert.Equal(userId, plugin.OwnerUserId);
    }

    // =========================================================================
    // Task 7.5 — non-member publishes private for org → 403
    // =========================================================================

    [Fact]
    public async Task PostUpload_FlagOn_NonMemberPublishesPrivateForOrg_Returns403()
    {
        // Arrange — org created by ownerUser; uploader is NOT a member
        (_, Guid orgId) = await SeedUserAndOrgAsync("nonmember-org-owner@example.com", "NM Org");

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity outsider = new()
        {
            Id = Guid.NewGuid(),
            Email = "outsider-publisher@example.com",
            EmailNormalized = "outsider-publisher@example.com",
            DisplayName = "Outsider",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Users.Add(outsider);
        await ctx.SaveChangesAsync();

        SetAuthUser(outsider.Id, outsider.Email);

        using MultipartFormDataContent form = BuildUploadForm(
            name: "NMPrivatePlugin",
            visibility: "private",
            ownerOrgId: orgId.ToString());

        // Act
        HttpResponseMessage response = await _client.PostAsync("/api/v1/plugins/upload", form);

        // Assert — non-member attempting private publish for an org → 403 (write rule)
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // =========================================================================
    // Task 7.5 — private upload without org → 400
    // (design rule: private requires ownerOrgId)
    // =========================================================================

    [Fact]
    public async Task PostUpload_FlagOn_PrivateWithoutOrg_Returns400()
    {
        // Arrange — authenticated user, private visibility, NO ownerOrgId supplied
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity user = new()
        {
            Id = Guid.NewGuid(),
            Email = "private-no-org@example.com",
            EmailNormalized = "private-no-org@example.com",
            DisplayName = "PrivateNoOrg",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        SetAuthUser(user.Id, user.Email);

        using MultipartFormDataContent form = BuildUploadForm(
            name: "PrivNoOrgPlugin",
            visibility: "private",
            ownerOrgId: null);  // private without org → invalid

        // Act
        HttpResponseMessage response = await _client.PostAsync("/api/v1/plugins/upload", form);

        // Assert — private without org is a 400 bad request
        // (CHECK constraint: visibility='public' OR owner_org_id IS NOT NULL)
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // =========================================================================
    // Task 7.5 — owner changes visibility org-private → public → 200 + owner_org_id cleared
    // Endpoint: PATCH /api/v1/plugins/{pluginId}/visibility
    // =========================================================================

    [Fact]
    public async Task PatchVisibility_OwnerChangesPrivateToPublic_Returns200AndClearsOwnerOrgId()
    {
        // Arrange — seed a private plugin owned by org
        (Guid ownerId, Guid orgId) = await SeedUserAndOrgAsync(
            "vis-change-owner@example.com", "Vis Change Org", "owner");

        // Seed private plugin directly in DB
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        Guid pluginId = Guid.NewGuid();
        PluginEntity plugin = new()
        {
            Id = pluginId,
            Name = "vis-change-plugin",
            NameNormalized = "vis-change-plugin",
            Slug = "vis-change-plugin",
            Description = "Visibility change test",
            Author = "Author",
            Visibility = "private",
            OwnerOrgId = orgId,
            OwnerUserId = ownerId,
            DownloadCount = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        SetAuthUser(ownerId, "vis-change-owner@example.com");

        using StringContent body = new(
            JsonSerializer.Serialize(new { visibility = "public" }),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act — PATCH /api/v1/plugins/{pluginId}/visibility is a NEW endpoint (does not exist yet)
        HttpResponseMessage response = await _client.PatchAsync(
            $"/api/v1/plugins/{pluginId}/visibility", body);

        // Assert — owner can change visibility; owner_org_id must be cleared when → public
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        PluginEntity? updated = await ctx.Plugins.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == pluginId);
        Assert.NotNull(updated);
        Assert.Equal("public", updated.Visibility);
        Assert.Null(updated.OwnerOrgId);  // cleared when visibility → public
    }

    // =========================================================================
    // Task 7.5 — owner changes visibility public → private (sets ownerOrgId) → 200
    // =========================================================================

    [Fact]
    public async Task PatchVisibility_OwnerChangesPublicToPrivate_Returns200AndSetsOwnerOrgId()
    {
        // Arrange — seed a public (ownerless) plugin; the owner changes it to private for their org
        (Guid ownerId, Guid orgId) = await SeedUserAndOrgAsync(
            "pub-to-priv-owner@example.com", "Pub To Priv Org", "owner");

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        Guid pluginId = Guid.NewGuid();
        PluginEntity plugin = new()
        {
            Id = pluginId,
            Name = "pub-to-priv-plugin",
            NameNormalized = "pub-to-priv-plugin",
            Slug = "pub-to-priv-plugin",
            Description = "Pub to priv test",
            Author = "Author",
            Visibility = "public",
            OwnerOrgId = orgId,    // already owned by org (public-with-org is valid)
            OwnerUserId = ownerId,
            DownloadCount = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        SetAuthUser(ownerId, "pub-to-priv-owner@example.com");

        using StringContent body = new(
            JsonSerializer.Serialize(new { visibility = "private", ownerOrgId = orgId }),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        HttpResponseMessage response = await _client.PatchAsync(
            $"/api/v1/plugins/{pluginId}/visibility", body);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        PluginEntity? updated = await ctx.Plugins.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == pluginId);
        Assert.NotNull(updated);
        Assert.Equal("private", updated.Visibility);
        Assert.Equal(orgId, updated.OwnerOrgId);
    }

    // =========================================================================
    // Task 7.5 — non-owner visibility change → 403
    // =========================================================================

    [Fact]
    public async Task PatchVisibility_NonOwner_Returns403()
    {
        // Arrange — plugin owned by org; outsider (not a member) attempts change
        (_, Guid orgId) = await SeedUserAndOrgAsync("nonowner-vis-orgowner@example.com", "NonOwner Org");

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        Guid pluginId = Guid.NewGuid();
        PluginEntity plugin = new()
        {
            Id = pluginId,
            Name = "nonowner-vis-plugin",
            NameNormalized = "nonowner-vis-plugin",
            Slug = "nonowner-vis-plugin",
            Description = "NonOwner vis test",
            Author = "Author",
            Visibility = "private",
            OwnerOrgId = orgId,
            OwnerUserId = null,
            DownloadCount = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        // Outsider: not a member of the org
        UserEntity outsider = new()
        {
            Id = Guid.NewGuid(),
            Email = "nonowner-vis-outsider@example.com",
            EmailNormalized = "nonowner-vis-outsider@example.com",
            DisplayName = "Outsider",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Users.Add(outsider);
        await ctx.SaveChangesAsync();

        SetAuthUser(outsider.Id, outsider.Email);

        using StringContent body = new(
            JsonSerializer.Serialize(new { visibility = "public" }),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        HttpResponseMessage response = await _client.PatchAsync(
            $"/api/v1/plugins/{pluginId}/visibility", body);

        // Assert — non-member write → 403
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // =========================================================================
    // Task 7.5 — unauthenticated visibility change → 401
    // =========================================================================

    [Fact]
    public async Task PatchVisibility_Unauthenticated_Returns401()
    {
        // Arrange — seed any plugin, no auth
        (_, Guid orgId) = await SeedUserAndOrgAsync("anon-vis-orgowner@example.com", "Anon Vis Org");

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        Guid pluginId = Guid.NewGuid();
        PluginEntity plugin = new()
        {
            Id = pluginId,
            Name = "anon-vis-plugin",
            NameNormalized = "anon-vis-plugin",
            Slug = "anon-vis-plugin",
            Description = "Anon vis test",
            Author = "Author",
            Visibility = "private",
            OwnerOrgId = orgId,
            OwnerUserId = null,
            DownloadCount = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        ClearAuthUser(); // anonymous

        using StringContent body = new(
            JsonSerializer.Serialize(new { visibility = "public" }),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        HttpResponseMessage response = await _client.PatchAsync(
            $"/api/v1/plugins/{pluginId}/visibility", body);

        // Assert — visibility change is always auth-required → 401
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // =========================================================================
    // Task 7.5 — visibility change produces audit log entry
    // =========================================================================

    [Fact]
    public async Task PatchVisibility_OwnerChangesVisibility_ProducesAuditLogEntry()
    {
        // Arrange
        (Guid ownerId, Guid orgId) = await SeedUserAndOrgAsync(
            "audit-vis-owner@example.com", "Audit Vis Org", "owner");

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        Guid pluginId = Guid.NewGuid();
        PluginEntity plugin = new()
        {
            Id = pluginId,
            Name = "audit-vis-plugin",
            NameNormalized = "audit-vis-plugin",
            Slug = "audit-vis-plugin",
            Description = "Audit vis test",
            Author = "Author",
            Visibility = "private",
            OwnerOrgId = orgId,
            OwnerUserId = ownerId,
            DownloadCount = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        SetAuthUser(ownerId, "audit-vis-owner@example.com");

        using StringContent body = new(
            JsonSerializer.Serialize(new { visibility = "public" }),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        HttpResponseMessage response = await _client.PatchAsync(
            $"/api/v1/plugins/{pluginId}/visibility", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Assert — "plugin.visibility_changed" audit entry must exist in org_audit_log
        bool auditExists = await ctx.OrgAuditLog
            .AsNoTracking()
            .AnyAsync(e => e.OrgId == orgId && e.Action == "plugin.visibility_changed");
        Assert.True(auditExists,
            "plugin.visibility_changed audit entry must be present after visibility change");
    }
}
