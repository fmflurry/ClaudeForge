using ClaudeForge.Core.Identity;
using ClaudeForge.Core.Modules.Organizations.Ports;
using ClaudeForge.Core.Modules.Organizations.UseCases;
using ClaudeForge.Core.Shared.Authorization;
using ClaudeForge.Core.Shared.Exceptions;
using NSubstitute;

namespace ClaudeForge.Tests.Unit.Organizations;

/// <summary>
/// Unit tests for Group 6, Task 6.2 — CreateOrganizationUseCase.
///
/// These tests are RED because the production types listed below do not yet exist.
/// The coder MUST create (in the exact namespaces):
///
///   NAMESPACE: ClaudeForge.Core.Modules.Organizations.Ports
///
///   interface IOrganizationStorePort
///     Task&lt;OrganizationDto?&gt; FindByNameNormalizedAsync(string nameNormalized, CancellationToken ct = default)
///     Task&lt;OrganizationDto&gt;  CreateAsync(CreateOrganizationRecord record, CancellationToken ct = default)
///
///   interface IMembershipStorePort
///     Task AddMemberAsync(Guid orgId, Guid userId, OrgRole role, CancellationToken ct = default)
///     Task RemoveMemberAsync(Guid orgId, Guid userId, CancellationToken ct = default)
///     Task UpdateMemberRoleAsync(Guid orgId, Guid userId, OrgRole newRole, CancellationToken ct = default)
///     Task&lt;int&gt; CountOwnersAsync(Guid orgId, CancellationToken ct = default)
///     Task&lt;MemberDto?&gt; FindMemberAsync(Guid orgId, Guid userId, CancellationToken ct = default)
///     Task&lt;IReadOnlyList&lt;MemberDto&gt;&gt; ListMembersAsync(Guid orgId, CancellationToken ct = default)
///     Task&lt;IReadOnlyList&lt;OrgSummaryDto&gt;&gt; ListOrgsForUserAsync(Guid userId, CancellationToken ct = default)
///
///   interface IInvitationStorePort
///     Task&lt;InvitationDto&gt;  CreateAsync(CreateInvitationRecord record, CancellationToken ct = default)
///     Task&lt;InvitationDto?&gt; FindByIdAsync(Guid id, CancellationToken ct = default)
///     Task&lt;InvitationDto?&gt; FindPendingByOrgAndEmailAsync(Guid orgId, string emailNormalized, CancellationToken ct = default)
///     Task UpdateStatusAsync(Guid id, string newStatus, DateTimeOffset? acceptedAt, DateTimeOffset? revokedAt, CancellationToken ct = default)
///
///   interface IInvitationEmailPort
///     Task SendInvitationAsync(string toEmail, string orgName, string inviterName, string invitationToken, CancellationToken ct = default)
///
///   interface IOrgAuditLogPort
///     Task AppendAsync(Guid orgId, Guid actorUserId, string action, string target, CancellationToken ct = default)
///
///   NAMESPACE: ClaudeForge.Core.Modules.Organizations.UseCases
///
///   record CreateOrganizationCommand(string Name, string? Slug)
///
///   record OrganizationDto(Guid Id, string Name, string NameNormalized, string Slug, Guid CreatedBy, DateTimeOffset CreatedAt)
///
///   record CreateOrganizationRecord(
///     Guid Id, string Name, string NameNormalized, string Slug, Guid CreatedBy, DateTimeOffset CreatedAt)
///
///   record MemberDto(Guid UserId, string Email, string DisplayName, OrgRole Role, DateTimeOffset JoinedAt)
///
///   record OrgSummaryDto(Guid Id, string Name, string Slug, OrgRole UserRole)
///
///   record InvitationDto(
///     Guid Id, Guid OrgId, string EmailNormalized, Guid InvitedBy, OrgRole Role,
///     string Status, string Token, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt,
///     DateTimeOffset? AcceptedAt, DateTimeOffset? RevokedAt)
///
///   record CreateInvitationRecord(
///     Guid Id, Guid OrgId, string EmailNormalized, Guid InvitedBy, OrgRole Role,
///     string Token, DateTimeOffset ExpiresAt)
///
///   sealed class CreateOrganizationUseCase
///     CreateOrganizationUseCase(
///         ICurrentUser currentUser,
///         IOrganizationStorePort orgStore,
///         IMembershipStorePort membershipStore,
///         IOrgAuditLogPort auditLog)
///
///     Task&lt;OrganizationDto&gt; ExecuteAsync(CreateOrganizationCommand command, CancellationToken ct = default)
///
///     Behavior:
///       - Unauthenticated caller → throws UnauthenticatedException (StatusCode 401)
///       - Null/empty name → throws ProblemDetailsException with StatusCode 400
///       - Normalizes name to lower-invariant; derives slug from name if not provided
///       - Duplicate name (same nameNormalized) → throws DuplicateOrgNameException (StatusCode 409)
///       - Creates org with new Guid PK; creates membership row with OrgRole.Owner
///       - Appends audit entry action="org.created", target="org:{orgId}"
///       - Returns OrganizationDto for the created org
///
///   sealed class DuplicateOrgNameException : ProblemDetailsException
///     StatusCode = 409
///     Message = "An organization with this name already exists."
///
///   sealed class UnauthenticatedException : ProblemDetailsException
///     StatusCode = 401
///     Message = "Authentication is required."
///
/// Constraints:
///   - Immutable patterns; no mutation of parameters
///   - No 'any' equivalent (nullable annotations throughout)
///   - Use OrgRole.Owner (value object from ClaudeForge.Core.Identity) — not string literals
/// </summary>
public sealed class CreateOrganizationUseCaseTests
{
    // =========================================================================
    // Helpers
    // =========================================================================

