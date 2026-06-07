using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using ClaudeForge.Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ClaudeForge.Tests.Integration.Auth;

/// <summary>
/// Integration tests for Group 1, Task 1.4:
/// The plugins chk_visibility_owner CHECK constraint.
///
/// Schema rule (from design.md):
///   CHECK(visibility='public' OR owner_org_id IS NOT NULL)
///
/// Meaning:
///   - private + NULL owner_org_id    → REJECTED
///   - private + non-NULL owner_org_id → ACCEPTED
///   - public  + NULL owner_org_id    → ACCEPTED (legacy / default)
///   - public  + non-NULL owner_org_id → ACCEPTED (org can own a public plugin)
///
/// Also asserts that additive columns on plugins have correct defaults:
///   - visibility defaults to 'public' (existing rows unaffected)
///   - owner_org_id is nullable
///   - owner_user_id is nullable
///
/// Expected production types referenced here that do NOT yet exist:
///
///   ClaudeForge.Infrastructure.Persistence.Entities.PluginEntity
///     string  Visibility    (NOT NULL, DEFAULT 'public')
///     Guid?   OwnerOrgId    (nullable FK → organizations)
///     Guid?   OwnerUserId   (nullable FK → users)
///
///   ClaudeForge.Infrastructure.Persistence.MarketplaceDbContext
///     DbSet&lt;UserEntity&gt;         Users
///     DbSet&lt;OrganizationEntity&gt; Organizations
///
/// The tests will FAIL TO COMPILE until the coder adds the Visibility,
/// OwnerOrgId, and OwnerUserId properties to PluginEntity and creates the
/// UserEntity and OrganizationEntity types with their DbSet registrations.
/// </summary>
[Collection(PostgresFixture.CollectionName)]
public sealed class PluginVisibilityConstraintTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public PluginVisibilityConstraintTests(PostgresFixture fixture)
    {
        _fixture = fixture;
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

    public Task DisposeAsync() => Task.CompletedTask;

    // -------------------------------------------------------------------------
    // Helper factories
    // -------------------------------------------------------------------------

    private static PluginEntity MakePlugin(string name, string slug) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        NameNormalized = name.ToLowerInvariant(),
        Slug = slug,
        Description = "Test plugin",
        Author = "test-author",
        DownloadCount = 0,
        Visibility = "public",          // default
        OwnerOrgId = null,              // ownerless by default
        OwnerUserId = null,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private static UserEntity MakeUser(string email) => new()
    {
        Id = Guid.NewGuid(),
        Email = email,
        EmailNormalized = email.ToLowerInvariant(),
        DisplayName = email.Split('@')[0],
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private static OrganizationEntity MakeOrg(Guid createdBy, string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        NameNormalized = name.ToLowerInvariant(),
        Slug = name.ToLowerInvariant().Replace(' ', '-'),
        CreatedBy = createdBy,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    // =========================================================================
    // chk_visibility_owner CHECK constraint
    // =========================================================================

    // -------------------------------------------------------------------------
    // Test CV1 — CRITICAL: private plugin with NULL owner_org_id → REJECTED
    //           This is the primary invariant from design.md
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Insert_PrivatePluginWithNullOwnerOrgId_ViolatesCheckConstraint()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        PluginEntity invalid = new()
        {
            Id = Guid.NewGuid(),
            Name = "PrivateOwnerlessPlugin",
            NameNormalized = "privateownerlessplugin",
            Slug = "private-ownerless-cv1",
            Description = "Private plugin without an owning org",
            Author = "test-author",
            DownloadCount = 0,
            Visibility = "private",     // private requires owner_org_id to be set
            OwnerOrgId = null,          // violates CHECK(visibility='public' OR owner_org_id IS NOT NULL)
            OwnerUserId = null,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        ctx.Plugins.Add(invalid);
        Exception ex = await Assert.ThrowsAnyAsync<Exception>(() => ctx.SaveChangesAsync());
        Assert.True(
            ex is DbUpdateException or PostgresException,
            $"Expected CHECK constraint violation but got {ex.GetType().Name}: {ex.Message}");
    }

    // -------------------------------------------------------------------------
    // Test CV2 — public plugin with NULL owner_org_id → ACCEPTED
    //           Legacy/anonymous plugins: public + ownerless is valid
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Insert_PublicPluginWithNullOwnerOrgId_Succeeds()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        PluginEntity plugin = MakePlugin("PublicOwnerlessPlugin", "public-ownerless-cv2");
        // Visibility = "public", OwnerOrgId = null — satisfies CHECK

        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync(); // Must NOT throw

        PluginEntity? persisted = await ctx.Plugins.AsNoTracking().FirstOrDefaultAsync(p => p.Id == plugin.Id);
        Assert.NotNull(persisted);
        Assert.Equal("public", persisted.Visibility);
        Assert.Null(persisted.OwnerOrgId);
    }

    // -------------------------------------------------------------------------
    // Test CV3 — private plugin with non-NULL owner_org_id → ACCEPTED
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Insert_PrivatePluginWithOwnerOrgId_Succeeds()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        UserEntity owner = MakeUser("private-org-owner@example.com");
        ctx.Users.Add(owner);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, "PrivatePluginOrg");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        PluginEntity plugin = new()
        {
            Id = Guid.NewGuid(),
            Name = "PrivateOrgPlugin",
            NameNormalized = "privateorgplugin",
            Slug = "private-org-cv3",
            Description = "Private plugin owned by an org",
            Author = "test-author",
            DownloadCount = 0,
            Visibility = "private",
            OwnerOrgId = org.Id,        // satisfies CHECK
            OwnerUserId = owner.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync(); // Must NOT throw

        PluginEntity? persisted = await ctx.Plugins.AsNoTracking().FirstOrDefaultAsync(p => p.Id == plugin.Id);
        Assert.NotNull(persisted);
        Assert.Equal("private", persisted.Visibility);
        Assert.Equal(org.Id, persisted.OwnerOrgId);
    }

    // -------------------------------------------------------------------------
    // Test CV4 — public plugin with a non-NULL owner_org_id → ACCEPTED
    //           An org can own a public plugin (org decides to publish publicly)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Insert_PublicPluginWithOwnerOrgId_Succeeds()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        UserEntity owner = MakeUser("public-org-owner@example.com");
        ctx.Users.Add(owner);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, "PublicOrgPlugin");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        PluginEntity plugin = new()
        {
            Id = Guid.NewGuid(),
            Name = "PublicOrgOwnedPlugin",
            NameNormalized = "publicorgownedplugin",
            Slug = "public-org-cv4",
            Description = "Public plugin owned by an org",
            Author = "test-author",
            DownloadCount = 0,
            Visibility = "public",
            OwnerOrgId = org.Id,        // satisfies CHECK (redundantly — public always passes)
            OwnerUserId = owner.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        ctx.Plugins.Add(plugin);
        await ctx.SaveChangesAsync(); // Must NOT throw

        PluginEntity? persisted = await ctx.Plugins.AsNoTracking().FirstOrDefaultAsync(p => p.Id == plugin.Id);
        Assert.NotNull(persisted);
        Assert.Equal("public", persisted.Visibility);
        Assert.Equal(org.Id, persisted.OwnerOrgId);
    }

    // =========================================================================
    // Additive column defaults
    // =========================================================================

    // -------------------------------------------------------------------------
    // Test DEF1 — visibility defaults to 'public' when not specified explicitly
    //             Simulates what existing legacy rows would get from DEFAULT
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Insert_PluginWithoutExplicitVisibility_DefaultsToPublic()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        // Insert via raw SQL to bypass EF and test the DB-level DEFAULT directly
        Guid pluginId = Guid.NewGuid();
        await ctx.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO plugins (id, name, name_normalized, slug, description, author, download_count, created_at, updated_at)
            VALUES ({0}, {1}, {2}, {3}, {4}, {5}, 0, now(), now())
            """,
            pluginId, "DefaultVisPlugin", "defaultvisplugin", "default-vis-def1",
            "Plugin without explicit visibility", "test-author");

        // Re-read and assert visibility defaulted to 'public'
        PluginEntity? persisted = await ctx.Plugins.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pluginId);
        Assert.NotNull(persisted);
        Assert.Equal("public", persisted.Visibility);
        Assert.Null(persisted.OwnerOrgId);
        Assert.Null(persisted.OwnerUserId);
    }

    // -------------------------------------------------------------------------
    // Test DEF2 — existing plugin rows are not broken by the additive migration
    //             owner_org_id and owner_user_id are nullable; public + null is valid
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExistingPlugin_HasPublicVisibilityAndNullOwners_AfterMigration()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        // Represents a legacy plugin row that existed before the auth migration
        PluginEntity legacy = MakePlugin("LegacyPlugin", "legacy-plugin-def2");
        // Explicitly confirm the defaults the migration sets on existing rows
        Assert.Equal("public", legacy.Visibility);
        Assert.Null(legacy.OwnerOrgId);
        Assert.Null(legacy.OwnerUserId);

        ctx.Plugins.Add(legacy);
        await ctx.SaveChangesAsync(); // Must NOT throw

        PluginEntity? persisted = await ctx.Plugins.AsNoTracking().FirstOrDefaultAsync(p => p.Id == legacy.Id);
        Assert.NotNull(persisted);
        Assert.Equal("public", persisted.Visibility);
        Assert.Null(persisted.OwnerOrgId);
        Assert.Null(persisted.OwnerUserId);
    }

    // -------------------------------------------------------------------------
    // Test DEF3 — idx_plugins_visibility_org composite index
    //             Both columns (visibility, owner_org_id) are queryable together
    //             Verify correct data retrieval using the indexed predicate
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Query_PluginsByVisibilityAndOwnerOrgId_ReturnsCorrectResults()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        UserEntity owner = MakeUser("index-query-owner@example.com");
        ctx.Users.Add(owner);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, "IndexQueryOrg");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        PluginEntity publicPlugin = MakePlugin("PublicIndexPlugin", "public-index-def3");

        PluginEntity privatePlugin = new()
        {
            Id = Guid.NewGuid(),
            Name = "PrivateIndexPlugin",
            NameNormalized = "privateindexplugin",
            Slug = "private-index-def3",
            Description = "Private plugin for index test",
            Author = "test-author",
            DownloadCount = 0,
            Visibility = "private",
            OwnerOrgId = org.Id,
            OwnerUserId = owner.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        ctx.Plugins.AddRange(publicPlugin, privatePlugin);
        await ctx.SaveChangesAsync();

        // Simulate the visibility filter predicate from design.md:
        // "WHERE plugins.visibility='public' OR plugins.owner_org_id = ANY(@viewerOrgIds)"
        Guid[] viewerOrgIds = [org.Id];
        List<Guid> visibleToMember = await ctx.Plugins
            .Where(p => p.Visibility == "public" || viewerOrgIds.Contains(p.OwnerOrgId!.Value))
            .Select(p => p.Id)
            .ToListAsync();

        // Anonymous user: public only
        List<Guid> visibleToAnon = await ctx.Plugins
            .Where(p => p.Visibility == "public")
            .Select(p => p.Id)
            .ToListAsync();

        Assert.Contains(publicPlugin.Id, visibleToMember);
        Assert.Contains(privatePlugin.Id, visibleToMember);

        Assert.Contains(publicPlugin.Id, visibleToAnon);
        Assert.DoesNotContain(privatePlugin.Id, visibleToAnon);
    }
}
