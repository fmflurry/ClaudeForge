using ClaudeForge.Core.Shared.Authorization;
using ClaudeForge.Infrastructure.Authorization;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using ClaudeForge.Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ClaudeForge.Tests.Unit.Authorization;

/// <summary>
/// Unit / integration tests for Group 2, Task 2.4 — OrgMembershipQueryAdapter.
///
/// These tests are RED because the production types do not yet exist.
/// The coder MUST create:
///
///   NAMESPACE: ClaudeForge.Core.Shared.Authorization
///
///   interface IOrgMembershipQueryPort
///     /// Returns the set of org-ids the given user belongs to.
///     /// Returns an empty array when the user has no memberships.
///     Task&lt;Guid[]&gt; GetOrgIdsForUserAsync(Guid userId, CancellationToken ct = default)
///
///     /// Returns true when the user is a member of the org with at least minRole (default = member).
///     /// minRole hierarchy (lowest to highest): member &lt; admin &lt; owner.
///     Task&lt;bool&gt; IsMemberAsync(Guid userId, Guid orgId, string? minRole = null, CancellationToken ct = default)
///
///   NAMESPACE: ClaudeForge.Infrastructure.Authorization
///
///   sealed class OrgMembershipQueryAdapter : IOrgMembershipQueryPort
///     OrgMembershipQueryAdapter(
///         IDbContextFactory&lt;MarketplaceDbContext&gt; dbFactory,
///         IMemoryCache cache)
///
///     /// Cache key pattern: "org-membership:{userId}"
///     /// Cache TTL: 30–60 seconds (the adapter may choose any value in this range)
///
///     /// Exposes the invalidation hook so membership-mutation use-cases can clear a user's cache.
///     void InvalidateUser(Guid userId)
///
/// Constraints (from design.md §3 and tasks.md §2.4):
///   - GetOrgIdsForUserAsync returns Guid[] only (no Organization domain types in signature)
///   - IsMemberAsync returns bool only
///   - 30–60s in-memory cache keyed by userId
///   - InvalidateUser removes the cached entry immediately
///
/// Tests in this file avoid Docker/Postgres by using IDbContextFactory that wraps
/// an in-memory context backed by a fake membership store (seeded directly).
/// Where a real EF context is available (via PostgresFixture), the adapter tests
/// run against the real schema using the existing fixture pattern.
/// </summary>

// ---------------------------------------------------------------------------
// Pure unit tests — in-memory context factory, no Docker needed
// ---------------------------------------------------------------------------

public sealed class OrgMembershipQueryAdapterUnitTests
{
    // =========================================================================
    // Helpers
    // =========================================================================

    private static IDbContextFactory<MarketplaceDbContext> MakeInMemoryFactory(string dbName)
    {
        DbContextOptions<MarketplaceDbContext> options =
            new DbContextOptionsBuilder<MarketplaceDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;

        return new SingletonDbContextFactory(options);
    }