    private static ICurrentUser MakeAuthUser(Guid userId, string email = "user@example.com")
    {
        ICurrentUser user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns(userId);
        user.Email.Returns(email);
        return user;
    }

    private static ICurrentUser MakeAnonUser()
    {
        ICurrentUser user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(false);
        user.UserId.Returns((Guid?)null);
        user.Email.Returns((string?)null);
        return user;
    }

    private static OrganizationDto MakeOrgDto(string name, Guid createdBy) => new(
        Id: Guid.NewGuid(),
        Name: name,
        NameNormalized: name.ToLowerInvariant(),
        Slug: name.ToLowerInvariant().Replace(' ', '-'),
        CreatedBy: createdBy,
        CreatedAt: DateTimeOffset.UtcNow);

    // =========================================================================
    // Authentication gate
    // =========================================================================

    [Fact]
    public async Task Execute_Unauthenticated_ThrowsUnauthenticatedException()
    {
        // Arrange
        ICurrentUser anonUser = MakeAnonUser();
        IOrganizationStorePort orgStore = Substitute.For<IOrganizationStorePort>();
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();

        CreateOrganizationUseCase useCase = new(anonUser, orgStore, membershipStore, auditLog);
        CreateOrganizationCommand command = new("Acme Corp", null);

        // Act & Assert
        UnauthenticatedException ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => useCase.ExecuteAsync(command));

        Assert.Equal(401, ex.StatusCode);
        Assert.Equal("Authentication is required.", ex.Message);

