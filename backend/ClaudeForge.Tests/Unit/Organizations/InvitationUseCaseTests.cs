using ClaudeForge.Core.Identity;
using ClaudeForge.Core.Modules.Organizations.Ports;
using ClaudeForge.Core.Modules.Organizations.UseCases;
using ClaudeForge.Core.Shared.Authorization;
using ClaudeForge.Core.Shared.Exceptions;
using NSubstitute;

namespace ClaudeForge.Tests.Unit.Organizations;

/// <summary>
/// Unit tests for Group 6, Tasks 6.6 and 6.8 — IssueInvitationUseCase,
/// AcceptInvitationUseCase, RevokeInvitationUseCase.
///
/// These tests are RED. The coder MUST create:
///
///   NAMESPACE: ClaudeForge.Core.Modules.Organizations.UseCases
///
///   sealed class IssueInvitationUseCase
///     IssueInvitationUseCase(ICurrentUser currentUser, IMembershipStorePort membershipStore,
///                            IInvitationStorePort invitationStore, IInvitationEmailPort emailPort,
///                            IOrgMembershipQueryPort membershipQuery, IOrgAuditLogPort auditLog)
///     Task&lt;InvitationDto&gt; ExecuteAsync(Guid orgId, string email, OrgRole role, CancellationToken ct = default)
///     Behavior:
///       - Unauthenticated → throws UnauthenticatedException (401)
///       - Caller not owner/admin → throws ForbiddenException (403)
///       - Email already a member of the org → throws AlreadyMemberException (409)
///       - Pending invite already exists for that email+org → throws DuplicateInvitationException (409)
///       - Valid case → creates pending InvitationDto with unique opaque token + 7-day expiry,
///         best-effort sends email (failure is swallowed — invite is still created),
///         appends audit entry action="invite.sent"
///
///   sealed class AcceptInvitationUseCase
///     AcceptInvitationUseCase(ICurrentUser currentUser, IInvitationStorePort invitationStore,
///                             IMembershipStorePort membershipStore,
///                             IOrgMembershipQueryPort membershipQuery, IOrgAuditLogPort auditLog)
///     Task ExecuteAsync(Guid orgId, Guid invitationId, CancellationToken ct = default)
///     Behavior:
///       - Unauthenticated → throws UnauthenticatedException (401)
///       - Invitation not found OR email does not match current user → throws InvitationNotFoundException (404)
///       - Invitation found but status != "pending" → throws InvitationGoneException (410)
///       - Valid pending → transitions status to "accepted", creates membership with the invitation's role,
///         invalidates cache, appends audit entry action="invite.accepted"
///
///   sealed class RevokeInvitationUseCase
///     RevokeInvitationUseCase(ICurrentUser currentUser, IInvitationStorePort invitationStore,
///                             IMembershipStorePort membershipStore,
///                             IOrgMembershipQueryPort membershipQuery, IOrgAuditLogPort auditLog)
///     Task ExecuteAsync(Guid orgId, Guid invitationId, CancellationToken ct = default)
///     Behavior:
///       - Unauthenticated → throws UnauthenticatedException (401)
///       - Caller not owner/admin → throws ForbiddenException (403)
///       - Invitation not found in this org → throws InvitationNotFoundException (404)
///       - Invitation found but status != "pending" → throws InvitationGoneException (410) [cannot revoke non-pending]
///       - Valid pending → transitions status to "revoked", sets revokedAt, appends audit action="invite.revoked"
///
///   sealed class AlreadyMemberException : ProblemDetailsException
///     StatusCode = 409
///     Message = "The user is already a member of this organization."
///
///   sealed class DuplicateInvitationException : ProblemDetailsException
///     StatusCode = 409
///     Message = "A pending invitation already exists for this email address."
///
///   sealed class InvitationNotFoundException : ProblemDetailsException
///     StatusCode = 404
///     Message = "Invitation not found."
///
///   sealed class InvitationGoneException : ProblemDetailsException
///     StatusCode = 410
///     Message = "This invitation is no longer valid."
/// </summary>

// ---------------------------------------------------------------------------
// Shared configurable test doubles
// ---------------------------------------------------------------------------

