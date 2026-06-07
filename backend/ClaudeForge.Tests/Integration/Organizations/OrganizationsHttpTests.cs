using System.Net;
using System.Net.Http.Json;
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
/// HTTP integration tests for Group 6, Tasks 6.1–6.13 — Organizations Module.
///
/// Uses WebApplicationFactory&lt;Program&gt; with a real PostgreSQL 16 container.
/// Tests the full HTTP stack for all org endpoints.
///
/// Endpoints under test:
///   POST   /api/v1/orgs                                     — create org (auth required)
///   GET    /api/v1/orgs                                     — list user orgs (auth required)
///   GET    /api/v1/orgs/{orgId}/members                     — list members (member required)
///   POST   /api/v1/orgs/{orgId}/invitations                 — issue invitation (admin/owner)
///   POST   /api/v1/orgs/{orgId}/invitations/{id}/accept     — accept invitation (auth required)
///   POST   /api/v1/orgs/{orgId}/invitations/{id}/revoke     — revoke invitation (admin/owner)
///   DELETE /api/v1/orgs/{orgId}/members/{userId}            — remove member (admin/owner)
///   PATCH  /api/v1/orgs/{orgId}/members/{userId}            — change role (owner only)
///
/// Expected production types that do NOT yet exist (coder MUST create):
///
///   NAMESPACE: ClaudeForge.Api.Modules.Organizations
///
///   sealed class OrganizationsModule : IModule
///     Registers: IOrganizationStorePort, IMembershipStorePort, IInvitationStorePort,
///                IInvitationEmailPort, IOrgAuditLogPort, IOrgMembershipQueryPort (if not already)
///                + all use-cases + ICurrentUser (per-request from HttpContext.User)
///     Maps endpoints above
///
///   NAMESPACE: ClaudeForge.Infrastructure.Organizations
///
///   sealed class OrganizationStoreAdapter : IOrganizationStorePort
///     (EF Core adapter backed by MarketplaceDbContext.Organizations)
///
///   sealed class MembershipStoreAdapter : IMembershipStorePort
///     (EF Core adapter backed by MarketplaceDbContext.OrganizationMembers)
///
///   sealed class InvitationStoreAdapter : IInvitationStorePort
///     (EF Core adapter backed by MarketplaceDbContext.OrganizationInvitations)
///
///   sealed class OrgAuditLogAdapter : IOrgAuditLogPort
///     (EF Core adapter backed by MarketplaceDbContext.OrgAuditLog; append-only)
///
///   sealed class SmtpInvitationEmailAdapter : IInvitationEmailPort
///     (configured via EMAIL__* env vars; failure is swallowed by the use-case)
///
///   NAMESPACE: ClaudeForge.Infrastructure.Context
///
///   sealed class HttpContextCurrentUser : ICurrentUser
///     Populated from IHttpContextAccessor + HttpContext.User claims
///     (UserId from "sub" claim, Email from "email" claim, IsAuthenticated from ClaimsPrincipal)
///
/// Note on authentication in tests:
///   The WebApplicationFactory test setup replaces authentication to allow tests to inject
///   a pre-authenticated user without going through the full OIDC/JWT flow.
///   Tests override the DI registration of ICurrentUser with a configurable stub.
///   This mirrors the pattern used in Group 5 (Identity module endpoint tests).
///   All "unauthenticated" tests send requests WITHOUT an authorization header
///   and the ICurrentUser stub returns IsAuthenticated=false.
///
/// Note on audit log assertions (Tasks 6.1, 6.13):
///   Audit entries are verified by direct DB query on org_audit_log after operations.
///   They are never surfaced through an API endpoint (internal-only per design.md §9).
/// </summary>
[Collection(PostgresFixture.CollectionName)]
public sealed class OrganizationsHttpTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // The test factory replaces ICurrentUser via a TestCurrentUser that can be
    // swapped per-request by setting the header "X-Test-User-Id" and "X-Test-User-Email".
    // The OrganizationsModule must read ICurrentUser from DI per-request (Scoped).
    public OrganizationsHttpTests(PostgresFixture fixture)
    {
        _fixture = fixture;

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace the DbContext registration with the test container connection
                    ServiceDescriptor? descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<MarketplaceDbContext>));
                    if (descriptor is not null)
                        services.Remove(descriptor);

                    ServiceDescriptor? ctxDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(MarketplaceDbContext));
                    if (ctxDescriptor is not null)
                        services.Remove(ctxDescriptor);

                    services.AddDbContext<MarketplaceDbContext>(options =>
                        options.UseNpgsql(fixture.ConnectionString));

                    // Replace ICurrentUser with a test double that reads from request headers
                    ServiceDescriptor? currentUserDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(ICurrentUser));
                    if (currentUserDescriptor is not null)
                        services.Remove(currentUserDescriptor);

                    services.AddScoped<ICurrentUser, HeaderBasedTestCurrentUser>();
                });
            });

        _client = _factory.CreateClient();
    }

    // -------------------------------------------------------------------------
    // Per-test isolation: truncate all auth + marketplace tables.
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

    /// <summary>
    /// Sets headers that the HeaderBasedTestCurrentUser reads to simulate an authenticated user.
    /// </summary>
    private void SetAuthUser(HttpClient client, Guid userId, string email)
    {
        client.DefaultRequestHeaders.Remove("X-Test-User-Id");
        client.DefaultRequestHeaders.Remove("X-Test-User-Email");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-User-Email", email);
    }

    private void ClearAuthUser(HttpClient client)
    {
        client.DefaultRequestHeaders.Remove("X-Test-User-Id");
        client.DefaultRequestHeaders.Remove("X-Test-User-Email");
    }

    // =========================================================================
    // POST /api/v1/orgs — CreateOrganization
    // =========================================================================

    // Task 6.2 — authenticated create → 201 + creator gets owner role

    [Fact]
    public async Task PostOrgs_AuthenticatedUser_Returns201AndCreatorIsOwner()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity creator = MakeUser("creator@example.com");
        ctx.Users.Add(creator);
        await ctx.SaveChangesAsync();

        SetAuthUser(_client, creator.Id, creator.Email);

        StringContent content = new(
            JsonSerializer.Serialize(new { name = "Test Org", slug = "test-org" }),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        HttpResponseMessage response = await _client.PostAsync("/api/v1/orgs", content);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        Assert.True(root.TryGetProperty("id", out JsonElement idEl), "Response must have 'id'");
        Assert.True(root.TryGetProperty("name", out JsonElement nameEl), "Response must have 'name'");
        Assert.Equal("Test Org", nameEl.GetString());

        // Verify creator has owner role in DB
        Guid orgId = Guid.Parse(idEl.GetString()!);
        OrganizationMemberEntity? membership = await ctx.OrganizationMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.OrgId == orgId && m.UserId == creator.Id);

        Assert.NotNull(membership);
        Assert.Equal("owner", membership.Role);
    }

    // Task 6.2 — duplicate name → 409

    [Fact]
    public async Task PostOrgs_DuplicateName_Returns409Conflict()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity creator = MakeUser("dup-org-creator@example.com");
        ctx.Users.Add(creator);
        await ctx.SaveChangesAsync();

        OrganizationEntity existingOrg = MakeOrg(creator.Id, "Existing Org");
        ctx.Organizations.Add(existingOrg);
        await ctx.SaveChangesAsync();

        SetAuthUser(_client, creator.Id, creator.Email);

        StringContent content = new(
            JsonSerializer.Serialize(new { name = "Existing Org", slug = "existing-org-2" }),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        HttpResponseMessage response = await _client.PostAsync("/api/v1/orgs", content);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("detail", out _),
            "Response must be RFC 7807 ProblemDetails");
    }

    // Task 6.2 — unauthenticated → 401

    [Fact]
    public async Task PostOrgs_Unauthenticated_Returns401()
    {
        // Arrange — no auth headers
        ClearAuthUser(_client);

        StringContent content = new(
            JsonSerializer.Serialize(new { name = "Anon Org", slug = "anon-org" }),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        HttpResponseMessage response = await _client.PostAsync("/api/v1/orgs", content);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Task 6.2 — name normalization: "  ACME  " → "acme" (trimmed + lower)

    [Fact]
    public async Task PostOrgs_NameWithSpacesAndMixedCase_NormalizedForUniqueCheck()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity creator = MakeUser("normalizer@example.com");
        ctx.Users.Add(creator);
        await ctx.SaveChangesAsync();

        // Pre-seed org with normalized name "acme"
        OrganizationEntity existing = new()
        {
            Id = Guid.NewGuid(),
            Name = "ACME",
            NameNormalized = "acme",
            Slug = "acme",
            CreatedBy = creator.Id,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        ctx.Organizations.Add(existing);
        await ctx.SaveChangesAsync();

        SetAuthUser(_client, creator.Id, creator.Email);

        // Submit with different casing — should collide on normalized form
        StringContent content = new(
            JsonSerializer.Serialize(new { name = "Acme", slug = "acme-2" }),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        HttpResponseMessage response = await _client.PostAsync("/api/v1/orgs", content);

        // Assert — 409 because "acme" == "Acme" normalized
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // =========================================================================
    // GET /api/v1/orgs — ListUserOrganizations
    // =========================================================================

    // Task 6.4 — unauthenticated → 401

    [Fact]
    public async Task GetOrgs_Unauthenticated_Returns401()
    {
        // Arrange
        ClearAuthUser(_client);

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/orgs");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Task 6.4 — authenticated with no orgs → 200 empty list

    [Fact]
    public async Task GetOrgs_AuthenticatedNoMemberships_Returns200EmptyList()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity user = MakeUser("no-orgs@example.com");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        SetAuthUser(_client, user.Id, user.Email);

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/orgs");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        // Expects { "data": [] } or direct array — allow both but data must be empty
        JsonElement root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Array)
        {
            Assert.Equal(0, root.GetArrayLength());
        }
        else
        {
            Assert.True(root.TryGetProperty("data", out JsonElement data));
            Assert.Equal(0, data.GetArrayLength());
        }
    }

    // Task 6.4 — list returns org name, slug, and user's role

    [Fact]
    public async Task GetOrgs_AuthenticatedWithMemberships_ReturnsOrgsWithRole()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity user = MakeUser("member-user@example.com");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(user.Id, "Member Org");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        ctx.OrganizationMembers.Add(MakeMember(org.Id, user.Id, "owner"));
        await ctx.SaveChangesAsync();

        SetAuthUser(_client, user.Id, user.Email);

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/orgs");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        // Resolve data array (direct or wrapped)
        JsonElement dataArr = root.ValueKind == JsonValueKind.Array ? root
            : root.GetProperty("data");

        Assert.True(dataArr.GetArrayLength() >= 1, "Expected at least one org in the list");
        JsonElement firstOrg = dataArr[0];
        Assert.True(firstOrg.TryGetProperty("name", out _), "Org must have 'name'");
        Assert.True(firstOrg.TryGetProperty("slug", out _), "Org must have 'slug'");
        Assert.True(firstOrg.TryGetProperty("userRole", out JsonElement roleEl),
            "Org must have 'userRole' field");

        string? role = roleEl.GetString();
        Assert.NotNull(role);
        Assert.True(role == "owner" || role == "admin" || role == "member",
            $"userRole '{role}' is not a valid OrgRole value");
    }

    // =========================================================================
    // GET /api/v1/orgs/{orgId}/members — ListOrgMembers
    // =========================================================================

    // Task 6.4 — non-member → 403 (non-disclosure — NOT 404)

    [Fact]
    public async Task GetOrgMembers_NonMember_Returns403Forbidden()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity owner = MakeUser("owner-members-list@example.com");
        UserEntity nonMember = MakeUser("non-member@example.com");
        ctx.Users.AddRange(owner, nonMember);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, "Members Only Org");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        ctx.OrganizationMembers.Add(MakeMember(org.Id, owner.Id, "owner"));
        await ctx.SaveChangesAsync();

        // Authenticated as the non-member
        SetAuthUser(_client, nonMember.Id, nonMember.Email);

        // Act
        HttpResponseMessage response = await _client.GetAsync($"/api/v1/orgs/{org.Id}/members");

        // Assert — 403 per design: "non-disclosure for org view — note: org membership list is 403 not 404"
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Task 6.4 — member can list members with email/name/role

    [Fact]
    public async Task GetOrgMembers_Member_Returns200WithMemberEmailAndRole()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity owner = MakeUser("org-owner-list@example.com");
        UserEntity member = MakeUser("org-member-list@example.com");
        ctx.Users.AddRange(owner, member);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, "List Members Org");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        ctx.OrganizationMembers.AddRange(
            MakeMember(org.Id, owner.Id, "owner"),
            MakeMember(org.Id, member.Id, "member"));
        await ctx.SaveChangesAsync();

        // Authenticated as the owner (a member)
        SetAuthUser(_client, owner.Id, owner.Email);

        // Act
        HttpResponseMessage response = await _client.GetAsync($"/api/v1/orgs/{org.Id}/members");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        JsonElement dataArr = root.ValueKind == JsonValueKind.Array ? root
            : root.GetProperty("data");

        Assert.True(dataArr.GetArrayLength() >= 2);

        // Each member must expose email, name (or displayName), and role
        foreach (JsonElement memberEl in dataArr.EnumerateArray())
        {
            Assert.True(
                memberEl.TryGetProperty("email", out _) ||
                memberEl.TryGetProperty("displayName", out _),
                "Member entry must have 'email' or 'displayName'");
            Assert.True(memberEl.TryGetProperty("role", out _), "Member entry must have 'role'");
        }
    }

    // =========================================================================
    // POST /api/v1/orgs/{orgId}/invitations — IssueInvitation
    // =========================================================================

    // Task 6.6 — owner/admin invite → 201 pending

    [Fact]
    public async Task PostInvitations_OwnerInvite_Returns201Pending()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity owner = MakeUser("invite-owner@example.com");
        ctx.Users.Add(owner);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, "Invite Org");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        ctx.OrganizationMembers.Add(MakeMember(org.Id, owner.Id, "owner"));
        await ctx.SaveChangesAsync();

        SetAuthUser(_client, owner.Id, owner.Email);

        StringContent content = new(
            JsonSerializer.Serialize(new { email = "invitee@example.com", role = "member" }),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/orgs/{org.Id}/invitations", content);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        Assert.True(root.TryGetProperty("status", out JsonElement statusEl));
        Assert.Equal("pending", statusEl.GetString());
    }

    // Task 6.6 — invite existing member → 409

    [Fact]
    public async Task PostInvitations_InviteExistingMember_Returns409Conflict()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity owner = MakeUser("existing-member-owner@example.com");
        UserEntity existingMember = MakeUser("existing-member@example.com");
        ctx.Users.AddRange(owner, existingMember);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, "Existing Member Org");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        ctx.OrganizationMembers.AddRange(
            MakeMember(org.Id, owner.Id, "owner"),
            MakeMember(org.Id, existingMember.Id, "member"));
        await ctx.SaveChangesAsync();

        SetAuthUser(_client, owner.Id, owner.Email);

        StringContent content = new(
            JsonSerializer.Serialize(new { email = existingMember.Email, role = "member" }),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/orgs/{org.Id}/invitations", content);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // Task 6.6 — member (non-owner/admin) invite → 403

    [Fact]
    public async Task PostInvitations_PlainMemberInvite_Returns403Forbidden()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity owner = MakeUser("plain-member-owner@example.com");
        UserEntity plainMember = MakeUser("plain-member@example.com");
        ctx.Users.AddRange(owner, plainMember);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, "Plain Member Org");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        ctx.OrganizationMembers.AddRange(
            MakeMember(org.Id, owner.Id, "owner"),
            MakeMember(org.Id, plainMember.Id, "member"));
        await ctx.SaveChangesAsync();

        // Authenticated as the plain member
        SetAuthUser(_client, plainMember.Id, plainMember.Email);

        StringContent content = new(
            JsonSerializer.Serialize(new { email = "new@example.com", role = "member" }),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/orgs/{org.Id}/invitations", content);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Task 6.6 — unauthenticated → 401

    [Fact]
    public async Task PostInvitations_Unauthenticated_Returns401()
    {
        // Arrange
        ClearAuthUser(_client);

        StringContent content = new(
            JsonSerializer.Serialize(new { email = "someone@example.com", role = "member" }),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/orgs/{Guid.NewGuid()}/invitations", content);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // =========================================================================
    // POST /api/v1/orgs/{orgId}/invitations/{id}/accept — AcceptInvitation
    // =========================================================================

    // Task 6.8 — accept valid pending → 200 + member role

    [Fact]
    public async Task AcceptInvitation_ValidPending_Returns200AndCreatesMembership()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity owner = MakeUser("accept-owner@example.com");
        UserEntity invitee = MakeUser("accept-invitee@example.com");
        ctx.Users.AddRange(owner, invitee);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, "Accept Org");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        ctx.OrganizationMembers.Add(MakeMember(org.Id, owner.Id, "owner"));

        OrganizationInvitationEntity invite = MakeInvitation(
            org.Id, owner.Id, invitee.EmailNormalized, "accept-token-001");
        ctx.OrganizationInvitations.Add(invite);
        await ctx.SaveChangesAsync();

        SetAuthUser(_client, invitee.Id, invitee.Email);

        // Act
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/orgs/{org.Id}/invitations/{invite.Id}/accept",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify membership created in DB
        bool isMember = await ctx.OrganizationMembers
            .AsNoTracking()
            .AnyAsync(m => m.OrgId == org.Id && m.UserId == invitee.Id);
        Assert.True(isMember, "Accepter must become a member of the organization");

        // Verify invite status updated to "accepted"
        OrganizationInvitationEntity? updatedInvite = await ctx.OrganizationInvitations
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == invite.Id);
        Assert.NotNull(updatedInvite);
        Assert.Equal("accepted", updatedInvite.Status);
    }

    // Task 6.8 — accept non-existent invitation → 404

    [Fact]
    public async Task AcceptInvitation_NotFound_Returns404()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity user = MakeUser("accept-notfound@example.com");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        SetAuthUser(_client, user.Id, user.Email);

        // Act
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/orgs/{Guid.NewGuid()}/invitations/{Guid.NewGuid()}/accept",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Task 6.8 — accept revoked/expired (non-pending) → 410 Gone

    [Theory]
    [InlineData("revoked")]
    [InlineData("expired")]
    public async Task AcceptInvitation_NonPendingStatus_Returns410Gone(string status)
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity owner = MakeUser($"accept-gone-owner-{status}@example.com");
        UserEntity invitee = MakeUser($"accept-gone-invitee-{status}@example.com");
        ctx.Users.AddRange(owner, invitee);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, $"Gone Org {status}");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        ctx.OrganizationMembers.Add(MakeMember(org.Id, owner.Id, "owner"));

        OrganizationInvitationEntity invite = MakeInvitation(
            org.Id, owner.Id, invitee.EmailNormalized, $"gone-token-{status}", status);
        ctx.OrganizationInvitations.Add(invite);
        await ctx.SaveChangesAsync();

        SetAuthUser(_client, invitee.Id, invitee.Email);

        // Act
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/orgs/{org.Id}/invitations/{invite.Id}/accept",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    // Task 6.8 — owner/admin revoke pending → 200

    [Fact]
    public async Task RevokeInvitation_OwnerRevokePending_Returns200()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity owner = MakeUser("revoke-owner@example.com");
        ctx.Users.Add(owner);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, "Revoke Org");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        ctx.OrganizationMembers.Add(MakeMember(org.Id, owner.Id, "owner"));

        OrganizationInvitationEntity invite = MakeInvitation(
            org.Id, owner.Id, "revoke-target@example.com", "revoke-token-001");
        ctx.OrganizationInvitations.Add(invite);
        await ctx.SaveChangesAsync();

        SetAuthUser(_client, owner.Id, owner.Email);

        // Act
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/orgs/{org.Id}/invitations/{invite.Id}/revoke",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify invite status updated in DB
        OrganizationInvitationEntity? updatedInvite = await ctx.OrganizationInvitations
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == invite.Id);
        Assert.NotNull(updatedInvite);
        Assert.Equal("revoked", updatedInvite.Status);
    }

    // =========================================================================
    // DELETE /api/v1/orgs/{orgId}/members/{userId} — RemoveMember
    // =========================================================================

    // Task 6.10 — owner/admin remove member → 204

    [Fact]
    public async Task DeleteMember_OwnerRemovesMember_Returns204()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity owner = MakeUser("remove-owner@example.com");
        UserEntity member = MakeUser("remove-member@example.com");
        ctx.Users.AddRange(owner, member);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, "Remove Org");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        ctx.OrganizationMembers.AddRange(
            MakeMember(org.Id, owner.Id, "owner"),
            MakeMember(org.Id, member.Id, "member"));
        await ctx.SaveChangesAsync();

        SetAuthUser(_client, owner.Id, owner.Email);

        // Act
        HttpResponseMessage response = await _client.DeleteAsync(
            $"/api/v1/orgs/{org.Id}/members/{member.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify membership removed from DB
        bool stillMember = await ctx.OrganizationMembers
            .AsNoTracking()
            .AnyAsync(m => m.OrgId == org.Id && m.UserId == member.Id);
        Assert.False(stillMember, "Removed member must no longer have a membership row");
    }

    // Task 6.10 — member removing another → 403

    [Fact]
    public async Task DeleteMember_PlainMemberRemovesOther_Returns403Forbidden()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity owner = MakeUser("member-remove-owner@example.com");
        UserEntity memberA = MakeUser("member-a@example.com");
        UserEntity memberB = MakeUser("member-b@example.com");
        ctx.Users.AddRange(owner, memberA, memberB);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, "Member Remove Org");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        ctx.OrganizationMembers.AddRange(
            MakeMember(org.Id, owner.Id, "owner"),
            MakeMember(org.Id, memberA.Id, "member"),
            MakeMember(org.Id, memberB.Id, "member"));
        await ctx.SaveChangesAsync();

        // Authenticated as memberA (plain member) trying to remove memberB
        SetAuthUser(_client, memberA.Id, memberA.Email);

        // Act
        HttpResponseMessage response = await _client.DeleteAsync(
            $"/api/v1/orgs/{org.Id}/members/{memberB.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Task 6.10 — sole owner self-removal → 400

    [Fact]
    public async Task DeleteMember_SoleOwnerSelfRemoval_Returns400BadRequest()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity owner = MakeUser("sole-owner@example.com");
        ctx.Users.Add(owner);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, "Sole Owner Org");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        ctx.OrganizationMembers.Add(MakeMember(org.Id, owner.Id, "owner"));
        await ctx.SaveChangesAsync();

        SetAuthUser(_client, owner.Id, owner.Email);

        // Act — sole owner trying to remove themselves
        HttpResponseMessage response = await _client.DeleteAsync(
            $"/api/v1/orgs/{org.Id}/members/{owner.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Verify membership still exists
        bool stillOwner = await ctx.OrganizationMembers
            .AsNoTracking()
            .AnyAsync(m => m.OrgId == org.Id && m.UserId == owner.Id);
        Assert.True(stillOwner, "Sole owner must not be removed");
    }

    // =========================================================================
    // PATCH /api/v1/orgs/{orgId}/members/{userId} — ChangeMemberRole
    // =========================================================================

    // Task 6.10 — owner promotes member→admin → 200

    [Fact]
    public async Task PatchMember_OwnerPromotesMemberToAdmin_Returns200()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity owner = MakeUser("promote-owner@example.com");
        UserEntity member = MakeUser("promote-member@example.com");
        ctx.Users.AddRange(owner, member);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, "Promote Org");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        ctx.OrganizationMembers.AddRange(
            MakeMember(org.Id, owner.Id, "owner"),
            MakeMember(org.Id, member.Id, "member"));
        await ctx.SaveChangesAsync();

        SetAuthUser(_client, owner.Id, owner.Email);

        StringContent content = new(
            JsonSerializer.Serialize(new { role = "admin" }),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        HttpResponseMessage response = await _client.PatchAsync(
            $"/api/v1/orgs/{org.Id}/members/{member.Id}", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify role updated in DB
        OrganizationMemberEntity? updatedMembership = await ctx.OrganizationMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.OrgId == org.Id && m.UserId == member.Id);
        Assert.NotNull(updatedMembership);
        Assert.Equal("admin", updatedMembership.Role);
    }

    // Task 6.10 — non-owner role change → 403

    [Fact]
    public async Task PatchMember_AdminChangesRole_Returns403Forbidden()
    {
        // Arrange — role change is owner-only per spec
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity owner = MakeUser("role-change-owner@example.com");
        UserEntity admin = MakeUser("role-change-admin@example.com");
        UserEntity member = MakeUser("role-change-member@example.com");
        ctx.Users.AddRange(owner, admin, member);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, "Role Change Org");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        ctx.OrganizationMembers.AddRange(
            MakeMember(org.Id, owner.Id, "owner"),
            MakeMember(org.Id, admin.Id, "admin"),
            MakeMember(org.Id, member.Id, "member"));
        await ctx.SaveChangesAsync();

        // Authenticated as admin — not allowed to change roles
        SetAuthUser(_client, admin.Id, admin.Email);

        StringContent content = new(
            JsonSerializer.Serialize(new { role = "admin" }),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        HttpResponseMessage response = await _client.PatchAsync(
            $"/api/v1/orgs/{org.Id}/members/{member.Id}", content);

        // Assert — owner-only, admin gets 403
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // =========================================================================
    // Audit log assertions — Tasks 6.1 and 6.13
    // =========================================================================

    [Fact]
    public async Task InviteSentAcceptedRevoked_AllProduceAuditLogEntries()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity owner = MakeUser("audit-lifecycle-owner@example.com");
        UserEntity invitee = MakeUser("audit-lifecycle-invitee@example.com");
        ctx.Users.AddRange(owner, invitee);
        await ctx.SaveChangesAsync();

        OrganizationEntity org = MakeOrg(owner.Id, "Audit Lifecycle Org");
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();

        ctx.OrganizationMembers.Add(MakeMember(org.Id, owner.Id, "owner"));
        await ctx.SaveChangesAsync();

        SetAuthUser(_client, owner.Id, owner.Email);

        // Step 1: issue invitation → should produce invite.sent audit entry
        StringContent inviteContent = new(
            JsonSerializer.Serialize(new { email = invitee.Email, role = "member" }),
            System.Text.Encoding.UTF8,
            "application/json");

        HttpResponseMessage inviteResponse = await _client.PostAsync(
            $"/api/v1/orgs/{org.Id}/invitations", inviteContent);
        Assert.Equal(HttpStatusCode.Created, inviteResponse.StatusCode);

        string inviteBody = await inviteResponse.Content.ReadAsStringAsync();
        using JsonDocument inviteDoc = JsonDocument.Parse(inviteBody);
        Guid inviteId = Guid.Parse(inviteDoc.RootElement.GetProperty("id").GetString()!);

        // Verify invite.sent audit entry
        bool inviteSentAudit = await ctx.OrgAuditLog
            .AsNoTracking()
            .AnyAsync(e => e.OrgId == org.Id && e.Action == "invite.sent");
        Assert.True(inviteSentAudit, "invite.sent audit entry must be present after invitation issued");

        // Step 2: accept invitation → should produce invite.accepted audit entry
        SetAuthUser(_client, invitee.Id, invitee.Email);

        HttpResponseMessage acceptResponse = await _client.PostAsync(
            $"/api/v1/orgs/{org.Id}/invitations/{inviteId}/accept",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        bool inviteAcceptedAudit = await ctx.OrgAuditLog
            .AsNoTracking()
            .AnyAsync(e => e.OrgId == org.Id && e.Action == "invite.accepted");
        Assert.True(inviteAcceptedAudit, "invite.accepted audit entry must be present after invitation accepted");

        // Audit log is internal-only — verify no API endpoint exposes it
        // (The audit log must only be readable via direct DB access)
        SetAuthUser(_client, owner.Id, owner.Email);
        HttpResponseMessage auditApiResponse = await _client.GetAsync($"/api/v1/orgs/{org.Id}/audit-log");
        Assert.Equal(HttpStatusCode.NotFound, auditApiResponse.StatusCode);
    }

    [Fact]
    public async Task InviteRevoked_ProducesInviteRevokedAuditEntry()
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
        OrganizationInvitationEntity invite = MakeInvitation(
            org.Id, owner.Id, "revoke-audit@example.com", "revoke-audit-token-001");
        ctx.OrganizationInvitations.Add(invite);
        await ctx.SaveChangesAsync();

        SetAuthUser(_client, owner.Id, owner.Email);

        // Act
        HttpResponseMessage revokeResponse = await _client.PostAsync(
            $"/api/v1/orgs/{org.Id}/invitations/{invite.Id}/revoke",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);

        // Assert — invite.revoked audit entry
        bool auditEntry = await ctx.OrgAuditLog
            .AsNoTracking()
            .AnyAsync(e => e.OrgId == org.Id && e.Action == "invite.revoked");
        Assert.True(auditEntry, "invite.revoked audit entry must be present after invitation revoked");
    }

    [Fact]
    public async Task MemberRemoved_ProducesMemberRemovedAuditEntry()
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

        SetAuthUser(_client, owner.Id, owner.Email);

        // Act
        HttpResponseMessage response = await _client.DeleteAsync(
            $"/api/v1/orgs/{org.Id}/members/{member.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Assert — member.removed audit entry
        bool auditEntry = await ctx.OrgAuditLog
            .AsNoTracking()
            .AnyAsync(e => e.OrgId == org.Id && e.Action == "member.removed");
        Assert.True(auditEntry, "member.removed audit entry must be present after member removed");
    }

    [Fact]
    public async Task RoleChanged_ProducesMemberRoleChangedAuditEntry()
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

        SetAuthUser(_client, owner.Id, owner.Email);

        // Act
        StringContent content = new(
            JsonSerializer.Serialize(new { role = "admin" }),
            System.Text.Encoding.UTF8,
            "application/json");
        HttpResponseMessage response = await _client.PatchAsync(
            $"/api/v1/orgs/{org.Id}/members/{member.Id}", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Assert — member.role_changed audit entry
        bool auditEntry = await ctx.OrgAuditLog
            .AsNoTracking()
            .AnyAsync(e => e.OrgId == org.Id && e.Action == "member.role_changed");
        Assert.True(auditEntry, "member.role_changed audit entry must be present after role changed");
    }

    // =========================================================================
    // Full org lifecycle — Task 6.13
    // =========================================================================

    [Fact]
    public async Task FullOrgLifecycle_CreateInviteAcceptListMembersChangeRoleRemove_AllSucceed()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity owner = MakeUser("lifecycle-owner@example.com");
        UserEntity invitee = MakeUser("lifecycle-invitee@example.com");
        ctx.Users.AddRange(owner, invitee);
        await ctx.SaveChangesAsync();

        // Step 1: create org
        SetAuthUser(_client, owner.Id, owner.Email);
        StringContent createOrgContent = new(
            JsonSerializer.Serialize(new { name = "Lifecycle Org", slug = "lifecycle-org" }),
            System.Text.Encoding.UTF8, "application/json");
        HttpResponseMessage createResponse = await _client.PostAsync("/api/v1/orgs", createOrgContent);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        string createBody = await createResponse.Content.ReadAsStringAsync();
        using JsonDocument createDoc = JsonDocument.Parse(createBody);
        Guid orgId = Guid.Parse(createDoc.RootElement.GetProperty("id").GetString()!);

        // Step 2: invite member
        StringContent inviteContent = new(
            JsonSerializer.Serialize(new { email = invitee.Email, role = "member" }),
            System.Text.Encoding.UTF8, "application/json");
        HttpResponseMessage inviteResp = await _client.PostAsync(
            $"/api/v1/orgs/{orgId}/invitations", inviteContent);
        Assert.Equal(HttpStatusCode.Created, inviteResp.StatusCode);

        string inviteBody = await inviteResp.Content.ReadAsStringAsync();
        using JsonDocument inviteDoc = JsonDocument.Parse(inviteBody);
        Guid inviteId = Guid.Parse(inviteDoc.RootElement.GetProperty("id").GetString()!);

        // Step 3: accept invitation
        SetAuthUser(_client, invitee.Id, invitee.Email);
        HttpResponseMessage acceptResp = await _client.PostAsync(
            $"/api/v1/orgs/{orgId}/invitations/{inviteId}/accept",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, acceptResp.StatusCode);

        // Step 4: list members (invitee is now a member and can list)
        HttpResponseMessage listResp = await _client.GetAsync($"/api/v1/orgs/{orgId}/members");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);

        string listBody = await listResp.Content.ReadAsStringAsync();
        using JsonDocument listDoc = JsonDocument.Parse(listBody);
        JsonElement membersArr = listDoc.RootElement.ValueKind == JsonValueKind.Array
            ? listDoc.RootElement
            : listDoc.RootElement.GetProperty("data");
        Assert.True(membersArr.GetArrayLength() >= 2, "Both owner and invitee should be members");

        // Step 5: owner changes invitee's role to admin
        SetAuthUser(_client, owner.Id, owner.Email);
        StringContent roleContent = new(
            JsonSerializer.Serialize(new { role = "admin" }),
            System.Text.Encoding.UTF8, "application/json");
        HttpResponseMessage changeResp = await _client.PatchAsync(
            $"/api/v1/orgs/{orgId}/members/{invitee.Id}", roleContent);
        Assert.Equal(HttpStatusCode.OK, changeResp.StatusCode);

        // Step 6: owner removes invitee
        HttpResponseMessage removeResp = await _client.DeleteAsync(
            $"/api/v1/orgs/{orgId}/members/{invitee.Id}");
        Assert.Equal(HttpStatusCode.NoContent, removeResp.StatusCode);

        // Verify all audit entries present
        string[] expectedAuditActions =
        [
            "org.created",
            "invite.sent",
            "invite.accepted",
            "member.role_changed",
            "member.removed",
        ];

        foreach (string action in expectedAuditActions)
        {
            bool found = await ctx.OrgAuditLog
                .AsNoTracking()
                .AnyAsync(e => e.OrgId == orgId && e.Action == action);
            Assert.True(found, $"Expected audit entry with action='{action}' but none found");
        }
    }
}

// =============================================================================
// HeaderBasedTestCurrentUser — reads test auth from request headers
// Used in integration tests to bypass real JWT auth while still exercising
// the ICurrentUser abstraction at use-case level.
// =============================================================================

/// <summary>
/// ICurrentUser implementation for integration tests.
/// Reads user identity from "X-Test-User-Id" and "X-Test-User-Email" request headers.
/// When neither header is present → IsAuthenticated=false (anonymous).
/// </summary>
internal sealed class HeaderBasedTestCurrentUser : ICurrentUser
{
    private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _httpContextAccessor;

    public HeaderBasedTestCurrentUser(Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor)
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
            if (Guid.TryParse(userId, out Guid parsed))
                return parsed;
            return null;
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
