using System.Net;
using System.Text.Json;
using ClaudeForge.Core.Shared.Authorization;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using ClaudeForge.Tests.Integration.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeForge.Tests.Integration.Organizations;

/// <summary>
/// Integration tests for Group 13, Task 13.3 — Append-only org_audit_log.
///
/// These tests are RED. They verify:
///   a) Audit entries are written for all org mutation events:
///      - invite.sent   (invite issued)
///      - invite.accepted
///      - invite.revoked
///      - member.removed
///      - member.role_changed
///      - org.visibility_changed (if applicable)
///   b) The audit log is INTERNAL-ONLY — no API endpoint exposes it.
///      Any GET to plausible audit-log routes must return 404.
///
/// These tests depend on the existing OrganizationsModule endpoints being wired
/// and auditing working end-to-end. The DeleteAccountUseCase from Task 13.1
/// is NOT under test here — these tests isolate the org mutation events.
///
/// Pre-requisites (all must exist — they're RED because Task 13.x contracts don't exist yet):
///   The IOrgAuditLogPort.AppendAsync must be called by:
///     - IssueInvitationUseCase  → action "invite.sent"
///     - AcceptInvitationUseCase → action "invite.accepted"
///     - RevokeInvitationUseCase → action "invite.revoked"
///     - RemoveMemberUseCase     → action "member.removed"
///     - ChangeMemberRoleUseCase → action "member.role_changed"
///   org.visibility_changed is future work — tested via a synthetic DB insert here.
/// </summary>
[Collection(PostgresFixture.CollectionName)]
public sealed class OrgAuditLogIntegrationTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public OrgAuditLogIntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace DbContext with test container
                    ServiceDescriptor? descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<MarketplaceDbContext>));
                    if (descriptor is not null)
                        services.Remove(descriptor);

                    ServiceDescriptor? ctxDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(MarketplaceDbContext));
                    if (ctxDescriptor is not null)
                        services.Remove(ctxDescriptor);

                    services.AddDbContext<MarketplaceDbContext>(options =>
                        options.UseNpgsql(_fixture.ConnectionString));

                    // Replace ICurrentUser with header-based test double
                    ServiceDescriptor? currentUserDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(ICurrentUser));
                    if (currentUserDescriptor is not null)
                        services.Remove(currentUserDescriptor);

                    services.AddScoped<ICurrentUser, AuditTestCurrentUser>();
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
        Guid orgId, Guid invitedBy, string emailNormalized, string token, string status = "pending") => new()
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            EmailNormalized = emailNormalized,
            InvitedBy = invitedBy,
            Role = "member",
            Status = status,
            Token = token,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
        };

    private void SetAuthUser(HttpClient client, Guid userId, string email)
    {
        client.DefaultRequestHeaders.Remove("X-Test-User-Id");
        client.DefaultRequestHeaders.Remove("X-Test-User-Email");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-User-Email", email);
    }

    private async Task<List<OrgAuditEntryEntity>> GetAuditEntriesForOrgAsync(Guid orgId)
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        return await ctx.OrgAuditLog.AsNoTracking()
            .Where(e => e.OrgId == orgId)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();
    }

    // =========================================================================
    // 13.3a — invite.sent audit entry
    // =========================================================================

    [Fact]
    public async Task PostInvitation_WritesInviteSentAuditEntry()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity owner = MakeUser("audit-invite-owner@example.com");
        ctx.Users.Add(owner);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, "Audit Invite Org");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        ctx.OrganizationMembers.Add(MakeMember(org.Id, owner.Id, "owner"));
        await ctx.SaveChangesAsync();

        SetAuthUser(_client, owner.Id, owner.Email);

        StringContent content = new(
            JsonSerializer.Serialize(new { email = "invitee@audit-test.com", role = "member" }),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/orgs/{org.Id}/invitations", content);

        // Assert — HTTP level
        Assert.True(
            response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.OK,
            $"Expected 201 or 200, got {(int)response.StatusCode}");

        // Assert — audit entry written
        List<OrgAuditEntryEntity> entries = await GetAuditEntriesForOrgAsync(org.Id);
        Assert.Contains(entries, e => e.Action == "invite.sent");

        OrgAuditEntryEntity sentEntry = entries.First(e => e.Action == "invite.sent");
        Assert.Equal(owner.Id, sentEntry.ActorUserId);
        Assert.Equal(org.Id, sentEntry.OrgId);
        Assert.False(string.IsNullOrWhiteSpace(sentEntry.Target),
            "invite.sent audit entry must include a non-empty target (e.g. the invitee email)");
    }

    // =========================================================================
    // 13.3b — invite.accepted audit entry
    // =========================================================================

    [Fact]
    public async Task AcceptInvitation_WritesInviteAcceptedAuditEntry()
    {
        // Arrange — owner creates org, invitee user exists and has a pending invitation
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity owner = MakeUser("audit-accept-owner@example.com");
        UserEntity invitee = MakeUser("audit-accept-invitee@example.com");
        ctx.Users.AddRange(owner, invitee);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, "Audit Accept Org");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        ctx.OrganizationMembers.Add(MakeMember(org.Id, owner.Id, "owner"));
        await ctx.SaveChangesAsync();

        // Seed a pending invitation for the invitee
        OrganizationInvitationEntity invitation = MakeInvitation(
            org.Id, owner.Id, invitee.Email.ToLowerInvariant(), "accept-test-token-001");
        ctx.OrganizationInvitations.Add(invitation);
        await ctx.SaveChangesAsync();

        // Act — invitee accepts the invitation
        SetAuthUser(_client, invitee.Id, invitee.Email);

        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/orgs/{org.Id}/invitations/{invitation.Id}/accept",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        // Assert — HTTP success (200 or 204)
        Assert.True(
            response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NoContent,
            $"Accept invitation expected 200 or 204, got {(int)response.StatusCode}");

        // Assert — invite.accepted audit entry written
        List<OrgAuditEntryEntity> entries = await GetAuditEntriesForOrgAsync(org.Id);
        Assert.Contains(entries, e => e.Action == "invite.accepted");

        OrgAuditEntryEntity acceptedEntry = entries.First(e => e.Action == "invite.accepted");
        Assert.Equal(invitee.Id, acceptedEntry.ActorUserId);
        Assert.Equal(org.Id, acceptedEntry.OrgId);
    }

    // =========================================================================
    // 13.3c — invite.revoked audit entry
    // =========================================================================

    [Fact]
    public async Task RevokeInvitation_WritesInviteRevokedAuditEntry()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity owner = MakeUser("audit-revoke-owner@example.com");
        ctx.Users.Add(owner);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, "Audit Revoke Org");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        ctx.OrganizationMembers.Add(MakeMember(org.Id, owner.Id, "owner"));
        await ctx.SaveChangesAsync();

        OrganizationInvitationEntity invitation = MakeInvitation(
            org.Id, owner.Id, "revoke-invitee@example.com", "revoke-test-token-001");
        ctx.OrganizationInvitations.Add(invitation);
        await ctx.SaveChangesAsync();

        // Act — owner revokes the invitation
        SetAuthUser(_client, owner.Id, owner.Email);

        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/orgs/{org.Id}/invitations/{invitation.Id}/revoke",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        Assert.True(
            response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NoContent,
            $"Revoke invitation expected 200 or 204, got {(int)response.StatusCode}");

        // Assert — invite.revoked audit entry written
        List<OrgAuditEntryEntity> entries = await GetAuditEntriesForOrgAsync(org.Id);
        Assert.Contains(entries, e => e.Action == "invite.revoked");

        OrgAuditEntryEntity revokedEntry = entries.First(e => e.Action == "invite.revoked");
        Assert.Equal(owner.Id, revokedEntry.ActorUserId);
        Assert.Equal(org.Id, revokedEntry.OrgId);
    }

    // =========================================================================
    // 13.3d — member.removed audit entry
    // =========================================================================

    [Fact]
    public async Task RemoveMember_WritesMemberRemovedAuditEntry()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity owner = MakeUser("audit-remove-owner@example.com");
        UserEntity member = MakeUser("audit-remove-member@example.com");
        ctx.Users.AddRange(owner, member);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, "Audit Remove Org");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        ctx.OrganizationMembers.AddRange(
            MakeMember(org.Id, owner.Id, "owner"),
            MakeMember(org.Id, member.Id, "member"));
        await ctx.SaveChangesAsync();

        // Act — owner removes the member
        SetAuthUser(_client, owner.Id, owner.Email);

        HttpResponseMessage response = await _client.DeleteAsync(
            $"/api/v1/orgs/{org.Id}/members/{member.Id}");

        Assert.True(
            response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NoContent,
            $"Remove member expected 200 or 204, got {(int)response.StatusCode}");

        // Assert — member.removed audit entry written
        List<OrgAuditEntryEntity> entries = await GetAuditEntriesForOrgAsync(org.Id);
        Assert.Contains(entries, e => e.Action == "member.removed");

        OrgAuditEntryEntity removedEntry = entries.First(e => e.Action == "member.removed");
        Assert.Equal(owner.Id, removedEntry.ActorUserId);
        Assert.Equal(org.Id, removedEntry.OrgId);
        Assert.False(string.IsNullOrWhiteSpace(removedEntry.Target),
            "member.removed audit entry must include a non-empty target (e.g. the removed user)");
    }

    // =========================================================================
    // 13.3e — member.role_changed audit entry
    // =========================================================================

    [Fact]
    public async Task ChangeMemberRole_WritesMemberRoleChangedAuditEntry()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity owner = MakeUser("audit-role-owner@example.com");
        UserEntity member = MakeUser("audit-role-member@example.com");
        ctx.Users.AddRange(owner, member);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, "Audit Role Org");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        ctx.OrganizationMembers.AddRange(
            MakeMember(org.Id, owner.Id, "owner"),
            MakeMember(org.Id, member.Id, "member"));
        await ctx.SaveChangesAsync();

        // Act — owner promotes the member to admin
        SetAuthUser(_client, owner.Id, owner.Email);

        StringContent content = new(
            JsonSerializer.Serialize(new { role = "admin" }),
            System.Text.Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await _client.PatchAsync(
            $"/api/v1/orgs/{org.Id}/members/{member.Id}", content);

        Assert.True(
            response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NoContent,
            $"Change role expected 200 or 204, got {(int)response.StatusCode}");

        // Assert — member.role_changed audit entry written
        List<OrgAuditEntryEntity> entries = await GetAuditEntriesForOrgAsync(org.Id);
        Assert.Contains(entries, e => e.Action == "member.role_changed");

        OrgAuditEntryEntity roleChangedEntry = entries.First(e => e.Action == "member.role_changed");
        Assert.Equal(owner.Id, roleChangedEntry.ActorUserId);
        Assert.Equal(org.Id, roleChangedEntry.OrgId);
    }

    // =========================================================================
    // 13.3f — org.visibility_changed audit entry (synthetic — verifies schema)
    // =========================================================================

    [Fact]
    public async Task AuditLog_CanStoreVisibilityChangedEntry()
    {
        // Arrange — insert a visibility-changed entry directly via EF to prove the schema supports it.
        // The actual UseCase that emits this will be implemented later; this test asserts the
        // audit log table can hold any action string (it's a VARCHAR, not an enum constraint).
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity actor = MakeUser("audit-visibility@example.com");
        ctx.Users.Add(actor);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(actor.Id, "Visibility Org");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        ctx.OrganizationMembers.Add(MakeMember(org.Id, actor.Id, "owner"));
        await ctx.SaveChangesAsync();

        // Insert the visibility-changed audit entry directly
        ctx.OrgAuditLog.Add(new OrgAuditEntryEntity
        {
            Id = Guid.NewGuid(),
            OrgId = org.Id,
            ActorUserId = actor.Id,
            Action = "org.visibility_changed",
            Target = "private→public",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await ctx.SaveChangesAsync();

        // Assert — the entry is retrievable
        List<OrgAuditEntryEntity> entries = await GetAuditEntriesForOrgAsync(org.Id);
        Assert.Contains(entries, e => e.Action == "org.visibility_changed");

        OrgAuditEntryEntity visEntry = entries.First(e => e.Action == "org.visibility_changed");
        Assert.Equal("private→public", visEntry.Target);
        Assert.Equal(actor.Id, visEntry.ActorUserId);
    }

    // =========================================================================
    // 13.3g — audit log is INTERNAL-ONLY: no API route exposes it
    // =========================================================================

    [Theory]
    [InlineData("/api/v1/orgs/{orgId}/audit-log")]
    [InlineData("/api/v1/orgs/{orgId}/audit")]
    [InlineData("/api/v1/orgs/{orgId}/logs")]
    [InlineData("/api/v1/audit-log")]
    [InlineData("/audit-log")]
    [InlineData("/audit")]
    [InlineData("/api/v1/admin/audit-log")]
    public async Task AuditLog_NotExposedViaAnyApiRoute_Returns404(string routeTemplate)
    {
        // Arrange — substitute a plausible orgId into the route template
        Guid plausibleOrgId = Guid.NewGuid();
        string route = routeTemplate.Replace("{orgId}", plausibleOrgId.ToString());

        // We try both an unauthenticated and a fake-authenticated request;
        // either way the route must not exist (404), not merely be forbidden (403).
        using HttpClient unauthClient = _factory.CreateClient();

        // Act
        HttpResponseMessage getResponse = await unauthClient.GetAsync(route);

        // Assert — 404 Not Found (route does not exist), NOT 403 Forbidden or 401 Unauthorized.
        // A 403/401 would mean the route exists but is gated — that's still a violation
        // of the "internal-only" contract because it could be unintentionally exposed.
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/orgs/{orgId}/audit-log")]
    [InlineData("/api/v1/orgs/{orgId}/audit")]
    [InlineData("/api/v1/orgs/{orgId}/logs")]
    public async Task AuditLog_AuthenticatedUserCannotAccessAuditLog_Returns404(string routeTemplate)
    {
        // Arrange — provision an owner user and their org
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity owner = MakeUser("audit-noexpose-owner@example.com");
        ctx.Users.Add(owner);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, "No Expose Org");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        ctx.OrganizationMembers.Add(MakeMember(org.Id, owner.Id, "owner"));
        await ctx.SaveChangesAsync();

        string route = routeTemplate.Replace("{orgId}", org.Id.ToString());

        // Set auth headers so the test ICurrentUser treats this as authenticated
        SetAuthUser(_client, owner.Id, owner.Email);

        // Act
        HttpResponseMessage response = await _client.GetAsync(route);

        // Assert — 404 regardless of auth (route must not exist at all)
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // =========================================================================
    // 13.3h — audit log is append-only: no DELETE or UPDATE route
    // =========================================================================

    [Theory]
    [InlineData("DELETE", "/api/v1/orgs/{orgId}/audit-log")]
    [InlineData("DELETE", "/api/v1/orgs/{orgId}/audit-log/{entryId}")]
    [InlineData("PUT", "/api/v1/orgs/{orgId}/audit-log/{entryId}")]
    [InlineData("PATCH", "/api/v1/orgs/{orgId}/audit-log/{entryId}")]
    public async Task AuditLog_NoDestructiveRoutes_Returns404(string httpMethod, string routeTemplate)
    {
        // Any DELETE / PUT / PATCH on an audit-log route must 404 — the route must not exist.
        Guid plausibleOrgId = Guid.NewGuid();
        Guid plausibleEntryId = Guid.NewGuid();
        string route = routeTemplate
            .Replace("{orgId}", plausibleOrgId.ToString())
            .Replace("{entryId}", plausibleEntryId.ToString());

        using HttpClient client = _factory.CreateClient();
        using HttpRequestMessage request = new(new HttpMethod(httpMethod), route);

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

// =============================================================================
// AuditTestCurrentUser — reads user from request headers for audit log tests
// Mirrors the HeaderBasedTestCurrentUser in OrganizationsHttpTests.
// =============================================================================

internal sealed class AuditTestCurrentUser : ICurrentUser
{
    private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _httpContextAccessor;

    public AuditTestCurrentUser(Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsAuthenticated
    {
        get
        {
            string? userId = _httpContextAccessor.HttpContext?.Request.Headers["X-Test-User-Id"];
            return !string.IsNullOrWhiteSpace(userId) && Guid.TryParse(userId, out _);
        }
    }

    public Guid? UserId
    {
        get
        {
            string? userId = _httpContextAccessor.HttpContext?.Request.Headers["X-Test-User-Id"];
            return Guid.TryParse(userId, out Guid parsed) ? parsed : null;
        }
    }

    public string? Email
    {
        get
        {
            string? email = _httpContextAccessor.HttpContext?.Request.Headers["X-Test-User-Email"];
            return string.IsNullOrWhiteSpace(email) ? null : email;
        }
    }
}