file sealed class StubMembershipQueryPort : IOrgMembershipQueryPort
{
    private readonly HashSet<Guid> _invalidated = new();
    private readonly Func<Guid, Guid, string?, bool> _isMemberFunc;

    public StubMembershipQueryPort(Func<Guid, Guid, string?, bool> isMemberFunc)
    {
        _isMemberFunc = isMemberFunc;
    }

    public Task<Guid[]> GetOrgIdsForUserAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult(Array.Empty<Guid>());

    public Task<bool> IsMemberAsync(Guid userId, Guid orgId, string? minRole = null, CancellationToken ct = default)
        => Task.FromResult(_isMemberFunc(userId, orgId, minRole));

    public void InvalidateUser(Guid userId) => _invalidated.Add(userId);

    public bool WasInvalidated(Guid userId) => _invalidated.Contains(userId);
}

// ===========================================================================
// IssueInvitationUseCase tests
// ===========================================================================

public sealed class IssueInvitationUseCaseTests
{
    private static ICurrentUser MakeAuthUser(Guid userId, string email = "caller@example.com")
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

    private static InvitationDto MakePendingInvite(
        Guid orgId, Guid invitedBy, string email, OrgRole role) => new(
        Id: Guid.NewGuid(),
        OrgId: orgId,
        EmailNormalized: email.ToLowerInvariant(),
        InvitedBy: invitedBy,
        Role: role,
        Status: "pending",
        Token: Guid.NewGuid().ToString("N"),
        CreatedAt: DateTimeOffset.UtcNow,
        ExpiresAt: DateTimeOffset.UtcNow.AddDays(7),
        AcceptedAt: null,
        RevokedAt: null);

    // =========================================================================
    // Authentication gate
    // =========================================================================

    [Fact]
    public async Task Execute_Unauthenticated_ThrowsUnauthenticatedException()
    {
        // Arrange
        ICurrentUser anonUser = MakeAnonUser();
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IInvitationStorePort invitationStore = Substitute.For<IInvitationStorePort>();
        IInvitationEmailPort emailPort = Substitute.For<IInvitationEmailPort>();
        StubMembershipQueryPort membershipQuery = new((_, _, _) => false);
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();

        IssueInvitationUseCase useCase = new(
            anonUser, membershipStore, invitationStore, emailPort, membershipQuery, auditLog);

        // Act & Assert
        UnauthenticatedException ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => useCase.ExecuteAsync(Guid.NewGuid(), "target@example.com", OrgRole.Member));