    private static OrganizationMemberEntity MakeMembership(Guid userId, Guid orgId, string role = "member") => new()
    {
        UserId = userId,
        OrgId = orgId,
        Role = role,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private static UserEntity MakeUser() => new()
    {
        Id = Guid.NewGuid(),
        Email = "test@example.com",
        EmailNormalized = "test@example.com",
        DisplayName = "Test",
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private static OrganizationEntity MakeOrg(Guid createdBy) => new()
    {
        Id = Guid.NewGuid(),
        Name = "TestOrg",
        NameNormalized = "testorg",
        Slug = "testorg",
        CreatedBy = createdBy,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private sealed class SingletonDbContextFactory : IDbContextFactory<MarketplaceDbContext>
    {
        private readonly DbContextOptions<MarketplaceDbContext> _options;

        public SingletonDbContextFactory(DbContextOptions<MarketplaceDbContext> options)
        {
            _options = options;
        }

        public MarketplaceDbContext CreateDbContext() => new(_options);
    }

    private static (OrgMembershipQueryAdapter adapter, IMemoryCache cache) MakeAdapter(
        IDbContextFactory<MarketplaceDbContext> factory)
    {
        IMemoryCache cache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
        OrgMembershipQueryAdapter adapter = new(factory, cache);
        return (adapter, cache);
    }

    // =========================================================================
    // GetOrgIdsForUserAsync — cache miss queries the store
    // =========================================================================

    [Fact]
    public async Task GetOrgIdsForUser_CacheMiss_QueriesDbAndReturnsOrgIds()
    {
        // Arrange
        string dbName = $"GetOrgIds_Miss_{Guid.NewGuid()}";
        IDbContextFactory<MarketplaceDbContext> factory = MakeInMemoryFactory(dbName);

        await using (MarketplaceDbContext ctx = factory.CreateDbContext())
        {
            UserEntity user = MakeUser();
            await ctx.Users.AddAsync(user);
            await ctx.SaveChangesAsync();

            OrganizationEntity org = MakeOrg(user.Id);
            await ctx.Organizations.AddAsync(org);
            await ctx.SaveChangesAsync();

            ctx.OrganizationMembers.Add(MakeMembership(user.Id, org.Id));
            await ctx.SaveChangesAsync();

            (OrgMembershipQueryAdapter adapter, _) = MakeAdapter(factory);

            // Act
            Guid[] result = await adapter.GetOrgIdsForUserAsync(user.Id);

            // Assert
            Assert.Single(result);
            Assert.Contains(org.Id, result);
        }
    }

    [Fact]
    public async Task GetOrgIdsForUser_NoMemberships_ReturnsEmptyArray()
    {
        // Arrange
        string dbName = $"GetOrgIds_Empty_{Guid.NewGuid()}";
        IDbContextFactory<MarketplaceDbContext> factory = MakeInMemoryFactory(dbName);

        await using (MarketplaceDbContext ctx = factory.CreateDbContext())
        {
            UserEntity user = MakeUser();
            await ctx.Users.AddAsync(user);
            await ctx.SaveChangesAsync();

            (OrgMembershipQueryAdapter adapter, _) = MakeAdapter(factory);

            // Act
            Guid[] result = await adapter.GetOrgIdsForUserAsync(user.Id);

            // Assert
            Assert.Empty(result);
        }
    }

    [Fact]
    public async Task GetOrgIdsForUser_UnknownUser_ReturnsEmptyArray()
    {
        // Arrange — unknown user (no rows) should return empty, not throw
        string dbName = $"GetOrgIds_Unknown_{Guid.NewGuid()}";
        IDbContextFactory<MarketplaceDbContext> factory = MakeInMemoryFactory(dbName);
        (OrgMembershipQueryAdapter adapter, _) = MakeAdapter(factory);

        // Act
        Guid[] result = await adapter.GetOrgIdsForUserAsync(Guid.NewGuid());

        // Assert — no exception; just empty
        Assert.Empty(result);
    }

    // =========================================================================
    // GetOrgIdsForUserAsync — cache hit returns cached value without re-querying
    // =========================================================================

    [Fact]
    public async Task GetOrgIdsForUser_CacheHit_ReturnsCachedValueWithoutQuery()
    {
        // Arrange — seed two memberships; first call caches; then mutate DB directly;
        // second call must return stale cached value (cache hit)
        string dbName = $"GetOrgIds_CacheHit_{Guid.NewGuid()}";
        IDbContextFactory<MarketplaceDbContext> factory = MakeInMemoryFactory(dbName);

        Guid userId = Guid.Empty; // will be set below
        Guid orgId1 = Guid.NewGuid();
        Guid orgId2 = Guid.NewGuid();

        await using (MarketplaceDbContext seedCtx = factory.CreateDbContext())
        {
            UserEntity user = MakeUser();
            userId = user.Id;
            await seedCtx.Users.AddAsync(user);
            await seedCtx.SaveChangesAsync();

            OrganizationEntity org1 = new()
            {
                Id = orgId1,
                Name = "OrgOne",
                NameNormalized = "orgone",
                Slug = "orgone",
                CreatedBy = user.Id,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await seedCtx.Organizations.AddAsync(org1);
            await seedCtx.SaveChangesAsync();

            seedCtx.OrganizationMembers.Add(MakeMembership(user.Id, orgId1));
            await seedCtx.SaveChangesAsync();
        }

        (OrgMembershipQueryAdapter adapter, _) = MakeAdapter(factory);

        // First call — cache miss — should return [orgId1]
        Guid[] firstResult = await adapter.GetOrgIdsForUserAsync(userId);
        Assert.Single(firstResult);
        Assert.Contains(orgId1, firstResult);

        // Mutate the DB directly (add a second membership) — adapter should NOT see this
        await using (MarketplaceDbContext mutCtx = factory.CreateDbContext())
        {
            OrganizationEntity org2 = new()
            {
                Id = orgId2,
                Name = "OrgTwo",
                NameNormalized = "orgtwo",
                Slug = "orgtwo",
                CreatedBy = userId,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await mutCtx.Organizations.AddAsync(org2);
            await mutCtx.SaveChangesAsync();

            mutCtx.OrganizationMembers.Add(MakeMembership(userId, orgId2));
            await mutCtx.SaveChangesAsync();
        }

        // Second call — cache hit — must return the stale result (only orgId1)
        Guid[] secondResult = await adapter.GetOrgIdsForUserAsync(userId);

        Assert.Single(secondResult);
        Assert.Contains(orgId1, secondResult);
        Assert.DoesNotContain(orgId2, secondResult);
    }

    // =========================================================================
    // GetOrgIdsForUserAsync — TTL window (30–60 seconds)
    // =========================================================================

    [Fact]
    public void Adapter_CacheTtl_IsWithin30To60SecondWindow()
    {
        // Arrange — inspect the adapter's declared TTL constant or property.
        // This is a design-contract test: the adapter MUST expose its TTL as a
        // static or instance property so the test can assert the spec range.
        // Expected: OrgMembershipQueryAdapter.CacheTtl is in [30s, 60s].
        TimeSpan ttl = OrgMembershipQueryAdapter.CacheTtl;

        // Assert — design.md §1 "30–60s in-memory cache"
        Assert.True(ttl >= TimeSpan.FromSeconds(30),
            $"Cache TTL {ttl.TotalSeconds}s is below the 30s minimum specified in design.md");
        Assert.True(ttl <= TimeSpan.FromSeconds(60),
            $"Cache TTL {ttl.TotalSeconds}s exceeds the 60s maximum specified in design.md");
    }

    // =========================================================================
    // InvalidateUser — clears a user's cached entry
    // =========================================================================

    [Fact]
    public async Task InvalidateUser_ClearsCache_SubsequentCallQueriesDb()
    {
        // Arrange — seed one membership, prime the cache via first call, then invalidate,
        // then add a second membership via DB, expect the next call to see the new state.
        string dbName = $"InvalidateUser_{Guid.NewGuid()}";
        IDbContextFactory<MarketplaceDbContext> factory = MakeInMemoryFactory(dbName);

        Guid userId = Guid.Empty;
        Guid orgId1 = Guid.NewGuid();
        Guid orgId2 = Guid.NewGuid();

        await using (MarketplaceDbContext seedCtx = factory.CreateDbContext())
        {
            UserEntity user = MakeUser();
            userId = user.Id;
            await seedCtx.Users.AddAsync(user);
            await seedCtx.SaveChangesAsync();

            OrganizationEntity org1 = new()
            {
                Id = orgId1,
                Name = "OrgInvalidate1",
                NameNormalized = "orginvalidate1",
                Slug = "orginvalidate1",
                CreatedBy = user.Id,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await seedCtx.Organizations.AddAsync(org1);
            await seedCtx.SaveChangesAsync();

            seedCtx.OrganizationMembers.Add(MakeMembership(user.Id, orgId1));
            await seedCtx.SaveChangesAsync();
        }

        (OrgMembershipQueryAdapter adapter, _) = MakeAdapter(factory);

        // Prime the cache
        Guid[] before = await adapter.GetOrgIdsForUserAsync(userId);
        Assert.Single(before);

        // Add a new membership to the DB
        await using (MarketplaceDbContext mutCtx = factory.CreateDbContext())
        {
            OrganizationEntity org2 = new()
            {
                Id = orgId2,
                Name = "OrgInvalidate2",
                NameNormalized = "orginvalidate2",
                Slug = "orginvalidate2",
                CreatedBy = userId,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await mutCtx.Organizations.AddAsync(org2);
            await mutCtx.SaveChangesAsync();

            mutCtx.OrganizationMembers.Add(MakeMembership(userId, orgId2));
            await mutCtx.SaveChangesAsync();
        }

        // Act — invalidate the cache entry for this user
        adapter.InvalidateUser(userId);

        // Now the adapter should re-query and see both orgs
        Guid[] after = await adapter.GetOrgIdsForUserAsync(userId);

        // Assert
        Assert.Equal(2, after.Length);
        Assert.Contains(orgId1, after);
        Assert.Contains(orgId2, after);
    }

    [Fact]
    public async Task InvalidateUser_OnlyInvalidatesTargetUser_NotOtherUsers()
    {
        // Arrange — two users in cache; invalidate only userA; userB's cache must survive
        string dbName = $"InvalidateUser_Isolation_{Guid.NewGuid()}";
        IDbContextFactory<MarketplaceDbContext> factory = MakeInMemoryFactory(dbName);

        Guid userAId = Guid.Empty;
        Guid userBId = Guid.Empty;
        Guid orgA = Guid.NewGuid();
        Guid orgB = Guid.NewGuid();

        await using (MarketplaceDbContext seedCtx = factory.CreateDbContext())
        {
            UserEntity userA = MakeUser();
            userAId = userA.Id;
            UserEntity userB = new()
            {
                Id = Guid.NewGuid(),
                Email = "b@example.com",
                EmailNormalized = "b@example.com",
                DisplayName = "UserB",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            userBId = userB.Id;

            await seedCtx.Users.AddRangeAsync(userA, userB);
            await seedCtx.SaveChangesAsync();

            OrganizationEntity o1 = new()
            {
                Id = orgA,
                Name = "OrgA",
                NameNormalized = "orga",
                Slug = "orga",
                CreatedBy = userAId,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            OrganizationEntity o2 = new()
            {
                Id = orgB,
                Name = "OrgB",
                NameNormalized = "orgb",
                Slug = "orgb",
                CreatedBy = userBId,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await seedCtx.Organizations.AddRangeAsync(o1, o2);
            await seedCtx.SaveChangesAsync();

            seedCtx.OrganizationMembers.Add(MakeMembership(userAId, orgA));
            seedCtx.OrganizationMembers.Add(MakeMembership(userBId, orgB));
            await seedCtx.SaveChangesAsync();
        }

        (OrgMembershipQueryAdapter adapter, _) = MakeAdapter(factory);

        // Prime cache for both users
        Guid[] aOrgsBefore = await adapter.GetOrgIdsForUserAsync(userAId);
        Guid[] bOrgsBefore = await adapter.GetOrgIdsForUserAsync(userBId);
        Assert.Single(aOrgsBefore);
        Assert.Single(bOrgsBefore);

        // Invalidate only userA
        adapter.InvalidateUser(userAId);

        // After invalidation: userB's cache entry is still intact
        // Mutate DB for both users — userB's mutation should NOT be seen (still cached)
        await using (MarketplaceDbContext mutCtx = factory.CreateDbContext())
        {
            Guid orgC = Guid.NewGuid();
            OrganizationEntity o3 = new()
            {
                Id = orgC,
                Name = "OrgC",
                NameNormalized = "orgc",
                Slug = "orgc",
                CreatedBy = userBId,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await mutCtx.Organizations.AddAsync(o3);
            await mutCtx.SaveChangesAsync();

            mutCtx.OrganizationMembers.Add(MakeMembership(userBId, orgC));
            await mutCtx.SaveChangesAsync();
        }

        Guid[] bOrgsAfterInvalidateA = await adapter.GetOrgIdsForUserAsync(userBId);

        // UserB's cache was NOT invalidated — still returns the original single org
        Assert.Single(bOrgsAfterInvalidateA);
        Assert.Contains(orgB, bOrgsAfterInvalidateA);
    }

    // =========================================================================
    // IsMemberAsync — correct membership lookup
    // =========================================================================

    [Fact]
    public async Task IsMember_UserInOrg_ReturnsTrue()
    {
        string dbName = $"IsMember_True_{Guid.NewGuid()}";
        IDbContextFactory<MarketplaceDbContext> factory = MakeInMemoryFactory(dbName);

        await using MarketplaceDbContext ctx = factory.CreateDbContext();
        UserEntity user = MakeUser();
        await ctx.Users.AddAsync(user);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(user.Id);
        await ctx.Organizations.AddAsync(org);
        await ctx.SaveChangesAsync();

        ctx.OrganizationMembers.Add(MakeMembership(user.Id, org.Id, "member"));
        await ctx.SaveChangesAsync();

        (OrgMembershipQueryAdapter adapter, _) = MakeAdapter(factory);

        bool isMember = await adapter.IsMemberAsync(user.Id, org.Id);

        Assert.True(isMember);
    }

    [Fact]
    public async Task IsMember_UserNotInOrg_ReturnsFalse()
    {
        string dbName = $"IsMember_False_{Guid.NewGuid()}";
        IDbContextFactory<MarketplaceDbContext> factory = MakeInMemoryFactory(dbName);

        await using MarketplaceDbContext ctx = factory.CreateDbContext();
        UserEntity user = MakeUser();
        await ctx.Users.AddAsync(user);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(user.Id);
        await ctx.Organizations.AddAsync(org);
        await ctx.SaveChangesAsync();
        // No membership record

        (OrgMembershipQueryAdapter adapter, _) = MakeAdapter(factory);

        bool isMember = await adapter.IsMemberAsync(user.Id, org.Id);

        Assert.False(isMember);
    }

    [Fact]
    public async Task IsMember_UnknownUser_ReturnsFalse()
    {
        string dbName = $"IsMember_UnknownUser_{Guid.NewGuid()}";
        IDbContextFactory<MarketplaceDbContext> factory = MakeInMemoryFactory(dbName);
        (OrgMembershipQueryAdapter adapter, _) = MakeAdapter(factory);

        bool isMember = await adapter.IsMemberAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(isMember);
    }

    // =========================================================================
    // IsMemberAsync — minRole hierarchy enforcement
    // Role hierarchy (lowest→highest): member < admin < owner
    // =========================================================================

    [Fact]
    public async Task IsMember_MinRoleMember_UserIsOwner_ReturnsTrue()
    {
        // owner satisfies minRole=member because owner >= member
        string dbName = $"IsMember_MinRole_Owner_{Guid.NewGuid()}";
        IDbContextFactory<MarketplaceDbContext> factory = MakeInMemoryFactory(dbName);

        await using MarketplaceDbContext ctx = factory.CreateDbContext();
        UserEntity user = MakeUser();
        await ctx.Users.AddAsync(user);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(user.Id);
        await ctx.Organizations.AddAsync(org);
        await ctx.SaveChangesAsync();

        ctx.OrganizationMembers.Add(MakeMembership(user.Id, org.Id, "owner"));
        await ctx.SaveChangesAsync();

        (OrgMembershipQueryAdapter adapter, _) = MakeAdapter(factory);

        bool result = await adapter.IsMemberAsync(user.Id, org.Id, minRole: "member");

        Assert.True(result);
    }

    [Fact]
    public async Task IsMember_MinRoleAdmin_UserIsMember_ReturnsFalse()
    {
        // member does NOT satisfy minRole=admin
        string dbName = $"IsMember_MinRole_Admin_IsMember_{Guid.NewGuid()}";
        IDbContextFactory<MarketplaceDbContext> factory = MakeInMemoryFactory(dbName);

        await using MarketplaceDbContext ctx = factory.CreateDbContext();
        UserEntity user = MakeUser();
        await ctx.Users.AddAsync(user);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(user.Id);
        await ctx.Organizations.AddAsync(org);
        await ctx.SaveChangesAsync();

        ctx.OrganizationMembers.Add(MakeMembership(user.Id, org.Id, "member")); // member < admin
        await ctx.SaveChangesAsync();

        (OrgMembershipQueryAdapter adapter, _) = MakeAdapter(factory);

        bool result = await adapter.IsMemberAsync(user.Id, org.Id, minRole: "admin");

        Assert.False(result);
    }

    [Fact]
    public async Task IsMember_MinRoleOwner_UserIsAdmin_ReturnsFalse()
    {
        // admin does NOT satisfy minRole=owner
        string dbName = $"IsMember_MinRole_Owner_IsAdmin_{Guid.NewGuid()}";
        IDbContextFactory<MarketplaceDbContext> factory = MakeInMemoryFactory(dbName);

        await using MarketplaceDbContext ctx = factory.CreateDbContext();
        UserEntity user = MakeUser();
        await ctx.Users.AddAsync(user);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(user.Id);
        await ctx.Organizations.AddAsync(org);
        await ctx.SaveChangesAsync();

        ctx.OrganizationMembers.Add(MakeMembership(user.Id, org.Id, "admin")); // admin < owner
        await ctx.SaveChangesAsync();

        (OrgMembershipQueryAdapter adapter, _) = MakeAdapter(factory);

        bool result = await adapter.IsMemberAsync(user.Id, org.Id, minRole: "owner");

        Assert.False(result);
    }

    [Fact]
    public async Task IsMember_MinRoleAdmin_UserIsAdmin_ReturnsTrue()
    {
        // admin satisfies minRole=admin exactly
        string dbName = $"IsMember_MinRole_Admin_Exact_{Guid.NewGuid()}";
        IDbContextFactory<MarketplaceDbContext> factory = MakeInMemoryFactory(dbName);

        await using MarketplaceDbContext ctx = factory.CreateDbContext();
        UserEntity user = MakeUser();
        await ctx.Users.AddAsync(user);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(user.Id);
        await ctx.Organizations.AddAsync(org);
        await ctx.SaveChangesAsync();

        ctx.OrganizationMembers.Add(MakeMembership(user.Id, org.Id, "admin"));
        await ctx.SaveChangesAsync();

        (OrgMembershipQueryAdapter adapter, _) = MakeAdapter(factory);

        bool result = await adapter.IsMemberAsync(user.Id, org.Id, minRole: "admin");

        Assert.True(result);
    }

    // =========================================================================
    // Return type constraint — primitives only, no Organization domain types
    // =========================================================================

    [Fact]
    public void GetOrgIdsForUser_ReturnType_IsGuidArray()
    {
        // Reflection-based: the return type of GetOrgIdsForUserAsync must be Task<Guid[]>
        System.Reflection.MethodInfo? method =
            typeof(IOrgMembershipQueryPort).GetMethod(nameof(IOrgMembershipQueryPort.GetOrgIdsForUserAsync));

        Assert.NotNull(method);

        // Return type is Task<Guid[]>
        Type returnType = method!.ReturnType;
        Assert.True(returnType.IsGenericType, "Return type must be generic (Task<T>)");
        Type[] args = returnType.GetGenericArguments();
        Assert.Single(args);
        Assert.Equal(typeof(Guid[]), args[0]);
    }

    [Fact]
    public void IsMember_ReturnType_IsBool()
    {
        // Reflection-based: the return type of IsMemberAsync must be Task<bool>
        System.Reflection.MethodInfo? method =
            typeof(IOrgMembershipQueryPort).GetMethod(nameof(IOrgMembershipQueryPort.IsMemberAsync));

        Assert.NotNull(method);

        Type returnType = method!.ReturnType;
        Assert.True(returnType.IsGenericType, "Return type must be generic (Task<T>)");
        Type[] args = returnType.GetGenericArguments();
        Assert.Single(args);
        Assert.Equal(typeof(bool), args[0]);
    }

    [Fact]
    public void IOrgMembershipQueryPort_MethodSignatures_ContainNoDomainTypes()
    {
        // Reflection-based: no parameter or return type from Organizations domain namespace
        // allowed in IOrgMembershipQueryPort per design.md §3 "return primitives only"
        string forbiddenNamespace = "ClaudeForge.Core.Modules.Organizations";

        System.Reflection.MethodInfo[] methods = typeof(IOrgMembershipQueryPort).GetMethods();
        foreach (System.Reflection.MethodInfo method in methods)
        {
            // Check return type generic args
            foreach (Type argType in method.ReturnType.GetGenericArguments())
            {
                Assert.False(
                    argType.Namespace?.StartsWith(forbiddenNamespace, StringComparison.Ordinal) ?? false,
                    $"Method {method.Name} return type includes Organizations domain type {argType.FullName}");
            }

            // Check parameter types
            foreach (System.Reflection.ParameterInfo param in method.GetParameters())
            {
                Type paramType = param.ParameterType;
                Assert.False(
                    paramType.Namespace?.StartsWith(forbiddenNamespace, StringComparison.Ordinal) ?? false,
                    $"Method {method.Name} parameter '{param.Name}' is an Organizations domain type {paramType.FullName}");
            }
        }
    }
}
