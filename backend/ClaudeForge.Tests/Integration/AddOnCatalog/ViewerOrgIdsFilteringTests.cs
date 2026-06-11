using System.Net;
using System.Text.Json;
using ClaudeForge.Core.Shared.Authorization;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using ClaudeForge.Tests.Integration.Fixtures;
using ClaudeForge.Tests.Integration.Organizations;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeForge.Tests.Integration.AddOnCatalog;

/// <summary>
/// Task 7.7 — viewerOrgIds filtering in PluginCatalog and PluginSearch.
///
/// Tests that once ICurrentUser + IOrgMembershipQueryPort are threaded into
/// ListAddOnsUseCase, GetAddOnDetailsUseCase, SearchAddOnsUseCase, and
/// DiscoverAddOnsUseCase, the viewerOrgIds SQL predicate is correctly applied:
///
///   SQL:  visibility='public' OR owner_org_id = ANY(@viewerOrgIds)
///
/// Scenarios:
///   anon sees only public plugins (private excluded from list, search, discover, count, pagination)
///   member sees public + own-org private
///   other-org private excluded from all surfaces (list, search, discover, counts, totals)
///   multi-org member sees all private plugins from all their orgs
///   GET /api/v1/plugins/{id} for private plugin the viewer cannot see → 404
///   search count with private excludes → totalCount correct (not inflated by invisible items)
///
/// RED STATE: These tests fail until:
///   - ListAddOnsUseCase gains (ICurrentUser, IOrgMembershipQueryPort) constructor args
///   - IAddOnRepositoryPort.ListAddOnsAsync gains viewerOrgIds: IReadOnlySet&lt;Guid&gt; arg
///   - IAddOnRepositoryPort.GetAddOnByIdAsync gains viewerOrgIds: IReadOnlySet&lt;Guid&gt; arg
///   - ISearchIndexPort.SearchAsync + DiscoverAsync gain viewerOrgIds args
///   - SQL predicate (visibility='public' OR owner_org_id = ANY(@ids)) applied in adapters
///   - GetAddOnDetailsUseCase gains (ICurrentUser, IOrgMembershipQueryPort)
///   - SearchAddOnsUseCase + DiscoverAddOnsUseCase gain (ICurrentUser, IOrgMembershipQueryPort)
/// </summary>
[Collection(PostgresFixture.CollectionName)]
public sealed class ViewerOrgIdsFilteringTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public ViewerOrgIdsFilteringTests(PostgresFixture fixture)
    {
        _fixture = fixture;

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
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

                    // Override ICurrentUser with header-based test stub
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
    // Helpers
    // =========================================================================

    private static AddOnEntity MakePublicPlugin(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        NameNormalized = name.ToLowerInvariant(),
        Slug = name.ToLowerInvariant(),
        Description = $"Public plugin {name}",
        Author = "Author",
        Visibility = "public",
        OwnerOrgId = null,
        OwnerUserId = null,
        DownloadCount = 0,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private static AddOnEntity MakePrivatePlugin(string name, Guid ownerOrgId, Guid? ownerUserId = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        NameNormalized = name.ToLowerInvariant(),
        Slug = name.ToLowerInvariant(),
        Description = $"Private plugin {name}",
        Author = "Author",
        Visibility = "private",
        OwnerOrgId = ownerOrgId,
        OwnerUserId = ownerUserId,
        DownloadCount = 0,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private async Task SeedVersionAsync(Guid pluginId, string version = "1.0.0")
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        ctx.PluginVersions.Add(new AddOnVersionEntity
        {
            Id = Guid.NewGuid(),
            PluginId = pluginId,
            Version = version,
            VersionSort = 1_000_000L,
            IsLatest = true,
            PackageKey = $"plugins/{pluginId}/{version}/package.tar.gz",
            PackageFormat = "tar.gz",
            SizeBytes = 100,
            Sha256 = new string('a', 64),
            DownloadCount = 0,
            ReleasedAt = DateTimeOffset.UtcNow,
            ReleaseNotes = string.Empty,
        });
        await ctx.SaveChangesAsync();
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
    // 7.7 — Anonymous sees only public plugins in catalog list
    // =========================================================================

    [Fact]
    public async Task ListPlugins_Anonymous_SeesOnlyPublicPlugins()
    {
        // Arrange — 1 public + 1 private
        (_, Guid orgId) = await SeedUserAndOrgAsync("anon-list-owner@example.com", "Anon List Org");

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        AddOnEntity pub = MakePublicPlugin("anon-list-public");
        AddOnEntity priv = MakePrivatePlugin("anon-list-private", orgId);
        ctx.Plugins.AddRange(pub, priv);
        await ctx.SaveChangesAsync();

        await SeedVersionAsync(pub.Id);
        await SeedVersionAsync(priv.Id);

        ClearAuthUser(); // anonymous

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/plugins");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        // totalCount must NOT include the private plugin
        int totalCount = root.GetProperty("totalCount").GetInt32();
        Assert.Equal(1, totalCount);

        JsonElement data = root.GetProperty("data");
        Assert.Equal(1, data.GetArrayLength());
        Assert.Equal("anon-list-public", data[0].GetProperty("name").GetString());
    }

    // =========================================================================
    // 7.7 — Member sees public + own-org private
    // =========================================================================

    [Fact]
    public async Task ListPlugins_Member_SeesPublicPlusOwnOrgPrivate()
    {
        // Arrange
        (Guid memberId, Guid orgId) = await SeedUserAndOrgAsync(
            "member-list-user@example.com", "Member List Org");

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        AddOnEntity pub = MakePublicPlugin("member-list-public");
        AddOnEntity ownPriv = MakePrivatePlugin("member-list-own-private", orgId);
        ctx.Plugins.AddRange(pub, ownPriv);
        await ctx.SaveChangesAsync();

        await SeedVersionAsync(pub.Id);
        await SeedVersionAsync(ownPriv.Id);

        SetAuthUser(memberId, "member-list-user@example.com");

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/plugins");

        // Assert — member sees both (2 total)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        int totalCount = root.GetProperty("totalCount").GetInt32();
        Assert.Equal(2, totalCount);

        // Verify both names appear
        HashSet<string?> names = root.GetProperty("data")
            .EnumerateArray()
            .Select(e => e.GetProperty("name").GetString())
            .ToHashSet();

        Assert.Contains("member-list-public", names);
        Assert.Contains("member-list-own-private", names);
    }

    // =========================================================================
    // 7.7 — Other-org private is excluded from list (not just data but totalCount too)
    // =========================================================================

    [Fact]
    public async Task ListPlugins_MemberOfOrgB_DoesNotSeeOrgAPrivate_AndCountIsCorrect()
    {
        // Arrange — two orgs; member of orgB; private plugin belongs to orgA
        (_, Guid orgA) = await SeedUserAndOrgAsync("orgA-list-owner@example.com", "OrgA List");
        (Guid memberB, _) = await SeedUserAndOrgAsync("orgB-list-member@example.com", "OrgB List");

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        AddOnEntity pub = MakePublicPlugin("cross-org-list-public");
        AddOnEntity orgAPriv = MakePrivatePlugin("orgA-list-private", orgA); // not visible to orgB member
        ctx.Plugins.AddRange(pub, orgAPriv);
        await ctx.SaveChangesAsync();

        await SeedVersionAsync(pub.Id);
        await SeedVersionAsync(orgAPriv.Id);

        SetAuthUser(memberB, "orgB-list-member@example.com");

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/plugins");

        // Assert — orgB member sees only the public plugin; count = 1 (NOT 2)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        // CRITICAL: totalCount must not leak orgA's private plugin
        int totalCount = root.GetProperty("totalCount").GetInt32();
        Assert.Equal(1, totalCount);

        HashSet<string?> names = root.GetProperty("data")
            .EnumerateArray()
            .Select(e => e.GetProperty("name").GetString())
            .ToHashSet();

        Assert.Contains("cross-org-list-public", names);
        Assert.DoesNotContain("orgA-list-private", names);
    }

    // =========================================================================
    // 7.7 — Multi-org member sees private from all their orgs
    // =========================================================================

    [Fact]
    public async Task ListPlugins_MultiOrgMember_SeesPrivateFromAllOwnOrgs()
    {
        // Arrange — user is member of both orgX and orgY
        (Guid userId, Guid orgX) = await SeedUserAndOrgAsync(
            "multi-org-member@example.com", "OrgX Multi");

        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        // Add user to orgY as well
        OrganizationEntity orgYEntity = new()
        {
            Id = Guid.NewGuid(),
            Name = "OrgY Multi",
            NameNormalized = "orgy-multi",
            Slug = "orgy-multi",
            CreatedBy = userId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Organizations.Add(orgYEntity);
        await ctx.SaveChangesAsync();

        ctx.OrganizationMembers.Add(new OrganizationMemberEntity
        {
            OrgId = orgYEntity.Id,
            UserId = userId,
            Role = "member",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await ctx.SaveChangesAsync();

        Guid orgY = orgYEntity.Id;

        AddOnEntity pub = MakePublicPlugin("multi-org-public");
        AddOnEntity orgXPriv = MakePrivatePlugin("multi-org-x-private", orgX);
        AddOnEntity orgYPriv = MakePrivatePlugin("multi-org-y-private", orgY);
        ctx.Plugins.AddRange(pub, orgXPriv, orgYPriv);
        await ctx.SaveChangesAsync();

        await SeedVersionAsync(pub.Id);
        await SeedVersionAsync(orgXPriv.Id);
        await SeedVersionAsync(orgYPriv.Id);

        SetAuthUser(userId, "multi-org-member@example.com");

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/plugins");

        // Assert — sees all 3: public + orgX private + orgY private
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        int totalCount = root.GetProperty("totalCount").GetInt32();
        Assert.Equal(3, totalCount);

        HashSet<string?> names = root.GetProperty("data")
            .EnumerateArray()
            .Select(e => e.GetProperty("name").GetString())
            .ToHashSet();

        Assert.Contains("multi-org-public", names);
        Assert.Contains("multi-org-x-private", names);
        Assert.Contains("multi-org-y-private", names);
    }

    // =========================================================================
    // 7.7 — GET /api/v1/plugins/{id}: private plugin not accessible to non-member → 404
    // =========================================================================

    [Fact]
    public async Task GetPluginDetails_PrivatePlugin_NonMember_Returns404()
    {
        // Arrange
        (_, Guid orgId) = await SeedUserAndOrgAsync(
            "detail-filter-owner@example.com", "Detail Filter Org");

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        AddOnEntity priv = MakePrivatePlugin("detail-filter-private", orgId);
        ctx.Plugins.Add(priv);
        await ctx.SaveChangesAsync();
        await SeedVersionAsync(priv.Id);

        // Outsider: authenticated but not a member
        UserEntity outsider = new()
        {
            Id = Guid.NewGuid(),
            Email = "detail-outsider@example.com",
            EmailNormalized = "detail-outsider@example.com",
            DisplayName = "Outsider",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Users.Add(outsider);
        await ctx.SaveChangesAsync();

        SetAuthUser(outsider.Id, outsider.Email);

        // Act — GET /api/v1/plugins/{pluginId}
        HttpResponseMessage response = await _client.GetAsync($"/api/v1/plugins/{priv.Id}");

        // Assert — non-member fetching private plugin detail → 404 (non-disclosure)
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // =========================================================================
    // 7.7 — GET /api/v1/plugins/{id}: private plugin → member can see it (200)
    // =========================================================================

    [Fact]
    public async Task GetPluginDetails_PrivatePlugin_Member_Returns200()
    {
        // Arrange
        (Guid memberId, Guid orgId) = await SeedUserAndOrgAsync(
            "detail-member-user@example.com", "Detail Member Org");

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        AddOnEntity priv = MakePrivatePlugin("detail-member-private", orgId, memberId);
        ctx.Plugins.Add(priv);
        await ctx.SaveChangesAsync();
        await SeedVersionAsync(priv.Id);

        SetAuthUser(memberId, "detail-member-user@example.com");

        // Act
        HttpResponseMessage response = await _client.GetAsync($"/api/v1/plugins/{priv.Id}");

        // Assert — member of owning org can access private plugin detail
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        Assert.Equal("detail-member-private", doc.RootElement.GetProperty("name").GetString());
    }

    // =========================================================================
    // 7.7 — GET /api/v1/plugins/{id}: private plugin → anonymous → 404
    // (not 401: single-plugin fetch uses non-disclosure 404 for both anon and auth-non-member)
    // BUT design §5 says: private+anon on DOWNLOAD → 401; for catalog read → 404 non-disclosure
    // =========================================================================

    [Fact]
    public async Task GetPluginDetails_PrivatePlugin_Anonymous_Returns404()
    {
        // Arrange
        (_, Guid orgId) = await SeedUserAndOrgAsync(
            "detail-anon-owner@example.com", "Detail Anon Org");

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        AddOnEntity priv = MakePrivatePlugin("detail-anon-private", orgId);
        ctx.Plugins.Add(priv);
        await ctx.SaveChangesAsync();
        await SeedVersionAsync(priv.Id);

        ClearAuthUser(); // anonymous

        // Act
        HttpResponseMessage response = await _client.GetAsync($"/api/v1/plugins/{priv.Id}");

        // Assert — anonymous access to private plugin detail → 404 (row not in result set)
        // Note: catalog detail (GET) is NOT restricted by [Authorize]; it just returns 404 when
        // the viewerOrgIds predicate excludes the row. This is the SQL-level non-disclosure.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // =========================================================================
    // 7.7 — Search: private plugin excluded for anonymous (count correct)
    // =========================================================================

    [Fact]
    public async Task SearchPlugins_Anonymous_ExcludesPrivateFromResultsAndCount()
    {
        // Arrange — 1 public named "search-public-vis", 1 private named "search-private-vis"
        (_, Guid orgId) = await SeedUserAndOrgAsync(
            "search-vis-owner@example.com", "Search Vis Org");

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        AddOnEntity pub = MakePublicPlugin("search-public-vis");
        pub.Description = "A searchable public plugin for visibility test";
        AddOnEntity priv = MakePrivatePlugin("search-private-vis", orgId);
        priv.Description = "A searchable private plugin for visibility test";
        ctx.Plugins.AddRange(pub, priv);
        await ctx.SaveChangesAsync();

        await SeedVersionAsync(pub.Id);
        await SeedVersionAsync(priv.Id);

        ClearAuthUser(); // anonymous

        // Act — search for "searchable" (hits both plugins on description)
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/plugins/search?q=searchable+visibility+test");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        // totalCount must be 1 (private excluded from count too)
        int totalCount = root.GetProperty("totalCount").GetInt32();
        Assert.Equal(1, totalCount);

        // Data must not contain the private plugin
        HashSet<string?> names = root.GetProperty("data")
            .EnumerateArray()
            .Select(e => e.TryGetProperty("name", out JsonElement nameEl)
                ? nameEl.GetString()
                : null)
            .ToHashSet();

        Assert.DoesNotContain("search-private-vis", names);
    }

    // =========================================================================
    // 7.7 — Search: member sees own-org private in search results
    // =========================================================================

    [Fact]
    public async Task SearchPlugins_Member_SeesOwnOrgPrivateInResults()
    {
        // Arrange
        (Guid memberId, Guid orgId) = await SeedUserAndOrgAsync(
            "search-member-vis@example.com", "Search Member Vis Org");

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        AddOnEntity priv = MakePrivatePlugin("search-member-private-vis", orgId);
        priv.Description = "Private search member visibility test plugin";
        ctx.Plugins.Add(priv);
        await ctx.SaveChangesAsync();

        await SeedVersionAsync(priv.Id);

        SetAuthUser(memberId, "search-member-vis@example.com");

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/plugins/search?q=private+search+member+visibility");

        // Assert — member sees own private in search
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        int totalCount = root.GetProperty("totalCount").GetInt32();
        Assert.True(totalCount >= 1, "Member should see at least their own-org private plugin");
    }

    // =========================================================================
    // 7.7 — Discover: private excluded for anonymous
    // =========================================================================

    [Fact]
    public async Task DiscoverPlugins_Anonymous_ExcludesPrivate()
    {
        // Arrange
        (_, Guid orgId) = await SeedUserAndOrgAsync(
            "discover-anon-owner@example.com", "Discover Anon Org");

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        AddOnEntity pub = MakePublicPlugin("discover-public");
        pub.Description = "Discover public plugin for filtering test";
        AddOnEntity priv = MakePrivatePlugin("discover-private", orgId);
        priv.Description = "Discover private plugin for filtering test";
        ctx.Plugins.AddRange(pub, priv);
        await ctx.SaveChangesAsync();

        await SeedVersionAsync(pub.Id);
        await SeedVersionAsync(priv.Id);

        ClearAuthUser(); // anonymous

        // Act
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/discovery?keyword=discover+filtering");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        // items must not contain the private plugin
        JsonElement items = root.TryGetProperty("items", out JsonElement itemsEl)
            ? itemsEl
            : root; // allow flat or wrapped

        HashSet<string?> names = items
            .EnumerateArray()
            .Select(e => e.TryGetProperty("name", out JsonElement nameEl)
                ? nameEl.GetString()
                : null)
            .ToHashSet();

        Assert.DoesNotContain("discover-private", names);
    }

    // =========================================================================
    // 7.7 — Pagination counts exclude invisible plugins
    //       (totalPages computed from filtered count, not raw row count)
    // =========================================================================

    [Fact]
    public async Task ListPlugins_Anonymous_PaginationTotalsExcludePrivate()
    {
        // Arrange — 3 public + 2 private (from separate org); limit=2 → page 1 = 2 items, page 2 = 1 item
        (_, Guid orgId) = await SeedUserAndOrgAsync(
            "pagination-owner@example.com", "Pagination Org");

        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        string[] pubNames = ["pagination-pub-1", "pagination-pub-2", "pagination-pub-3"];
        string[] privNames = ["pagination-priv-1", "pagination-priv-2"];

        List<AddOnEntity> all = [
            ..pubNames.Select(MakePublicPlugin),
            ..privNames.Select(n => MakePrivatePlugin(n, orgId)),
        ];
        ctx.Plugins.AddRange(all);
        await ctx.SaveChangesAsync();

        foreach (AddOnEntity p in all)
            await SeedVersionAsync(p.Id);

        ClearAuthUser(); // anonymous

        // Act — page 1 of 2, limit 2
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/plugins?page=1&limit=2");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        // totalCount = 3 (only public); private 2 excluded
        int totalCount = root.GetProperty("totalCount").GetInt32();
        Assert.Equal(3, totalCount);

        // totalPages = ceil(3/2) = 2
        int totalPages = root.GetProperty("totalPages").GetInt32();
        Assert.Equal(2, totalPages);

        // Page 1 data has 2 items (all public)
        int dataCount = root.GetProperty("data").GetArrayLength();
        Assert.Equal(2, dataCount);
    }
}