        Assert.Equal(401, ex.StatusCode);
        await invitationStore.DidNotReceive().CreateAsync(Arg.Any<CreateInvitationRecord>());
    }

    // =========================================================================
    // Authorization — member (non-owner/admin) invite attempt → 403
    // =========================================================================

    [Fact]
    public async Task Execute_CallerIsPlainMember_ThrowsForbiddenException()
    {
        // Arrange
        Guid callerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(callerId);
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IInvitationStorePort invitationStore = Substitute.For<IInvitationStorePort>();
        IInvitationEmailPort emailPort = Substitute.For<IInvitationEmailPort>();
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();

        // Caller is member only — does NOT satisfy minRole="admin"
        StubMembershipQueryPort membershipQuery =
            new((uid, oid, minRole) => minRole == null || minRole == "member");

        IssueInvitationUseCase useCase = new(
            authUser, membershipStore, invitationStore, emailPort, membershipQuery, auditLog);

        // Act & Assert
        ForbiddenException ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => useCase.ExecuteAsync(orgId, "target@example.com", OrgRole.Member));

        Assert.Equal(403, ex.StatusCode);
        await invitationStore.DidNotReceive().CreateAsync(Arg.Any<CreateInvitationRecord>());
    }

    // =========================================================================
    // Admin can invite (not just owner)
    // =========================================================================

    [Fact]
    public async Task Execute_CallerIsAdmin_CreatesInvitation()
    {
        // Arrange
        Guid callerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(callerId, "admin@example.com");
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IInvitationStorePort invitationStore = Substitute.For<IInvitationStorePort>();
        IInvitationEmailPort emailPort = Substitute.For<IInvitationEmailPort>();
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();

        // Caller is admin → satisfies minRole="admin"
        StubMembershipQueryPort membershipQuery = new((uid, oid, _) => true);

        // Target is not yet a member
        membershipStore.FindMemberAsync(orgId, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((MemberDto?)null);

        // No pending invite yet
        invitationStore.FindPendingByOrgAndEmailAsync(orgId, "target@example.com", Arg.Any<CancellationToken>())
            .Returns((InvitationDto?)null);

        InvitationDto created = MakePendingInvite(orgId, callerId, "target@example.com", OrgRole.Member);
        invitationStore.CreateAsync(Arg.Any<CreateInvitationRecord>(), Arg.Any<CancellationToken>())
            .Returns(created);

        IssueInvitationUseCase useCase = new(
            authUser, membershipStore, invitationStore, emailPort, membershipQuery, auditLog);

        // Act
        InvitationDto result = await useCase.ExecuteAsync(orgId, "target@example.com", OrgRole.Member);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("pending", result.Status);
        await invitationStore.Received(1).CreateAsync(Arg.Any<CreateInvitationRecord>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Invite existing member → 409
    // =========================================================================

    [Fact]
    public async Task Execute_TargetIsAlreadyMember_ThrowsAlreadyMemberException()
    {
        // Arrange
        Guid callerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        Guid targetUserId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(callerId, "owner@example.com");
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IInvitationStorePort invitationStore = Substitute.For<IInvitationStorePort>();
        IInvitationEmailPort emailPort = Substitute.For<IInvitationEmailPort>();
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();
        StubMembershipQueryPort membershipQuery = new((uid, oid, _) => true);

        // The target email belongs to a user already in the org
        MemberDto existingMember = new(targetUserId, "target@example.com", "Target", OrgRole.Member, DateTimeOffset.UtcNow);
        membershipStore.FindMemberByEmailAsync(orgId, "target@example.com", Arg.Any<CancellationToken>())
            .Returns(existingMember);

        IssueInvitationUseCase useCase = new(
            authUser, membershipStore, invitationStore, emailPort, membershipQuery, auditLog);

        // Act & Assert
        AlreadyMemberException ex = await Assert.ThrowsAsync<AlreadyMemberException>(
            () => useCase.ExecuteAsync(orgId, "target@example.com", OrgRole.Member));

        Assert.Equal(409, ex.StatusCode);
        Assert.Equal("The user is already a member of this organization.", ex.Message);

        await invitationStore.DidNotReceive().CreateAsync(Arg.Any<CreateInvitationRecord>());
    }

    // =========================================================================
    // Duplicate pending invite → 409
    // =========================================================================

    [Fact]
    public async Task Execute_PendingInviteAlreadyExists_ThrowsDuplicateInvitationException()
    {
        // Arrange
        Guid callerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(callerId, "owner@example.com");
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IInvitationStorePort invitationStore = Substitute.For<IInvitationStorePort>();
        IInvitationEmailPort emailPort = Substitute.For<IInvitationEmailPort>();
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();
        StubMembershipQueryPort membershipQuery = new((uid, oid, _) => true);

        // No existing membership (not yet a member)
        membershipStore.FindMemberByEmailAsync(orgId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((MemberDto?)null);

        // But a pending invite already exists
        InvitationDto existingInvite = MakePendingInvite(orgId, callerId, "new@example.com", OrgRole.Member);
        invitationStore.FindPendingByOrgAndEmailAsync(orgId, "new@example.com", Arg.Any<CancellationToken>())
            .Returns(existingInvite);

        IssueInvitationUseCase useCase = new(
            authUser, membershipStore, invitationStore, emailPort, membershipQuery, auditLog);

        // Act & Assert
        DuplicateInvitationException ex = await Assert.ThrowsAsync<DuplicateInvitationException>(
            () => useCase.ExecuteAsync(orgId, "new@example.com", OrgRole.Member));

        Assert.Equal(409, ex.StatusCode);
        await invitationStore.DidNotReceive().CreateAsync(Arg.Any<CreateInvitationRecord>());
    }

    // =========================================================================
    // Email port failure is best-effort — invite still recorded
    // =========================================================================

    [Fact]
    public async Task Execute_EmailPortThrows_InviteStillCreatedAndStatusVisible()
    {
        // Arrange — Task 6.12: IInvitationEmailPort failure path
        Guid callerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(callerId, "owner@example.com");
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IInvitationStorePort invitationStore = Substitute.For<IInvitationStorePort>();
        IInvitationEmailPort emailPort = Substitute.For<IInvitationEmailPort>();
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();
        StubMembershipQueryPort membershipQuery = new((uid, oid, _) => true);

        membershipStore.FindMemberByEmailAsync(orgId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((MemberDto?)null);

        invitationStore.FindPendingByOrgAndEmailAsync(orgId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((InvitationDto?)null);

        InvitationDto created = MakePendingInvite(orgId, callerId, "target@example.com", OrgRole.Member);
        invitationStore.CreateAsync(Arg.Any<CreateInvitationRecord>(), Arg.Any<CancellationToken>())
            .Returns(created);

        // Email port throws (e.g. SMTP unavailable)
        emailPort.SendInvitationAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .ThrowsAsync(new InvalidOperationException("SMTP server unreachable"));

        IssueInvitationUseCase useCase = new(
            authUser, membershipStore, invitationStore, emailPort, membershipQuery, auditLog);

        // Act — must NOT throw; email failure is best-effort
        InvitationDto result = await useCase.ExecuteAsync(orgId, "target@example.com", OrgRole.Member);

        // Assert — invitation was still created
        Assert.NotNull(result);
        Assert.Equal("pending", result.Status);
        await invitationStore.Received(1).CreateAsync(Arg.Any<CreateInvitationRecord>(), Arg.Any<CancellationToken>());

        // Email port was called (even though it failed)
        await emailPort.Received(1).SendInvitationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Happy path — owner invite → 201 pending + audit appended
    // =========================================================================

    [Fact]
    public async Task Execute_ValidOwnerInvite_AppendsPendingAuditEntry()
    {
        // Arrange
        Guid callerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(callerId, "owner@example.com");
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IInvitationStorePort invitationStore = Substitute.For<IInvitationStorePort>();
        IInvitationEmailPort emailPort = Substitute.For<IInvitationEmailPort>();
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();
        StubMembershipQueryPort membershipQuery = new((uid, oid, _) => true);

        membershipStore.FindMemberByEmailAsync(orgId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((MemberDto?)null);

        invitationStore.FindPendingByOrgAndEmailAsync(orgId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((InvitationDto?)null);

        InvitationDto created = MakePendingInvite(orgId, callerId, "invited@example.com", OrgRole.Member);
        invitationStore.CreateAsync(Arg.Any<CreateInvitationRecord>(), Arg.Any<CancellationToken>())
            .Returns(created);

        IssueInvitationUseCase useCase = new(
            authUser, membershipStore, invitationStore, emailPort, membershipQuery, auditLog);

        // Act
        InvitationDto result = await useCase.ExecuteAsync(orgId, "invited@example.com", OrgRole.Member);

        // Assert — audit entry appended
        await auditLog.Received(1).AppendAsync(
            orgId,
            callerId,
            "invite.sent",
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        Assert.Equal("pending", result.Status);
    }
}

// ===========================================================================
// AcceptInvitationUseCase tests
// ===========================================================================

public sealed class AcceptInvitationUseCaseTests
{
    private static ICurrentUser MakeAuthUser(Guid userId, string email = "target@example.com")
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

    private static InvitationDto MakeInvite(
        Guid inviteId, Guid orgId, string emailNormalized, string status,
        OrgRole role = default!) => new(
        Id: inviteId,
        OrgId: orgId,
        EmailNormalized: emailNormalized,
        InvitedBy: Guid.NewGuid(),
        Role: role ?? OrgRole.Member,
        Status: status,
        Token: Guid.NewGuid().ToString("N"),
        CreatedAt: DateTimeOffset.UtcNow.AddDays(-1),
        ExpiresAt: DateTimeOffset.UtcNow.AddDays(6),
        AcceptedAt: null,
        RevokedAt: null);

    // =========================================================================
    // Authentication gate
    // =========================================================================

    [Fact]
    public async Task Execute_Unauthenticated_ThrowsUnauthenticatedException()
    {
        // Arrange
        ICurrentUser anonUser = MakeAnonUser();
        IInvitationStorePort invitationStore = Substitute.For<IInvitationStorePort>();
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        StubMembershipQueryPort membershipQuery = new((_, _, _) => false);
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();

        AcceptInvitationUseCase useCase = new(
            anonUser, invitationStore, membershipStore, membershipQuery, auditLog);

        // Act & Assert
        UnauthenticatedException ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => useCase.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(401, ex.StatusCode);
    }

    // =========================================================================
    // Invitation not found or email mismatch → 404
    // =========================================================================

    [Fact]
    public async Task Execute_InvitationNotFound_ThrowsInvitationNotFoundException()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(userId, "target@example.com");
        IInvitationStorePort invitationStore = Substitute.For<IInvitationStorePort>();
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        StubMembershipQueryPort membershipQuery = new((_, _, _) => false);
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();

        invitationStore.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((InvitationDto?)null);

        AcceptInvitationUseCase useCase = new(
            authUser, invitationStore, membershipStore, membershipQuery, auditLog);

        // Act & Assert
        InvitationNotFoundException ex = await Assert.ThrowsAsync<InvitationNotFoundException>(
            () => useCase.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(404, ex.StatusCode);
        await membershipStore.DidNotReceive().AddMemberAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<OrgRole>());
    }

    [Fact]
    public async Task Execute_InvitationEmailMismatch_ThrowsInvitationNotFoundException()
    {
        // Arrange — invitation exists but is for a different email
        Guid userId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        Guid inviteId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(userId, "someone-else@example.com");
        IInvitationStorePort invitationStore = Substitute.For<IInvitationStorePort>();
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        StubMembershipQueryPort membershipQuery = new((_, _, _) => false);
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();

        InvitationDto invite = MakeInvite(inviteId, orgId, "different@example.com", "pending");
        invitationStore.FindByIdAsync(inviteId, Arg.Any<CancellationToken>())
            .Returns(invite);

        AcceptInvitationUseCase useCase = new(
            authUser, invitationStore, membershipStore, membershipQuery, auditLog);

        // Act & Assert — must not reveal whether invite exists for a different email
        InvitationNotFoundException ex = await Assert.ThrowsAsync<InvitationNotFoundException>(
            () => useCase.ExecuteAsync(orgId, inviteId));

        Assert.Equal(404, ex.StatusCode);
    }

    // =========================================================================
    // Non-pending invite → 410 Gone
    // =========================================================================

    [Theory]
    [InlineData("revoked")]
    [InlineData("expired")]
    [InlineData("accepted")]
    public async Task Execute_NonPendingInvitation_ThrowsInvitationGoneException(string status)
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        Guid inviteId = Guid.NewGuid();
        string email = "target@example.com";
        ICurrentUser authUser = MakeAuthUser(userId, email);
        IInvitationStorePort invitationStore = Substitute.For<IInvitationStorePort>();
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        StubMembershipQueryPort membershipQuery = new((_, _, _) => false);
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();

        InvitationDto invite = MakeInvite(inviteId, orgId, email, status);
        invitationStore.FindByIdAsync(inviteId, Arg.Any<CancellationToken>())
            .Returns(invite);

        AcceptInvitationUseCase useCase = new(
            authUser, invitationStore, membershipStore, membershipQuery, auditLog);

        // Act & Assert — non-pending → 410 Gone
        InvitationGoneException ex = await Assert.ThrowsAsync<InvitationGoneException>(
            () => useCase.ExecuteAsync(orgId, inviteId));

        Assert.Equal(410, ex.StatusCode);
        Assert.Equal("This invitation is no longer valid.", ex.Message);

        await membershipStore.DidNotReceive().AddMemberAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<OrgRole>());
    }

    // =========================================================================
    // Happy path — accept valid pending → 200 + member role + cache invalidation + audit
    // =========================================================================

    [Fact]
    public async Task Execute_ValidPendingInvite_CreatesMembershipAndInvalidatesCache()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        Guid inviteId = Guid.NewGuid();
        string email = "target@example.com";
        ICurrentUser authUser = MakeAuthUser(userId, email);
        IInvitationStorePort invitationStore = Substitute.For<IInvitationStorePort>();
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();
        StubMembershipQueryPort membershipQuery = new((_, _, _) => false);

        InvitationDto invite = MakeInvite(inviteId, orgId, email, "pending", OrgRole.Member);
        invitationStore.FindByIdAsync(inviteId, Arg.Any<CancellationToken>())
            .Returns(invite);

        AcceptInvitationUseCase useCase = new(
            authUser, invitationStore, membershipStore, membershipQuery, auditLog);

        // Act
        await useCase.ExecuteAsync(orgId, inviteId);

        // Assert — status updated to "accepted"
        await invitationStore.Received(1).UpdateStatusAsync(
            Arg.Is(inviteId),
            Arg.Is("accepted"),
            Arg.Is<DateTimeOffset?>(x => x != null),  // acceptedAt is set
            Arg.Is<DateTimeOffset?>(x => x == null),  // revokedAt stays null
            Arg.Any<CancellationToken>());

        // Assert — membership added with role from invitation
        await membershipStore.Received(1).AddMemberAsync(
            orgId, userId, OrgRole.Member, Arg.Any<CancellationToken>());

        // Assert — cache invalidated for the accepting user
        Assert.True(membershipQuery.WasInvalidated(userId),
            "InvalidateUser must be called after membership is created");
    }

    [Fact]
    public async Task Execute_ValidPendingInvite_AppendsAuditEntry()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        Guid inviteId = Guid.NewGuid();
        string email = "target@example.com";
        ICurrentUser authUser = MakeAuthUser(userId, email);
        IInvitationStorePort invitationStore = Substitute.For<IInvitationStorePort>();
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();
        StubMembershipQueryPort membershipQuery = new((_, _, _) => false);

        InvitationDto invite = MakeInvite(inviteId, orgId, email, "pending", OrgRole.Member);
        invitationStore.FindByIdAsync(inviteId, Arg.Any<CancellationToken>())
            .Returns(invite);

        AcceptInvitationUseCase useCase = new(
            authUser, invitationStore, membershipStore, membershipQuery, auditLog);

        // Act
        await useCase.ExecuteAsync(orgId, inviteId);

        // Assert — audit appended
        await auditLog.Received(1).AppendAsync(
            orgId,
            userId,
            "invite.accepted",
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }
}

// ===========================================================================
// RevokeInvitationUseCase tests
// ===========================================================================

public sealed class RevokeInvitationUseCaseTests
{
    private static ICurrentUser MakeAuthUser(Guid userId, string email = "admin@example.com")
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
        return user;
    }

    private static InvitationDto MakeInvite(Guid inviteId, Guid orgId, string status) => new(
        Id: inviteId,
        OrgId: orgId,
        EmailNormalized: "invitee@example.com",
        InvitedBy: Guid.NewGuid(),
        Role: OrgRole.Member,
        Status: status,
        Token: Guid.NewGuid().ToString("N"),
        CreatedAt: DateTimeOffset.UtcNow.AddDays(-1),
        ExpiresAt: DateTimeOffset.UtcNow.AddDays(6),
        AcceptedAt: null,
        RevokedAt: null);

    [Fact]
    public async Task Execute_Unauthenticated_ThrowsUnauthenticatedException()
    {
        // Arrange
        ICurrentUser anonUser = MakeAnonUser();
        IInvitationStorePort invitationStore = Substitute.For<IInvitationStorePort>();
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        StubMembershipQueryPort membershipQuery = new((_, _, _) => false);
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();

        RevokeInvitationUseCase useCase = new(
            anonUser, invitationStore, membershipStore, membershipQuery, auditLog);

        // Act & Assert
        UnauthenticatedException ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => useCase.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task Execute_CallerIsPlainMember_ThrowsForbiddenException()
    {
        // Arrange
        Guid callerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(callerId);
        IInvitationStorePort invitationStore = Substitute.For<IInvitationStorePort>();
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();

        // Caller is member only — does NOT satisfy minRole="admin"
        StubMembershipQueryPort membershipQuery =
            new((uid, oid, minRole) => minRole == null || minRole == "member");

        RevokeInvitationUseCase useCase = new(
            authUser, invitationStore, membershipStore, membershipQuery, auditLog);

        // Act & Assert
        ForbiddenException ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => useCase.ExecuteAsync(orgId, Guid.NewGuid()));

        Assert.Equal(403, ex.StatusCode);
        await invitationStore.DidNotReceive().UpdateStatusAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateTimeOffset?>(),
            Arg.Any<DateTimeOffset?>());
    }

    [Fact]
    public async Task Execute_InvitationNotFound_ThrowsInvitationNotFoundException()
    {
        // Arrange
        Guid callerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(callerId);
        IInvitationStorePort invitationStore = Substitute.For<IInvitationStorePort>();
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();
        StubMembershipQueryPort membershipQuery = new((uid, oid, _) => true);

        invitationStore.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((InvitationDto?)null);

        RevokeInvitationUseCase useCase = new(
            authUser, invitationStore, membershipStore, membershipQuery, auditLog);

        // Act & Assert
        InvitationNotFoundException ex = await Assert.ThrowsAsync<InvitationNotFoundException>(
            () => useCase.ExecuteAsync(orgId, Guid.NewGuid()));

        Assert.Equal(404, ex.StatusCode);
    }

    [Theory]
    [InlineData("accepted")]
    [InlineData("revoked")]
    [InlineData("expired")]
    public async Task Execute_NonPendingInvitation_ThrowsInvitationGoneException(string status)
    {
        // Arrange — cannot revoke a non-pending invite
        Guid callerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        Guid inviteId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(callerId);
        IInvitationStorePort invitationStore = Substitute.For<IInvitationStorePort>();
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();
        StubMembershipQueryPort membershipQuery = new((uid, oid, _) => true);

        InvitationDto invite = MakeInvite(inviteId, orgId, status);
        invitationStore.FindByIdAsync(inviteId, Arg.Any<CancellationToken>())
            .Returns(invite);

        RevokeInvitationUseCase useCase = new(
            authUser, invitationStore, membershipStore, membershipQuery, auditLog);

        // Act & Assert — 410 Gone
        InvitationGoneException ex = await Assert.ThrowsAsync<InvitationGoneException>(
            () => useCase.ExecuteAsync(orgId, inviteId));

        Assert.Equal(410, ex.StatusCode);
    }

    [Fact]
    public async Task Execute_OwnerRevokePendingInvite_TransitionsToRevokedAndAppendsAudit()
    {
        // Arrange
        Guid callerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        Guid inviteId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(callerId);
        IInvitationStorePort invitationStore = Substitute.For<IInvitationStorePort>();
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();
        StubMembershipQueryPort membershipQuery = new((uid, oid, _) => true);

        InvitationDto invite = MakeInvite(inviteId, orgId, "pending");
        invitationStore.FindByIdAsync(inviteId, Arg.Any<CancellationToken>())
            .Returns(invite);

        RevokeInvitationUseCase useCase = new(
            authUser, invitationStore, membershipStore, membershipQuery, auditLog);

        // Act
        await useCase.ExecuteAsync(orgId, inviteId);

        // Assert — status set to "revoked" with revokedAt timestamp
        await invitationStore.Received(1).UpdateStatusAsync(
            Arg.Is(inviteId),
            Arg.Is("revoked"),
            Arg.Is<DateTimeOffset?>(x => x == null),  // acceptedAt stays null
            Arg.Is<DateTimeOffset?>(x => x != null),  // revokedAt is set
            Arg.Any<CancellationToken>());

        // Assert — audit appended
        await auditLog.Received(1).AppendAsync(
            orgId,
            callerId,
            "invite.revoked",
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }
}
