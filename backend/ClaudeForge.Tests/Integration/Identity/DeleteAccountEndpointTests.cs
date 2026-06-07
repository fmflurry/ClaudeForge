using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClaudeForge.Core.Identity.Ports;
using ClaudeForge.Core.Shared.Authorization;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using ClaudeForge.Tests.Integration.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeForge.Tests.Integration.Identity;

/// <summary>
/// Integration tests for Group 13, Task 13.2 — DELETE /auth/me endpoint.
///
/// These tests are RED. The coder MUST create:
///
///   NAMESPACE: ClaudeForge.Api.Modules.Identity  (or the IdentityModule)
///
///   DELETE /auth/me  [Authorize]
///     - Unauthenticated → 401
///     - Authenticated → 204 No Content
///     - Account deletion: soft-deletes user, revokes refresh tokens, removes memberships,
///       deletes sole-owner/no-other-members orgs
///     - Calls IOrgMembershipQueryPort.InvalidateUser (membership cache invalidated)
///     - A deleted user can no longer authenticate: GET /auth/me → 401
///       (FindByIdAsync returns null because user has deleted_at set, or the endpoint
///        checks user.DeletedAt and rejects; either implementation is valid)
///
///   Infrastructure adapters (new methods on existing adapters OR a new adapter):
///     IUserDeletionPort:
///       SoftDeleteUserAsync(Guid userId) → sets users.deleted_at = now()
///       RemoveAllMembershipsForUserAsync(Guid userId) → DELETEs organization_members WHERE user_id=userId
///       RevokeAllRefreshTokensForUserAsync(Guid userId) → sets revoked_at=now() WHERE user_id=userId AND revoked_at IS NULL
///     IOrgDeletionPort:
///       FindSoleOwnerOrgsWithNoOtherMembersAsync(Guid userId) → orgs where (1) user is sole owner (2) no other members exist
///       DeleteOrganizationAsync(Guid orgId) → hard-deletes the org (CASCADE removes members, invitations, audit log)
/// </summary>
[Collection(AuthEndpointFixture.CollectionName)]
public sealed class DeleteAccountEndpointTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private readonly WebApplicationFactory<Program> _factory;

    public DeleteAccountEndpointTests(PostgresFixture pg)
    {
        _pg = pg;
        _factory = AuthEndpointFixture.CreateFactory(pg);
    }

    public async Task InitializeAsync()
    {
        await using MarketplaceDbContext ctx = _pg.CreateContext();
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
        await _factory.DisposeAsync();
    }

    // =========================================================================
    // 13.2 — Unauthenticated → 401
    // =========================================================================

    [Fact]
    public async Task DeleteMe_Unauthenticated_Returns401()
    {
        // Arrange — no Bearer token on the request
        using HttpClient client = _factory.CreateClient();
        AuthEndpointFixture.ClearBearerToken(client);

        // Act — DELETE /auth/me without auth → 401
        HttpResponseMessage response = await client.DeleteAsync("/auth/me");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // =========================================================================
    // 13.2 — Authenticated → 204 and account is actually deleted
    // =========================================================================

    [Fact]
    public async Task DeleteMe_AuthenticatedUser_Returns204()
    {
        // Arrange — provision a user and issue a token
        Guid userId = Guid.NewGuid();
        const string email = "delete-me-204@test.example.com";

        await using MarketplaceDbContext ctx = _pg.CreateContext();
        ctx.Users.Add(new UserEntity
        {
            Id = userId,
            Email = email,
            EmailNormalized = email.ToLowerInvariant(),
            DisplayName = "Delete Me 204",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await ctx.SaveChangesAsync();

        string accessToken = AuthEndpointFixture.IssueTestAccessToken(userId, email, "Delete Me 204");
        using HttpClient client = _factory.CreateClient();
        AuthEndpointFixture.SetBearerToken(client, accessToken);

        // Act
        HttpResponseMessage response = await client.DeleteAsync("/auth/me");

        // Assert — 204 No Content is the canonical GDPR deletion success response
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteMe_AuthenticatedUser_SetsDeletedAtInDatabase()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        const string email = "soft-delete-check@test.example.com";

        await using MarketplaceDbContext setupCtx = _pg.CreateContext();
        setupCtx.Users.Add(new UserEntity
        {
            Id = userId,
            Email = email,
            EmailNormalized = email.ToLowerInvariant(),
            DisplayName = "Soft Delete Check",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await setupCtx.SaveChangesAsync();

        string accessToken = AuthEndpointFixture.IssueTestAccessToken(userId, email, "Soft Delete Check");
        using HttpClient client = _factory.CreateClient();
        AuthEndpointFixture.SetBearerToken(client, accessToken);

        // Act
        await client.DeleteAsync("/auth/me");

        // Assert — deleted_at is now set on the user row
        await using MarketplaceDbContext verifyCtx = _pg.CreateContext();
        UserEntity? user = await verifyCtx.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        Assert.NotNull(user);
        Assert.NotNull(user.DeletedAt);
        Assert.True(user.DeletedAt.Value <= DateTimeOffset.UtcNow,
            "deleted_at must be set to a past or current timestamp");
    }

    [Fact]
    public async Task DeleteMe_AuthenticatedUser_RevokesAllRefreshTokensInDatabase()
    {
        // Arrange — provision user + an active refresh token
        Guid userId = Guid.NewGuid();
        const string email = "revoke-tokens@test.example.com";

        await using MarketplaceDbContext setupCtx = _pg.CreateContext();
        setupCtx.Users.Add(new UserEntity
        {
            Id = userId,
            Email = email,
            EmailNormalized = email.ToLowerInvariant(),
            DisplayName = "Revoke Tokens",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await setupCtx.SaveChangesAsync();

        // Seed two active refresh tokens for this user
        Guid rt1Id = Guid.NewGuid();
        Guid rt2Id = Guid.NewGuid();
        setupCtx.RefreshTokens.AddRange(
            new RefreshTokenEntity
            {
                Id = rt1Id,
                UserId = userId,
                TokenHash = new string('a', 64),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
                RootId = rt1Id,
                Provider = "google",
                CreatedAt = DateTimeOffset.UtcNow,
            },
            new RefreshTokenEntity
            {
                Id = rt2Id,
                UserId = userId,
                TokenHash = new string('b', 64),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
                RootId = rt2Id,
                Provider = "google",
                CreatedAt = DateTimeOffset.UtcNow,
            });
        await setupCtx.SaveChangesAsync();

        string accessToken = AuthEndpointFixture.IssueTestAccessToken(userId, email, "Revoke Tokens");
        using HttpClient client = _factory.CreateClient();
        AuthEndpointFixture.SetBearerToken(client, accessToken);

        // Act
        await client.DeleteAsync("/auth/me");

        // Assert — both refresh tokens are now revoked
        await using MarketplaceDbContext verifyCtx = _pg.CreateContext();
        List<RefreshTokenEntity> tokens = await verifyCtx.RefreshTokens.AsNoTracking()
            .Where(rt => rt.UserId == userId)
            .ToListAsync();

        Assert.All(tokens, rt =>
            Assert.NotNull(rt.RevokedAt));
    }

    [Fact]
    public async Task DeleteMe_AuthenticatedUser_RemovesMembershipRows()
    {
        // Arrange — provision user + org + membership
        Guid userId = Guid.NewGuid();
        const string email = "remove-memberships@test.example.com";

        await using MarketplaceDbContext setupCtx = _pg.CreateContext();
        setupCtx.Users.Add(new UserEntity
        {
            Id = userId,
            Email = email,
            EmailNormalized = email.ToLowerInvariant(),
            DisplayName = "Remove Memberships",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await setupCtx.SaveChangesAsync();

        // Create an org owned by a second user, and add the deleting user as member
        Guid ownerId = Guid.NewGuid();
        setupCtx.Users.Add(new UserEntity
        {
            Id = ownerId,
            Email = "second-owner@example.com",
            EmailNormalized = "second-owner@example.com",
            DisplayName = "Second Owner",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await setupCtx.SaveChangesAsync();

        OrganizationEntity org = new()
        {
            Id = Guid.NewGuid(),
            Name = "Shared Org",
            NameNormalized = "shared org",
            Slug = "shared-org",
            CreatedBy = ownerId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        setupCtx.Organizations.Add(org);
        await setupCtx.SaveChangesAsync();

        setupCtx.OrganizationMembers.AddRange(
            new OrganizationMemberEntity
            {
                OrgId = org.Id,
                UserId = ownerId,
                Role = "owner",
                CreatedAt = DateTimeOffset.UtcNow,
            },
            new OrganizationMemberEntity
            {
                OrgId = org.Id,
                UserId = userId,
                Role = "member",
                CreatedAt = DateTimeOffset.UtcNow,
            });
        await setupCtx.SaveChangesAsync();

        string accessToken = AuthEndpointFixture.IssueTestAccessToken(userId, email, "Remove Memberships");
        using HttpClient client = _factory.CreateClient();
        AuthEndpointFixture.SetBearerToken(client, accessToken);

        // Act
        await client.DeleteAsync("/auth/me");

        // Assert — the deleting user's membership row is gone
        await using MarketplaceDbContext verifyCtx = _pg.CreateContext();
        bool hasMembership = await verifyCtx.OrganizationMembers.AsNoTracking()
            .AnyAsync(m => m.UserId == userId);

        Assert.False(hasMembership,
            "All organization_members rows for the deleted user must be removed");

        // The org itself must still exist (it has another owner)
        bool orgExists = await verifyCtx.Organizations.AsNoTracking()
            .AnyAsync(o => o.Id == org.Id);
        Assert.True(orgExists, "The org should survive because it has another owner");
    }

    [Fact]
    public async Task DeleteMe_SoleOwnerWithNoOtherMembers_DeletesOrg()
    {
        // Arrange — user is sole owner and only member of the org
        Guid userId = Guid.NewGuid();
        const string email = "sole-owner-delete@test.example.com";

        await using MarketplaceDbContext setupCtx = _pg.CreateContext();
        setupCtx.Users.Add(new UserEntity
        {
            Id = userId,
            Email = email,
            EmailNormalized = email.ToLowerInvariant(),
            DisplayName = "Sole Owner",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await setupCtx.SaveChangesAsync();

        OrganizationEntity soleOrg = new()
        {
            Id = Guid.NewGuid(),
            Name = "Solo Org",
            NameNormalized = "solo org",
            Slug = "solo-org",
            CreatedBy = userId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        setupCtx.Organizations.Add(soleOrg);
        await setupCtx.SaveChangesAsync();

        setupCtx.OrganizationMembers.Add(new OrganizationMemberEntity
        {
            OrgId = soleOrg.Id,
            UserId = userId,
            Role = "owner",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await setupCtx.SaveChangesAsync();

        string accessToken = AuthEndpointFixture.IssueTestAccessToken(userId, email, "Sole Owner");
        using HttpClient client = _factory.CreateClient();
        AuthEndpointFixture.SetBearerToken(client, accessToken);

        // Act
        await client.DeleteAsync("/auth/me");

        // Assert — the org is deleted (hard-delete) because the user was the sole owner with no other members
        await using MarketplaceDbContext verifyCtx = _pg.CreateContext();
        bool orgExists = await verifyCtx.Organizations.AsNoTracking()
            .AnyAsync(o => o.Id == soleOrg.Id);

        Assert.False(orgExists,
            "An org where the deleted user was the sole owner with no other members must be removed");
    }

    [Fact]
    public async Task DeleteMe_SoleOwnerWithOtherMembers_OrgSurvives()
    {
        // Arrange — user is sole owner but there are other members (non-owner)
        Guid userId = Guid.NewGuid();
        Guid otherMemberId = Guid.NewGuid();
        const string email = "sole-owner-other-members@test.example.com";

        await using MarketplaceDbContext setupCtx = _pg.CreateContext();
        setupCtx.Users.AddRange(
            new UserEntity
            {
                Id = userId,
                Email = email,
                EmailNormalized = email.ToLowerInvariant(),
                DisplayName = "Sole Owner With Members",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            },
            new UserEntity
            {
                Id = otherMemberId,
                Email = "other-member@example.com",
                EmailNormalized = "other-member@example.com",
                DisplayName = "Other Member",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        await setupCtx.SaveChangesAsync();

        OrganizationEntity orgWithMembers = new()
        {
            Id = Guid.NewGuid(),
            Name = "Org With Members",
            NameNormalized = "org with members",
            Slug = "org-with-members",
            CreatedBy = userId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        setupCtx.Organizations.Add(orgWithMembers);
        await setupCtx.SaveChangesAsync();

        setupCtx.OrganizationMembers.AddRange(
            new OrganizationMemberEntity
            {
                OrgId = orgWithMembers.Id,
                UserId = userId,
                Role = "owner",
                CreatedAt = DateTimeOffset.UtcNow,
            },
            new OrganizationMemberEntity
            {
                OrgId = orgWithMembers.Id,
                UserId = otherMemberId,
                Role = "member",
                CreatedAt = DateTimeOffset.UtcNow,
            });
        await setupCtx.SaveChangesAsync();

        string accessToken = AuthEndpointFixture.IssueTestAccessToken(userId, email, "Sole Owner With Members");
        using HttpClient client = _factory.CreateClient();
        AuthEndpointFixture.SetBearerToken(client, accessToken);

        // Act
        await client.DeleteAsync("/auth/me");

        // Assert — org must still exist (has another member even though user was sole owner)
        await using MarketplaceDbContext verifyCtx = _pg.CreateContext();
        bool orgExists = await verifyCtx.Organizations.AsNoTracking()
            .AnyAsync(o => o.Id == orgWithMembers.Id);

        Assert.True(orgExists,
            "Org with other members must survive even when the deleting user was the sole owner");
    }

    // =========================================================================
    // 13.2 — Deleted user can no longer authenticate
    // =========================================================================

    [Fact]
    public async Task DeleteMe_ThenGetMe_Returns401BecauseUserIsDeleted()
    {
        // Arrange — provision user, issue access token, delete account
        Guid userId = Guid.NewGuid();
        const string email = "deleted-no-auth@test.example.com";

        await using MarketplaceDbContext setupCtx = _pg.CreateContext();
        setupCtx.Users.Add(new UserEntity
        {
            Id = userId,
            Email = email,
            EmailNormalized = email.ToLowerInvariant(),
            DisplayName = "Deleted No Auth",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await setupCtx.SaveChangesAsync();

        string accessToken = AuthEndpointFixture.IssueTestAccessToken(userId, email, "Deleted No Auth");

        using HttpClient deleteClient = _factory.CreateClient();
        AuthEndpointFixture.SetBearerToken(deleteClient, accessToken);

        HttpResponseMessage deleteResponse = await deleteClient.DeleteAsync("/auth/me");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Act — attempt to call GET /auth/me with the same (still valid JWT signature) access token
        // The server must reject it because the user account has been deleted (soft-deleted)
        using HttpClient getClient = _factory.CreateClient();
        AuthEndpointFixture.SetBearerToken(getClient, accessToken);

        HttpResponseMessage getMeResponse = await getClient.GetAsync("/auth/me");

        // Assert — 401: the JWT is still cryptographically valid but the user account no longer exists
        Assert.Equal(HttpStatusCode.Unauthorized, getMeResponse.StatusCode);
    }
}
