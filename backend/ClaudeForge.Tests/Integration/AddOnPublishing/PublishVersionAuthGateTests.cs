using System.Formats.Tar;
using System.IO.Compression;
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

namespace ClaudeForge.Tests.Integration.AddOnPublishing;

/// <summary>
/// Task — Publish-version auth gate.
///
/// Scenarios:
///   anonymous POST /{pluginId}/versions                         → 401
///   authenticated non-member publishing to org-owned plugin    → 403
///   authenticated owner/member publishing to org-owned plugin  → 201
///   non-creator trying to adopt an ownerless public plugin
///     into their org via PATCH /visibility                     → 403
///
/// RED STATE: These tests will fail until PublishVersionUseCase gains auth checks
/// and the /versions endpoint gains .RequireAuthorization().
/// </summary>
[Collection(PostgresFixture.CollectionName)]
public sealed class PublishVersionAuthGateTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public PublishVersionAuthGateTests(PostgresFixture fixture)
    {
        _fixture = fixture;

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        // Upload can be anonymous in these tests — only version-publish gate is under test.
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
    // Archive + form helpers
    // =========================================================================

    private static MemoryStream BuildValidTarGz(string version = "1.0.0")
    {
        MemoryStream output = new();
        using (GZipStream gzip = new(output, CompressionMode.Compress, leaveOpen: true))
        using (TarWriter tar = new(gzip, TarEntryFormat.Pax, leaveOpen: false))
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(
                $$"""{"name":"test-plugin","version":"{{version}}","description":"Desc","author":"Author","types":["skill"],"languages":["typescript"]}""");
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
        string pluginName,
        string version = "1.0.0",
        string? visibility = null,
        string? ownerOrgId = null)
    {
        MultipartFormDataContent form = new();
        MemoryStream archive = BuildValidTarGz(version);
        StreamContent packageContent = new(archive);
        packageContent.Headers.ContentType = new MediaTypeHeaderValue("application/gzip");
        form.Add(packageContent, "package", $"{pluginName.ToLowerInvariant()}-{version}.tar.gz");
        form.Add(new StringContent(pluginName), "name");
        form.Add(new StringContent("Test description"), "description");
        form.Add(new StringContent("Test Author"), "author");
        form.Add(new StringContent(version), "initialVersion");
        form.Add(new StringContent("Initial release"), "releaseNotes");
        if (visibility is not null)
            form.Add(new StringContent(visibility), "visibility");
        if (ownerOrgId is not null)
            form.Add(new StringContent(ownerOrgId), "ownerOrgId");
        return form;
    }

    private static MultipartFormDataContent BuildVersionForm(string version = "2.0.0")
    {
        MultipartFormDataContent form = new();
        MemoryStream archive = BuildValidTarGz(version);
        StreamContent packageContent = new(archive);
        packageContent.Headers.ContentType = new MediaTypeHeaderValue("application/gzip");
        form.Add(packageContent, "package", $"plugin-{version}.tar.gz");
        form.Add(new StringContent(version), "versionNumber");
        form.Add(new StringContent("New version"), "releaseNotes");
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

    private async Task<Guid> SeedPluginOwnedByOrgAsync(
        string pluginName, Guid orgId, Guid? ownerUserId = null)
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        Guid pluginId = Guid.NewGuid();
        AddOnEntity plugin = new()
        {
            Id = pluginId,
            Name = pluginName,
            NameNormalized = pluginName.ToLowerInvariant(),
            Slug = pluginName.ToLowerInvariant(),
            Description = "Auth gate test plugin",
            Author = "Author",
            Visibility = "private",
            OwnerOrgId = orgId,
            OwnerUserId = ownerUserId,
            DownloadCount = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Plugins.Add(plugin);

        AddOnVersionEntity version = new()
        {
            Id = Guid.NewGuid(),
            PluginId = pluginId,
            Version = "1.0.0",
            VersionSort = 1_000_000_000L,
            PackageKey = $"plugins/{pluginId}/1.0.0/package.tar.gz",
            PackageFormat = "tar.gz",
            SizeBytes = 1024,
            Sha256 = "abc",
            ReleaseNotes = "Initial",
            IsLatest = true,
            ReleasedAt = DateTimeOffset.UtcNow,
        };
        ctx.PluginVersions.Add(version);
        await ctx.SaveChangesAsync();

        return pluginId;
    }

    private async Task<Guid> SeedOwnerlessPublicPluginAsync(
        string pluginName, Guid ownerUserId)
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        Guid pluginId = Guid.NewGuid();
        AddOnEntity plugin = new()
        {
            Id = pluginId,
            Name = pluginName,
            NameNormalized = pluginName.ToLowerInvariant(),
            Slug = pluginName.ToLowerInvariant(),
            Description = "Ownerless public plugin",
            Author = "Author",
            Visibility = "public",
            OwnerOrgId = null,
            OwnerUserId = ownerUserId,
            DownloadCount = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        return pluginId;
    }

    // =========================================================================
    // Test 1: Anonymous POST /{pluginId}/versions → 401
    // =========================================================================

    [Fact]
    public async Task PostVersion_Anonymous_Returns401()
    {
        // Arrange — seed a plugin (upload is anonymous since flag is OFF)
        using MultipartFormDataContent uploadForm = BuildUploadForm("AnonVersionPlugin");
        HttpResponseMessage uploadResp = await _client.PostAsync("/api/v1/plugins/upload", uploadForm);
        Assert.Equal(HttpStatusCode.Created, uploadResp.StatusCode);

        string uploadBody = await uploadResp.Content.ReadAsStringAsync();
        using JsonDocument uploadDoc = JsonDocument.Parse(uploadBody);
        Guid pluginId = Guid.Parse(uploadDoc.RootElement.GetProperty("pluginId").GetString()!);

        // No auth headers — anonymous caller
        ClearAuthUser();

        using MultipartFormDataContent versionForm = BuildVersionForm("2.0.0");

        // Act
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/plugins/{pluginId}/versions", versionForm);

        // Assert — anonymous must be rejected 401
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // =========================================================================
    // Test 2: Authenticated non-member publishing to org-owned plugin → 403
    // =========================================================================

    [Fact]
    public async Task PostVersion_AuthenticatedNonMember_Returns403()
    {
        // Arrange — org with member user; outsider is NOT a member
        (_, Guid orgId) = await SeedUserAndOrgAsync("pvgates-org-owner@example.com", "PVGates Org");

        Guid pluginId = await SeedPluginOwnedByOrgAsync("PvgatesOrgPlugin", orgId);

        // Outsider — authenticated but not a member of the org
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity outsider = new()
        {
            Id = Guid.NewGuid(),
            Email = "pvgates-outsider@example.com",
            EmailNormalized = "pvgates-outsider@example.com",
            DisplayName = "Outsider",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Users.Add(outsider);
        await ctx.SaveChangesAsync();

        SetAuthUser(outsider.Id, outsider.Email);

        using MultipartFormDataContent versionForm = BuildVersionForm("2.0.0");

        // Act
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/plugins/{pluginId}/versions", versionForm);

        // Assert — authenticated non-member → 403
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // =========================================================================
    // Test 3: Authenticated owner/member publishing to org-owned plugin → 201
    // =========================================================================

    [Fact]
    public async Task PostVersion_AuthenticatedMember_Returns201()
    {
        // Arrange — seed user + org, seed plugin owned by that org
        (Guid memberId, Guid orgId) = await SeedUserAndOrgAsync(
            "pvgates-member@example.com", "PVGates Member Org", "owner");

        Guid pluginId = await SeedPluginOwnedByOrgAsync("PvgatesMemberPlugin", orgId, memberId);

        SetAuthUser(memberId, "pvgates-member@example.com");

        using MultipartFormDataContent versionForm = BuildVersionForm("2.0.0");

        // Act
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/plugins/{pluginId}/versions", versionForm);

        // Assert — authenticated member → 201
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // =========================================================================
    // Test 4: Non-creator trying to adopt an ownerless public plugin into
    //         their org via PATCH /visibility → 403
    // =========================================================================

    [Fact]
    public async Task PatchVisibility_NonCreatorAdoptsOwnerlessPlugin_Returns403()
    {
        // Arrange — creator owns an ownerless public plugin
        await using MarketplaceDbContext setupCtx = _fixture.CreateContext();
        UserEntity creator = new()
        {
            Id = Guid.NewGuid(),
            Email = "ownerless-creator@example.com",
            EmailNormalized = "ownerless-creator@example.com",
            DisplayName = "Creator",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        setupCtx.Users.Add(creator);
        await setupCtx.SaveChangesAsync();

        Guid pluginId = await SeedOwnerlessPublicPluginAsync("OwnerlessPlugin", creator.Id);

        // Non-creator: authenticated, has their own org, but is NOT the creator of the plugin
        (Guid attackerId, Guid attackerOrgId) = await SeedUserAndOrgAsync(
            "ownerless-attacker@example.com", "Attacker Org", "owner");

        SetAuthUser(attackerId, "ownerless-attacker@example.com");

        using StringContent body = new(
            System.Text.Json.JsonSerializer.Serialize(new
            {
                visibility = "private",
                ownerOrgId = attackerOrgId,
            }),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act — attacker tries to claim the ownerless plugin for their org
        HttpResponseMessage response = await _client.PatchAsync(
            $"/api/v1/plugins/{pluginId}/visibility", body);

        // Assert — only the creator may adopt an ownerless plugin → 403
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
