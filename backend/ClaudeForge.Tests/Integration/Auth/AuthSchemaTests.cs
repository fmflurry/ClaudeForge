using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using ClaudeForge.Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ClaudeForge.Tests.Integration.Auth;

/// <summary>
/// Integration tests for Group 1, Task 1.2:
/// EF entity mappings for the seven new auth tables.
///
/// These tests run against a REAL PostgreSQL 16 container via Testcontainers.
/// Docker must be running on the test host.
///
/// All tests in this file are RED because the production types listed below
/// do not yet exist. The coder must create them to make the tests GREEN.
///
/// Expected production types (coder must match these names exactly):
///
///   ClaudeForge.Infrastructure.Persistence.MarketplaceDbContext
///     DbSet&lt;UserEntity&gt;                  Users
///     DbSet&lt;UserIdentityEntity&gt;          UserIdentities
///     DbSet&lt;OrganizationEntity&gt;          Organizations
///     DbSet&lt;OrganizationMemberEntity&gt;    OrganizationMembers
///     DbSet&lt;OrganizationInvitationEntity&gt; OrganizationInvitations
///     DbSet&lt;RefreshTokenEntity&gt;          RefreshTokens
///     DbSet&lt;OrgAuditEntryEntity&gt;         OrgAuditLog
///
///   ClaudeForge.Infrastructure.Persistence.Entities.UserEntity
///     Guid           Id                 (PK, gen_random_uuid())
///     string         Email              (NOT NULL)
///     string         EmailNormalized    (NOT NULL, UNIQUE)
///     string         DisplayName        (NOT NULL)
///     DateTimeOffset CreatedAt          (NOT NULL, DEFAULT now())
///     DateTimeOffset UpdatedAt          (NOT NULL, DEFAULT now())
///     DateTimeOffset? DeletedAt         (nullable)
///     ICollection&lt;UserIdentityEntity&gt;          Identities
///     ICollection&lt;OrganizationMemberEntity&gt;    Memberships
///     ICollection&lt;OrganizationInvitationEntity&gt; SentInvitations
///     ICollection&lt;RefreshTokenEntity&gt;          RefreshTokens
///
///   ClaudeForge.Infrastructure.Persistence.Entities.UserIdentityEntity
///     Guid   Id       (PK)
///     Guid   UserId   (NOT NULL, FK → users ON DELETE CASCADE)
///     string Provider (NOT NULL)
///     string Subject  (NOT NULL)
///     DateTimeOffset CreatedAt
///     UNIQUE(Provider, Subject)
///     UserEntity User
///
///   ClaudeForge.Infrastructure.Persistence.Entities.OrganizationEntity
///     Guid   Id              (PK)
///     string Name            (NOT NULL)
///     string NameNormalized  (NOT NULL, UNIQUE)
///     string Slug            (NOT NULL, UNIQUE)
///     Guid   CreatedBy       (NOT NULL, FK → users)
///     DateTimeOffset CreatedAt
///     ICollection&lt;OrganizationMemberEntity&gt;    Members
///     ICollection&lt;OrganizationInvitationEntity&gt; Invitations
///     ICollection&lt;OrgAuditEntryEntity&gt;         AuditLog
///
///   ClaudeForge.Infrastructure.Persistence.Entities.OrganizationMemberEntity
///     Guid   OrgId     (PK composite, FK → organizations ON DELETE CASCADE)
///     Guid   UserId    (PK composite, FK → users ON DELETE CASCADE)
///     string Role      (NOT NULL — "owner"|"admin"|"member")
///     DateTimeOffset CreatedAt
///     OrganizationEntity Organization
///     UserEntity User
///
///   ClaudeForge.Infrastructure.Persistence.Entities.OrganizationInvitationEntity
///     Guid   Id              (PK)
///     Guid   OrgId           (NOT NULL, FK → organizations ON DELETE CASCADE)
///     string EmailNormalized (NOT NULL)
///     Guid   InvitedBy       (NOT NULL, FK → users)
///     string Role            (NOT NULL, DEFAULT "member")
///     string Status          (NOT NULL, DEFAULT "pending" — "pending"|"accepted"|"revoked"|"expired")
///     string Token           (NOT NULL, UNIQUE)
///     DateTimeOffset  CreatedAt
///     DateTimeOffset  ExpiresAt  (NOT NULL)
///     DateTimeOffset? AcceptedAt (nullable)
///     DateTimeOffset? RevokedAt  (nullable)
///     Partial-UNIQUE(OrgId, EmailNormalized) WHERE Status='pending'
///     OrganizationEntity Organization
///     UserEntity InvitedByUser
///
///   ClaudeForge.Infrastructure.Persistence.Entities.RefreshTokenEntity
///     Guid   Id         (PK)
///     Guid   UserId     (NOT NULL, FK → users ON DELETE CASCADE)
///     string TokenHash  (NOT NULL, UNIQUE, CHAR(64))
///     DateTimeOffset  ExpiresAt  (NOT NULL)
///     DateTimeOffset? RevokedAt  (nullable)
///     Guid?  RotatedTo  (nullable, self-FK → refresh_tokens)
///     DateTimeOffset  CreatedAt
///     UserEntity User
///     RefreshTokenEntity? RotatedToToken (self-navigation)
///
///   ClaudeForge.Infrastructure.Persistence.Entities.OrgAuditEntryEntity
///     Guid   Id          (PK)
///     Guid   OrgId       (NOT NULL, FK → organizations ON DELETE CASCADE)
///     Guid   ActorUserId (NOT NULL, FK → users)
///     string Action      (NOT NULL)
///     string Target      (NOT NULL)
///     DateTimeOffset CreatedAt
///     OrganizationEntity Organization
///     UserEntity ActorUser
///
///   ClaudeForge.Core.Identity.OrgRole     (value object: Owner | Admin | Member)
///   ClaudeForge.Core.Shared.Visibility    (value object: Public | Private)
/// </summary>
[Collection(PostgresFixture.CollectionName)]
public sealed class AuthSchemaTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public AuthSchemaTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // -------------------------------------------------------------------------
    // Per-test isolation: truncate auth tables + plugins before each test.
    // Auth tables must be truncated in FK-safe order.
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
    // Helper factories (immutable creation, no mutation)
    // -------------------------------------------------------------------------

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

    private static OrganizationMemberEntity MakeMember(Guid orgId, Guid userId, string role) => new()
    {
        OrgId = orgId,
        UserId = userId,
        Role = role,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private static OrganizationInvitationEntity MakeInvitation(
        Guid orgId, Guid invitedBy, string emailNormalized, string token) => new()
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            EmailNormalized = emailNormalized,
            InvitedBy = invitedBy,
            Role = "member",
            Status = "pending",
            Token = token,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
        };

    private static RefreshTokenEntity MakeRefreshToken(Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        TokenHash = new string('a', 64),
        ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private static OrgAuditEntryEntity MakeAuditEntry(Guid orgId, Guid actorUserId) => new()
    {
        Id = Guid.NewGuid(),
        OrgId = orgId,
        ActorUserId = actorUserId,
        Action = "member.added",
        Target = "user:test@example.com",
        CreatedAt = DateTimeOffset.UtcNow,
    };

    // =========================================================================
    // USERS TABLE
    // =========================================================================

    // -------------------------------------------------------------------------
    // Test U1 — users.email_normalized UNIQUE violation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Insert_TwoUsersWithSameEmailNormalized_ThrowsUniqueViolation()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        UserEntity first = MakeUser("Alice@Example.Com");
        UserEntity second = new()
        {
            Id = Guid.NewGuid(),
            Email = "alice2@example.com",
            EmailNormalized = "alice@example.com", // same normalized
            DisplayName = "Alice2",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        ctx.Users.Add(first);
        await ctx.SaveChangesAsync();

        ctx.Users.Add(second);
        Exception ex = await Assert.ThrowsAnyAsync<Exception>(() => ctx.SaveChangesAsync());
        Assert.True(
            ex is DbUpdateException or PostgresException,
            $"Expected unique violation but got {ex.GetType().Name}: {ex.Message}");
    }

    // -------------------------------------------------------------------------
    // Test U2 — users required NOT NULL fields
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Insert_UserWithNullEmail_ThrowsException()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        UserEntity invalid = new()
        {
            Id = Guid.NewGuid(),
            Email = null!,
            EmailNormalized = "null-email-u2",
            DisplayName = "Test",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        ctx.Users.Add(invalid);
        await Assert.ThrowsAnyAsync<Exception>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Insert_UserWithNullDisplayName_ThrowsException()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        UserEntity invalid = new()
        {
            Id = Guid.NewGuid(),
            Email = "display-null@example.com",
            EmailNormalized = "display-null@example.com",
            DisplayName = null!,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        ctx.Users.Add(invalid);
        await Assert.ThrowsAnyAsync<Exception>(() => ctx.SaveChangesAsync());
    }

    // -------------------------------------------------------------------------
    // Test U3 — users.deleted_at is nullable (soft-delete support)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Insert_UserWithNullDeletedAt_Succeeds()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        UserEntity user = MakeUser("soft-delete-test@example.com");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        UserEntity? persisted = await ctx.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == user.Id);
        Assert.NotNull(persisted);
        Assert.Null(persisted!.DeletedAt);
    }

    // =========================================================================
    // USER_IDENTITIES TABLE
    // =========================================================================

    // -------------------------------------------------------------------------
    // Test UI1 — user_identities UNIQUE(provider, subject)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Insert_TwoUserIdentitiesWithSameProviderAndSubject_ThrowsUniqueViolation()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        UserEntity user = MakeUser("identity-test@example.com");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        UserIdentityEntity id1 = new()
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Provider = "google",
            Subject = "google-sub-123",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        UserIdentityEntity id2 = new()
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Provider = "google",    // same provider
            Subject = "google-sub-123", // same subject
            CreatedAt = DateTimeOffset.UtcNow,
        };

        ctx.UserIdentities.Add(id1);
        await ctx.SaveChangesAsync();

        ctx.UserIdentities.Add(id2);
        Exception ex = await Assert.ThrowsAnyAsync<Exception>(() => ctx.SaveChangesAsync());
        Assert.True(
            ex is DbUpdateException or PostgresException,
            $"Expected unique violation but got {ex.GetType().Name}: {ex.Message}");
    }

    // -------------------------------------------------------------------------
    // Test UI2 — user_identities ON DELETE CASCADE from users
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Delete_User_CascadesToUserIdentities()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        UserEntity user = MakeUser("cascade-identity@example.com");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        UserIdentityEntity identity = new()
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Provider = "google",
            Subject = "cascade-sub-001",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        ctx.UserIdentities.Add(identity);
        await ctx.SaveChangesAsync();

        ctx.Users.Remove(user);
        await ctx.SaveChangesAsync();

        bool identityExists = await ctx.UserIdentities.AnyAsync(i => i.UserId == user.Id);
        Assert.False(identityExists, "user_identities must be deleted when the user is deleted");
    }

    // -------------------------------------------------------------------------
    // Test UI3 — user_identities required NOT NULL fields
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Insert_UserIdentityWithNullProvider_ThrowsException()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        UserEntity user = MakeUser("identity-null-provider@example.com");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        UserIdentityEntity invalid = new()
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Provider = null!,
            Subject = "some-subject",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        ctx.UserIdentities.Add(invalid);
        await Assert.ThrowsAnyAsync<Exception>(() => ctx.SaveChangesAsync());
    }

    // =========================================================================
    // ORGANIZATIONS TABLE
    // =========================================================================

    // -------------------------------------------------------------------------
    // Test O1 — organizations.name_normalized UNIQUE violation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Insert_TwoOrgsWithSameNameNormalized_ThrowsUniqueViolation()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        UserEntity owner = MakeUser("org-owner@example.com");
        ctx.Users.Add(owner);
        await ctx.SaveChangesAsync();

        OrganizationEntity org1 = MakeOrg(owner.Id, "AcmeCorp");
        OrganizationEntity org2 = new()
        {
            Id = Guid.NewGuid(),
            Name = "ACMECORP",
            NameNormalized = "acmecorp", // same normalized
            Slug = "acmecorp-2",
            CreatedBy = owner.Id,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        ctx.Organizations.Add(org1);
        await ctx.SaveChangesAsync();

        ctx.Organizations.Add(org2);
        Exception ex = await Assert.ThrowsAnyAsync<Exception>(() => ctx.SaveChangesAsync());
        Assert.True(
            ex is DbUpdateException or PostgresException,
            $"Expected unique violation on name_normalized but got {ex.GetType().Name}: {ex.Message}");
    }

    // -------------------------------------------------------------------------
    // Test O2 — organizations.slug UNIQUE violation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Insert_TwoOrgsWithSameSlug_ThrowsUniqueViolation()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        UserEntity owner = MakeUser("slug-owner@example.com");
        ctx.Users.Add(owner);
        await ctx.SaveChangesAsync();

        OrganizationEntity org1 = new()
        {
            Id = Guid.NewGuid(),
            Name = "OrgAlpha",
            NameNormalized = "orgalpha",
            Slug = "shared-org-slug",
            CreatedBy = owner.Id,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        OrganizationEntity org2 = new()
        {
            Id = Guid.NewGuid(),
            Name = "OrgBeta",
            NameNormalized = "orgbeta",
            Slug = "shared-org-slug", // same slug
            CreatedBy = owner.Id,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        ctx.Organizations.Add(org1);
        await ctx.SaveChangesAsync();

        ctx.Organizations.Add(org2);
        Exception ex = await Assert.ThrowsAnyAsync<Exception>(() => ctx.SaveChangesAsync());
        Assert.True(
            ex is DbUpdateException or PostgresException,
            $"Expected unique violation on slug but got {ex.GetType().Name}: {ex.Message}");
    }

    // =========================================================================
    // ORGANIZATION_MEMBERS TABLE
    // =========================================================================

    // -------------------------------------------------------------------------
    // Test OM1 — organization_members composite PK prevents duplicates
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Insert_DuplicateOrgMember_ThrowsPrimaryKeyViolation()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        UserEntity owner = MakeUser("dup-member-owner@example.com");
        UserEntity member = MakeUser("dup-member-user@example.com");
        ctx.Users.AddRange(owner, member);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, "DupMemberOrg");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        OrganizationMemberEntity m1 = MakeMember(org.Id, member.Id, "member");
        ctx.OrganizationMembers.Add(m1);
        await ctx.SaveChangesAsync();

        OrganizationMemberEntity m2 = MakeMember(org.Id, member.Id, "admin"); // same PK
        ctx.OrganizationMembers.Add(m2);
        Exception ex = await Assert.ThrowsAnyAsync<Exception>(() => ctx.SaveChangesAsync());
        Assert.True(
            ex is DbUpdateException or PostgresException,
            $"Expected PK violation but got {ex.GetType().Name}: {ex.Message}");
    }

    // -------------------------------------------------------------------------
    // Test OM2 — organization_members ON DELETE CASCADE from organizations
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Delete_Organization_CascadesToMembers()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        UserEntity owner = MakeUser("cascade-org-owner@example.com");
        ctx.Users.Add(owner);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, "CascadeOrgMembers");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        OrganizationMemberEntity member = MakeMember(org.Id, owner.Id, "owner");
        ctx.OrganizationMembers.Add(member);
        await ctx.SaveChangesAsync();

        ctx.Organizations.Remove(org);
        await ctx.SaveChangesAsync();

        bool memberExists = await ctx.OrganizationMembers.AnyAsync(m => m.OrgId == org.Id);
        Assert.False(memberExists, "organization_members must be deleted when the org is deleted");
    }

    // -------------------------------------------------------------------------
    // Test OM3 — organization_members ON DELETE CASCADE from users
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Delete_User_CascadesToOrgMemberships()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        UserEntity owner = MakeUser("member-cascade-owner@example.com");
        UserEntity memberUser = MakeUser("member-cascade-member@example.com");
        ctx.Users.AddRange(owner, memberUser);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, "MemberCascadeOrg");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        OrganizationMemberEntity membership = MakeMember(org.Id, memberUser.Id, "member");
        ctx.OrganizationMembers.Add(membership);
        await ctx.SaveChangesAsync();

        ctx.Users.Remove(memberUser);
        await ctx.SaveChangesAsync();

        bool membershipExists = await ctx.OrganizationMembers.AnyAsync(m => m.UserId == memberUser.Id);
        Assert.False(membershipExists, "organization_members must be deleted when the user is deleted");
    }

    // =========================================================================
    // ORGANIZATION_INVITATIONS TABLE
    // =========================================================================

    // -------------------------------------------------------------------------
    // Test OI1 — organization_invitations.token UNIQUE violation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Insert_TwoInvitationsWithSameToken_ThrowsUniqueViolation()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        UserEntity owner = MakeUser("invite-token-owner@example.com");
        ctx.Users.Add(owner);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, "TokenDupOrg");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        OrganizationInvitationEntity inv1 = MakeInvitation(org.Id, owner.Id, "alice@example.com", "unique-token-001");
        OrganizationInvitationEntity inv2 = new()
        {
            Id = Guid.NewGuid(),
            OrgId = org.Id,
            EmailNormalized = "bob@example.com",
            InvitedBy = owner.Id,
            Role = "member",
            Status = "pending",
            Token = "unique-token-001", // same token
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
        };

        ctx.OrganizationInvitations.Add(inv1);
        await ctx.SaveChangesAsync();

        ctx.OrganizationInvitations.Add(inv2);
        Exception ex = await Assert.ThrowsAnyAsync<Exception>(() => ctx.SaveChangesAsync());
        Assert.True(
            ex is DbUpdateException or PostgresException,
            $"Expected unique violation on token but got {ex.GetType().Name}: {ex.Message}");
    }

    // -------------------------------------------------------------------------
    // Test OI2 — partial UNIQUE(org_id, email_normalized) WHERE status='pending'
    //           Two pending invites for the same email in the same org → violation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Insert_TwoPendingInvitationsForSameEmailInSameOrg_ThrowsPartialUniqueViolation()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        UserEntity owner = MakeUser("partial-unique-owner@example.com");
        ctx.Users.Add(owner);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, "PartialUniqueOrg");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        OrganizationInvitationEntity inv1 = MakeInvitation(org.Id, owner.Id, "alice@example.com", "token-partial-001");
        OrganizationInvitationEntity inv2 = MakeInvitation(org.Id, owner.Id, "alice@example.com", "token-partial-002"); // same email, same org, both pending

        ctx.OrganizationInvitations.Add(inv1);
        await ctx.SaveChangesAsync();

        ctx.OrganizationInvitations.Add(inv2);
        Exception ex = await Assert.ThrowsAnyAsync<Exception>(() => ctx.SaveChangesAsync());
        Assert.True(
            ex is DbUpdateException or PostgresException,
            $"Expected partial unique violation on pending invite but got {ex.GetType().Name}: {ex.Message}");
    }

    // -------------------------------------------------------------------------
    // Test OI3 — partial UNIQUE allows multiple non-pending invites for same email
    //           Accepted + new pending for same email in same org → ALLOWED
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Insert_AcceptedAndPendingInvitationsForSameEmail_Succeeds()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        UserEntity owner = MakeUser("accepted-then-pending-owner@example.com");
        ctx.Users.Add(owner);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, "AcceptedPendingOrg");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        OrganizationInvitationEntity accepted = new()
        {
            Id = Guid.NewGuid(),
            OrgId = org.Id,
            EmailNormalized = "charlie@example.com",
            InvitedBy = owner.Id,
            Role = "member",
            Status = "accepted", // NOT pending → partial unique does not apply
            Token = "token-accepted-001",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            AcceptedAt = DateTimeOffset.UtcNow,
        };

        OrganizationInvitationEntity pending = MakeInvitation(org.Id, owner.Id, "charlie@example.com", "token-pending-001");

        ctx.OrganizationInvitations.Add(accepted);
        await ctx.SaveChangesAsync();

        ctx.OrganizationInvitations.Add(pending);
        // Must NOT throw — partial unique only covers status='pending'
        await ctx.SaveChangesAsync();

        int count = await ctx.OrganizationInvitations
            .CountAsync(i => i.OrgId == org.Id && i.EmailNormalized == "charlie@example.com");
        Assert.Equal(2, count);
    }

    // -------------------------------------------------------------------------
    // Test OI4 — organization_invitations ON DELETE CASCADE from organizations
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Delete_Organization_CascadesToInvitations()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        UserEntity owner = MakeUser("cascade-invite-owner@example.com");
        ctx.Users.Add(owner);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, "CascadeInviteOrg");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        OrganizationInvitationEntity invite = MakeInvitation(org.Id, owner.Id, "dave@example.com", "token-cascade-001");
        ctx.OrganizationInvitations.Add(invite);
        await ctx.SaveChangesAsync();

        ctx.Organizations.Remove(org);
        await ctx.SaveChangesAsync();

        bool inviteExists = await ctx.OrganizationInvitations.AnyAsync(i => i.OrgId == org.Id);
        Assert.False(inviteExists, "organization_invitations must be deleted when the org is deleted");
    }

    // -------------------------------------------------------------------------
    // Test OI5 — invitation required NOT NULL fields (email_normalized, role, status, token)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Insert_InvitationWithNullToken_ThrowsException()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        UserEntity owner = MakeUser("invite-null-token@example.com");
        ctx.Users.Add(owner);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, "NullTokenOrg");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        OrganizationInvitationEntity invalid = new()
        {
            Id = Guid.NewGuid(),
            OrgId = org.Id,
            EmailNormalized = "test@example.com",
            InvitedBy = owner.Id,
            Role = "member",
            Status = "pending",
            Token = null!,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
        };

        ctx.OrganizationInvitations.Add(invalid);
        await Assert.ThrowsAnyAsync<Exception>(() => ctx.SaveChangesAsync());
    }

    // =========================================================================
    // REFRESH_TOKENS TABLE
    // =========================================================================

    // -------------------------------------------------------------------------
    // Test RT1 — refresh_tokens.token_hash UNIQUE violation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Insert_TwoRefreshTokensWithSameTokenHash_ThrowsUniqueViolation()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        UserEntity user = MakeUser("rt-hash-collision@example.com");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        RefreshTokenEntity rt1 = new()
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = new string('b', 64),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        RefreshTokenEntity rt2 = new()
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = new string('b', 64), // same hash
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        ctx.RefreshTokens.Add(rt1);
        await ctx.SaveChangesAsync();

        ctx.RefreshTokens.Add(rt2);
        Exception ex = await Assert.ThrowsAnyAsync<Exception>(() => ctx.SaveChangesAsync());
        Assert.True(
            ex is DbUpdateException or PostgresException,
            $"Expected unique violation on token_hash but got {ex.GetType().Name}: {ex.Message}");
    }

    // -------------------------------------------------------------------------
    // Test RT2 — refresh_tokens ON DELETE CASCADE from users
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Delete_User_CascadesToRefreshTokens()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        UserEntity user = MakeUser("rt-cascade-user@example.com");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        RefreshTokenEntity rt = MakeRefreshToken(user.Id);
        ctx.RefreshTokens.Add(rt);
        await ctx.SaveChangesAsync();

        ctx.Users.Remove(user);
        await ctx.SaveChangesAsync();

        bool rtExists = await ctx.RefreshTokens.AnyAsync(r => r.UserId == user.Id);
        Assert.False(rtExists, "refresh_tokens must be deleted when the user is deleted");
    }

    // -------------------------------------------------------------------------
    // Test RT3 — refresh_tokens.rotated_to self-FK
    //           A token can reference another refresh token as its rotated-to successor
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Insert_RefreshTokenWithRotatedToSelfFK_Succeeds()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        UserEntity user = MakeUser("rt-rotation@example.com");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        RefreshTokenEntity original = new()
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = new string('c', 64),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        RefreshTokenEntity rotated = new()
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = new string('d', 64),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        ctx.RefreshTokens.AddRange(original, rotated);
        await ctx.SaveChangesAsync();

        // Now set rotated_to on original to point to rotated
        RefreshTokenEntity? tracked = await ctx.RefreshTokens.FindAsync(original.Id);
        Assert.NotNull(tracked);
        tracked!.RotatedTo = rotated.Id;
        await ctx.SaveChangesAsync();

        RefreshTokenEntity? reloaded = await ctx.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == original.Id);

        Assert.NotNull(reloaded);
        Assert.Equal(rotated.Id, reloaded!.RotatedTo);
    }

    // -------------------------------------------------------------------------
    // Test RT4 — refresh_tokens.token_hash must be CHAR(64) — SHA-256 hex
    //           token_hash shorter than 64 chars should fail (column constraint)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Insert_RefreshTokenWithShortHash_ThrowsException()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        UserEntity user = MakeUser("rt-short-hash@example.com");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        RefreshTokenEntity invalid = new()
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = "tooshort", // must be 64 chars
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        ctx.RefreshTokens.Add(invalid);
        await Assert.ThrowsAnyAsync<Exception>(() => ctx.SaveChangesAsync());
    }

    // =========================================================================
    // ORG_AUDIT_LOG TABLE
    // =========================================================================

    // -------------------------------------------------------------------------
    // Test AL1 — org_audit_log ON DELETE CASCADE from organizations
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Delete_Organization_CascadesToAuditLog()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        UserEntity owner = MakeUser("audit-cascade-owner@example.com");
        ctx.Users.Add(owner);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, "AuditCascadeOrg");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        OrgAuditEntryEntity entry = MakeAuditEntry(org.Id, owner.Id);
        ctx.OrgAuditLog.Add(entry);
        await ctx.SaveChangesAsync();

        ctx.Organizations.Remove(org);
        await ctx.SaveChangesAsync();

        bool entryExists = await ctx.OrgAuditLog.AnyAsync(e => e.OrgId == org.Id);
        Assert.False(entryExists, "org_audit_log must be deleted when the org is deleted");
    }

    // -------------------------------------------------------------------------
    // Test AL2 — org_audit_log required NOT NULL fields
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Insert_AuditEntryWithNullAction_ThrowsException()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        UserEntity owner = MakeUser("audit-null-action@example.com");
        ctx.Users.Add(owner);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, "NullActionAuditOrg");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        OrgAuditEntryEntity invalid = new()
        {
            Id = Guid.NewGuid(),
            OrgId = org.Id,
            ActorUserId = owner.Id,
            Action = null!,
            Target = "some-target",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        ctx.OrgAuditLog.Add(invalid);
        await Assert.ThrowsAnyAsync<Exception>(() => ctx.SaveChangesAsync());
    }

    // =========================================================================
    // CROSS-TABLE — full lifecycle (happy path round-trip)
    // =========================================================================

    // -------------------------------------------------------------------------
    // Test LIFE1 — insert user → org → member → invite → refresh token → audit entry, all persisted
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FullAuthLifecycle_Insert_AllEntitiesPersisted()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();

        UserEntity owner = MakeUser("lifecycle@example.com");
        ctx.Users.Add(owner);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, "LifecycleOrg");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        OrganizationMemberEntity membership = MakeMember(org.Id, owner.Id, "owner");
        ctx.OrganizationMembers.Add(membership);

        OrganizationInvitationEntity invite = MakeInvitation(org.Id, owner.Id, "invited@example.com", "lifecycle-token-001");
        ctx.OrganizationInvitations.Add(invite);

        RefreshTokenEntity rt = MakeRefreshToken(owner.Id);
        ctx.RefreshTokens.Add(rt);

        OrgAuditEntryEntity audit = MakeAuditEntry(org.Id, owner.Id);
        ctx.OrgAuditLog.Add(audit);

        await ctx.SaveChangesAsync();

        Assert.True(await ctx.OrganizationMembers.AnyAsync(m => m.OrgId == org.Id && m.UserId == owner.Id));
        Assert.True(await ctx.OrganizationInvitations.AnyAsync(i => i.OrgId == org.Id));
        Assert.True(await ctx.RefreshTokens.AnyAsync(r => r.UserId == owner.Id));
        Assert.True(await ctx.OrgAuditLog.AnyAsync(e => e.OrgId == org.Id));
    }
}
