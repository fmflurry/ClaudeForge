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
using System.Formats.Tar;
using System.IO.Compression;

namespace ClaudeForge.Tests.Integration.PluginDistribution;

/// <summary>
/// Task 7.1 — Private-download enforcement in PluginDistribution.
///
/// Tests that DownloadPluginUseCase (once wired with ICurrentUser + IOrgMembershipQueryPort +
/// IPluginAccessPolicy) enforces the 404-read / 401-anonymous non-disclosure rule:
///
///   public  + anon                       → 200  (backward-compat)
///   public  + authenticated non-member   → 200  (public always accessible)
///   private + member of owning org       → 200  (member download succeeds)
///   private + anon                       → 401  (Unauthenticated — not 403)
///   private + authenticated non-member   → 404  (NotFound — non-disclosure)
///   private + member of DIFFERENT org    → 404  (NotFound — non-disclosure)
///
/// RED STATE: These tests will fail until:
///   - DownloadPluginUseCase gains (ICurrentUser, IOrgMembershipQueryPort, IPluginAccessPolicy)
///   - IPluginDistributionRepositoryPort.ResolveAsync returns plugin visibility + ownerOrgId
///   - The download endpoint maps AccessDecision → HTTP 401/404/200 appropriately.
///
/// The factory wires ICurrentUser as HeaderBasedTestCurrentUser (from OrganizationsHttpTests)
/// so tests control identity via X-Test-User-Id / X-Test-User-Email headers.
/// IOrgMembershipQueryPort is a real EF-backed adapter; test data populates organization_members.
/// </summary>
[Collection(PostgresFixture.CollectionName)]
public sealed class PrivateDownloadEnforcementTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _storageRoot;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public PrivateDownloadEnforcementTests(PostgresFixture fixture)
    {
        _fixture = fixture;

        _storageRoot = Path.Combine(
            Path.GetTempPath(),
            "claudeforge-authz-dist-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_storageRoot);

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace DbContext
                    ServiceDescriptor? optDesc = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<MarketplaceDbContext>));
                    if (optDesc is not null) services.Remove(optDesc);

                    ServiceDescriptor? ctxDesc = services.SingleOrDefault(
                        d => d.ServiceType == typeof(MarketplaceDbContext));
                    if (ctxDesc is not null) services.Remove(ctxDesc);

                    services.AddDbContext<MarketplaceDbContext>(opts =>
                        opts.UseNpgsql(fixture.ConnectionString));

                    // Replace storage with isolated local adapter
                    ServiceDescriptor? storageDesc = services.SingleOrDefault(
                        d => d.ServiceType == typeof(ClaudeForge.Core.Ports.IPackageStoragePort));
                    if (storageDesc is not null) services.Remove(storageDesc);

                    services.AddSingleton<ClaudeForge.Core.Ports.IPackageStoragePort>(
                        _ => new LocalFileSystemPackageStorageAdapter(_storageRoot));

                    // Replace ICurrentUser with header-driven test stub
                    // (ICurrentUser must be registered by the production module;
                    //  here we override it with the test double.)
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
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();

        try { Directory.Delete(_storageRoot, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static byte[] BuildMinimalTarGz(string pluginName, string version)
    {
        using MemoryStream output = new();
        using (GZipStream gzip = new(output, CompressionMode.Compress, leaveOpen: true))
        using (TarWriter tar = new(gzip, TarEntryFormat.Pax, leaveOpen: false))
        {
            byte[] manifestBytes = System.Text.Encoding.UTF8.GetBytes(
                $$"""{"name":"{{pluginName}}","version":"{{version}}","description":"Test","author":"Author","types":["skill"],"languages":["typescript"]}""");
            PaxTarEntry entry = new(TarEntryType.RegularFile, "plugin.json")
            {
                DataStream = new MemoryStream(manifestBytes),
            };
            tar.WriteEntry(entry);
        }
        return output.ToArray();
    }

    private async Task<Guid> SeedPublicPluginAsync(string name, string version = "1.0.0")
    {
        byte[] bytes = BuildMinimalTarGz(name, version);
        Guid pluginId = Guid.NewGuid();
        string key = $"plugins/{pluginId}/{version}/package.tar.gz";
        string path = Path.Combine(_storageRoot, key.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, bytes);

        string sha = Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(bytes));

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        PluginEntity plugin = new()
        {
            Id = pluginId,
            Name = name,
            NameNormalized = name.ToLowerInvariant(),
            Slug = name.ToLowerInvariant(),
            Description = "Test",
            Author = "Author",
            Visibility = "public",
            OwnerOrgId = null,
            OwnerUserId = null,
            DownloadCount = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        ctx.PluginVersions.Add(new PluginVersionEntity
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

    private async Task<(Guid pluginId, Guid orgId)> SeedPrivatePluginAsync(
        string name,
        Guid ownerOrgId,
        string version = "1.0.0")
    {
        byte[] bytes = BuildMinimalTarGz(name, version);
        Guid pluginId = Guid.NewGuid();
        string key = $"plugins/{pluginId}/{version}/package.tar.gz";
        string path = Path.Combine(_storageRoot, key.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, bytes);

        string sha = Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(bytes));

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        PluginEntity plugin = new()
        {
            Id = pluginId,
            Name = name,
            NameNormalized = name.ToLowerInvariant(),
            Slug = name.ToLowerInvariant(),
            Description = "Private test plugin",
            Author = "Author",
            Visibility = "private",
            OwnerOrgId = ownerOrgId,
            OwnerUserId = null,
            DownloadCount = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync();

        ctx.PluginVersions.Add(new PluginVersionEntity
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

        return (pluginId, ownerOrgId);
    }

    /// <summary>Seeds user + org + member row and returns (userId, orgId).</summary>
    private async Task<(Guid userId, Guid orgId)> SeedUserAndOrgAsync(
        string email,
        string orgName,
        string role = "member")
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

    // =========================================================================
    // 7.1 — Public plugin: anonymous → 200
    // =========================================================================

    [Fact]
    public async Task GetDownload_PublicPlugin_AnonymousUser_Returns200()
    {
        // Arrange — public plugin, no auth headers
        Guid pluginId = await SeedPublicPluginAsync("public-anon-dl");
        ClearAuthUser();

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/plugins/{pluginId}/download");

        // Assert — public + anon must always return 200 (backward-compat guarantee)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // =========================================================================
    // 7.1 — Private plugin + member of owning org → 200
    // =========================================================================

    [Fact]
    public async Task GetDownload_PrivatePlugin_MemberOfOwningOrg_Returns200()
    {
        // Arrange
        (Guid userId, Guid orgId) = await SeedUserAndOrgAsync(
            "member-dl@example.com", "DL Member Org");

        (Guid pluginId, _) = await SeedPrivatePluginAsync("private-member-dl", orgId);

        SetAuthUser(userId, "member-dl@example.com");

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/plugins/{pluginId}/download");

        // Assert — member of owning org must get 200
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // =========================================================================
    // 7.1 — Private plugin + anonymous → 401 (Unauthenticated, NOT 403)
    // =========================================================================

    [Fact]
    public async Task GetDownload_PrivatePlugin_AnonymousUser_Returns401()
    {
        // Arrange
        (_, Guid orgId) = await SeedUserAndOrgAsync(
            "org-owner-anon@example.com", "Anon Private Org");

        (Guid pluginId, _) = await SeedPrivatePluginAsync("private-anon-dl", orgId);

        ClearAuthUser(); // anonymous

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/plugins/{pluginId}/download");

        // Assert — private + anon MUST return 401 (not 403, not 404)
        // Design rule: "private + anon → Unauthenticated (401)"
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // =========================================================================
    // 7.1 — Private plugin + authenticated non-member → 404 (NonDisclosure)
    // =========================================================================

    [Fact]
    public async Task GetDownload_PrivatePlugin_AuthenticatedNonMember_Returns404()
    {
        // Arrange — private plugin owned by orgA; requester is NOT a member of orgA
        (_, Guid orgA) = await SeedUserAndOrgAsync("orgA-owner@example.com", "OrgA DL");

        (Guid pluginId, _) = await SeedPrivatePluginAsync("private-nonmember-dl", orgA);

        // Non-member user: exists in DB but not in orgA
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity outsider = new()
        {
            Id = Guid.NewGuid(),
            Email = "outsider-dl@example.com",
            EmailNormalized = "outsider-dl@example.com",
            DisplayName = "Outsider",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Users.Add(outsider);
        await ctx.SaveChangesAsync();

        SetAuthUser(outsider.Id, outsider.Email);

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/plugins/{pluginId}/download");

        // Assert — private + auth non-member MUST return 404 (non-disclosure, NOT 403)
        // Design rule: "private + authenticated non-member → NotFound(404)"
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // =========================================================================
    // 7.1 — Private plugin + member of DIFFERENT org → 404
    // =========================================================================

    [Fact]
    public async Task GetDownload_PrivatePlugin_MemberOfDifferentOrg_Returns404()
    {
        // Arrange — private plugin owned by orgA; user is member of orgB only
        (_, Guid orgA) = await SeedUserAndOrgAsync("orgA-owner2@example.com", "OrgA DL2");
        (Guid userB, _) = await SeedUserAndOrgAsync("orgB-member@example.com", "OrgB DL2");

        (Guid pluginId, _) = await SeedPrivatePluginAsync("private-diff-org-dl", orgA);

        // userB is a member of orgB (not orgA)
        SetAuthUser(userB, "orgB-member@example.com");

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/plugins/{pluginId}/download");

        // Assert — member of wrong org gets 404 (non-disclosure)
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // =========================================================================
    // 7.1 — Public plugin + authenticated non-member → 200
    // (Authenticated users can still download public plugins)
    // =========================================================================

    [Fact]
    public async Task GetDownload_PublicPlugin_AuthenticatedNonMember_Returns200()
    {
        // Arrange — public plugin; authenticated user with no org memberships
        Guid pluginId = await SeedPublicPluginAsync("public-auth-dl");

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity authUser = new()
        {
            Id = Guid.NewGuid(),
            Email = "auth-public-dl@example.com",
            EmailNormalized = "auth-public-dl@example.com",
            DisplayName = "AuthUser",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Users.Add(authUser);
        await ctx.SaveChangesAsync();

        SetAuthUser(authUser.Id, authUser.Email);

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/plugins/{pluginId}/download");

        // Assert — public is always 200 regardless of auth state
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // =========================================================================
    // 7.1 — 404 response for private plugin does NOT reveal existence
    //       (body must NOT say "private" or identify visibility)
    // =========================================================================

    [Fact]
    public async Task GetDownload_PrivatePlugin_NonMember_404BodyDoesNotRevealVisibility()
    {
        // Arrange
        (_, Guid orgId) = await SeedUserAndOrgAsync("reveal-owner@example.com", "Reveal Org");
        (Guid pluginId, _) = await SeedPrivatePluginAsync("reveal-visibility-dl", orgId);

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity outsider = new()
        {
            Id = Guid.NewGuid(),
            Email = "reveal-outsider@example.com",
            EmailNormalized = "reveal-outsider@example.com",
            DisplayName = "RevealOutsider",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Users.Add(outsider);
        await ctx.SaveChangesAsync();

        SetAuthUser(outsider.Id, outsider.Email);

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/plugins/{pluginId}/download");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        // Non-disclosure: detail must match the standard "Plugin not found" — not expose "private"
        using JsonDocument doc = JsonDocument.Parse(body);
        string? detail = doc.RootElement.TryGetProperty("detail", out JsonElement detailEl)
            ? detailEl.GetString()
            : null;

        Assert.NotNull(detail);
        Assert.DoesNotContain("private", detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("visibility", detail, StringComparison.OrdinalIgnoreCase);
        // Must still be the standard "Plugin not found" message (non-disclosure)
        Assert.Equal("Plugin not found", detail);
    }
}