        // Must not attempt any storage operation
        await orgStore.DidNotReceive().FindByNameNormalizedAsync(Arg.Any<string>());
        await orgStore.DidNotReceive().CreateAsync(Arg.Any<CreateOrganizationRecord>());
        await membershipStore.DidNotReceive().AddMemberAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<OrgRole>());
    }

    // =========================================================================
    // Input validation
    // =========================================================================

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Execute_EmptyOrWhitespaceName_ThrowsBadRequest(string name)
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(userId);
        IOrganizationStorePort orgStore = Substitute.For<IOrganizationStorePort>();
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();

        CreateOrganizationUseCase useCase = new(authUser, orgStore, membershipStore, auditLog);
        CreateOrganizationCommand command = new(name, null);

        // Act & Assert
        ProblemDetailsException ex = await Assert.ThrowsAsync<ProblemDetailsException>(
            () => useCase.ExecuteAsync(command));

        Assert.Equal(400, ex.StatusCode);

        await orgStore.DidNotReceive().CreateAsync(Arg.Any<CreateOrganizationRecord>());
    }

    // =========================================================================
    // Duplicate name — 409 Conflict
    // =========================================================================

    [Fact]
    public async Task Execute_DuplicateName_ThrowsDuplicateOrgNameException()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(userId);
        IOrganizationStorePort orgStore = Substitute.For<IOrganizationStorePort>();
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();

        OrganizationDto existingOrg = MakeOrgDto("Acme Corp", Guid.NewGuid());
        orgStore.FindByNameNormalizedAsync("acme corp")
            .Returns(existingOrg);

        CreateOrganizationUseCase useCase = new(authUser, orgStore, membershipStore, auditLog);
        CreateOrganizationCommand command = new("Acme Corp", null);

        // Act & Assert
        DuplicateOrgNameException ex = await Assert.ThrowsAsync<DuplicateOrgNameException>(
            () => useCase.ExecuteAsync(command));

        Assert.Equal(409, ex.StatusCode);
        Assert.Equal("An organization with this name already exists.", ex.Message);

        // No membership or org creation
        await orgStore.DidNotReceive().CreateAsync(Arg.Any<CreateOrganizationRecord>());
        await membershipStore.DidNotReceive().AddMemberAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<OrgRole>());
    }

    // =========================================================================
    // Happy path — authenticated create → creator gets owner role
    // =========================================================================

    [Fact]
    public async Task Execute_ValidRequest_CreatesOrgAndAssignsOwnerRole()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(userId, "creator@example.com");
        IOrganizationStorePort orgStore = Substitute.For<IOrganizationStorePort>();
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();

        orgStore.FindByNameNormalizedAsync("my org")
            .Returns((OrganizationDto?)null);

        OrganizationDto createdOrg = MakeOrgDto("My Org", userId);
        orgStore.CreateAsync(Arg.Any<CreateOrganizationRecord>())
            .Returns(createdOrg);

        CreateOrganizationUseCase useCase = new(authUser, orgStore, membershipStore, auditLog);
        CreateOrganizationCommand command = new("My Org", null);

        // Act
        OrganizationDto result = await useCase.ExecuteAsync(command);

        // Assert — org was persisted
        Assert.NotNull(result);
        Assert.Equal("My Org", result.Name);

        // Creator must be added as Owner (not string literal — OrgRole.Owner)
        await membershipStore.Received(1).AddMemberAsync(
            result.Id,
            userId,
            OrgRole.Owner,
            Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Name normalization — case-insensitive uniqueness check
    // =========================================================================

    [Fact]
    public async Task Execute_UpperCaseName_NormalizesToLowerForDuplicateCheck()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(userId);
        IOrganizationStorePort orgStore = Substitute.For<IOrganizationStorePort>();
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();

        orgStore.FindByNameNormalizedAsync(Arg.Any<string>())
            .Returns((OrganizationDto?)null);

        OrganizationDto createdOrg = MakeOrgDto("ACME CORP", userId);
        orgStore.CreateAsync(Arg.Any<CreateOrganizationRecord>())
            .Returns(createdOrg);

        CreateOrganizationUseCase useCase = new(authUser, orgStore, membershipStore, auditLog);
        CreateOrganizationCommand command = new("ACME CORP", null);

        // Act
        await useCase.ExecuteAsync(command);

        // Assert — the store was called with the lower-invariant form
        await orgStore.Received(1).FindByNameNormalizedAsync("acme corp", Arg.Any<CancellationToken>());

        // The record passed to CreateAsync must have NameNormalized = lower-invariant
        await orgStore.Received(1).CreateAsync(
            Arg.Is<CreateOrganizationRecord>(r => r.NameNormalized == "acme corp"),
            Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Slug auto-derivation when not provided
    // =========================================================================

    [Fact]
    public async Task Execute_SlugNotProvided_DerivesSlugFromName()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(userId);
        IOrganizationStorePort orgStore = Substitute.For<IOrganizationStorePort>();
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();

        orgStore.FindByNameNormalizedAsync(Arg.Any<string>())
            .Returns((OrganizationDto?)null);

        OrganizationDto createdOrg = MakeOrgDto("My Cool Org", userId);
        orgStore.CreateAsync(Arg.Any<CreateOrganizationRecord>())
            .Returns(createdOrg);

        CreateOrganizationUseCase useCase = new(authUser, orgStore, membershipStore, auditLog);
        CreateOrganizationCommand command = new("My Cool Org", Slug: null);

        // Act
        await useCase.ExecuteAsync(command);

        // Assert — slug must be derived (spaces → hyphens, lower-case)
        await orgStore.Received(1).CreateAsync(
            Arg.Is<CreateOrganizationRecord>(r =>
                r.Slug == "my-cool-org"),
            Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Audit log appended on successful create
    // =========================================================================

    [Fact]
    public async Task Execute_ValidRequest_AppendsAuditLogEntry()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(userId);
        IOrganizationStorePort orgStore = Substitute.For<IOrganizationStorePort>();
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();

        orgStore.FindByNameNormalizedAsync(Arg.Any<string>())
            .Returns((OrganizationDto?)null);

        OrganizationDto createdOrg = MakeOrgDto("Audit Org", userId);
        orgStore.CreateAsync(Arg.Any<CreateOrganizationRecord>())
            .Returns(createdOrg);

        CreateOrganizationUseCase useCase = new(authUser, orgStore, membershipStore, auditLog);

        // Act
        OrganizationDto result = await useCase.ExecuteAsync(new CreateOrganizationCommand("Audit Org", null));

        // Assert — audit entry must be appended
        await auditLog.Received(1).AppendAsync(
            result.Id,
            userId,
            "org.created",
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }
}
