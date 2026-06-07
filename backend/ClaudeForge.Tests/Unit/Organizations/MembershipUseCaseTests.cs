using ClaudeForge.Core.Identity;
using ClaudeForge.Core.Modules.Organizations.Ports;
using ClaudeForge.Core.Modules.Organizations.UseCases;
using ClaudeForge.Core.Shared.Authorization;
using ClaudeForge.Core.Shared.Exceptions;
using NSubstitute;

namespace ClaudeForge.Tests.Unit.Organizations;

/// <summary>
/// Unit tests for Group 6, Tasks 6.4 and 6.10 — ListUserOrganizationsUseCase,
/// ListOrgMembersUseCase, RemoveMemberUseCase, ChangeMemberRoleUseCase.
///
/// These tests are RED. The coder MUST create:
///
///   NAMESPACE: ClaudeForge.Core.Modules.Organizations.UseCases
///
///   sealed class ListUserOrganizationsUseCase
///     ListUserOrganizationsUseCase(ICurrentUser currentUser, IMembershipStorePort membershipStore)
///     Task&lt;IReadOnlyList&lt;OrgSummaryDto&gt;&gt; ExecuteAsync(CancellationToken ct = default)
///     Behavior:
///       - Unauthenticated → throws UnauthenticatedException (401)
///       - Authenticated → returns all OrgSummaryDto for the user (empty list if none)
///
///   sealed class ListOrgMembersUseCase
///     ListOrgMembersUseCase(ICurrentUser currentUser, IMembershipStorePort membershipStore,
///                           IOrgMembershipQueryPort membershipQuery)
///     Task&lt;IReadOnlyList&lt;MemberDto&gt;&gt; ExecuteAsync(Guid orgId, CancellationToken ct = default)
///     Behavior:
///       - Unauthenticated → throws UnauthenticatedException (401) [but per design: unauth
///         on member-list endpoint returns 403 because unauth is not a member — the endpoint
///         itself requires auth; the use-case enforces auth first then membership]
///       - Caller not a member of orgId → throws ForbiddenException (403)
///       - Caller is a member → returns list of MemberDto with email, name, role
///
///   sealed class RemoveMemberUseCase
///     RemoveMemberUseCase(ICurrentUser currentUser, IMembershipStorePort membershipStore,
///                         IOrgMembershipQueryPort membershipQuery, IOrgAuditLogPort auditLog)
///     Task ExecuteAsync(Guid orgId, Guid targetUserId, CancellationToken ct = default)
///     Behavior:
///       - Unauthenticated → throws UnauthenticatedException (401)
///       - Caller is a plain member (not owner/admin) → throws ForbiddenException (403)
///       - Target membership not found → throws MemberNotFoundException (404)
///       - Sole owner removing themselves → throws SoleOwnerRemovalException (400)
///       - Owner/admin removes another member → removes membership, invalidates cache, appends audit
///       - Cache invalidation: calls IOrgMembershipQueryPort.InvalidateUser (via the adapter's
///         concrete type — the test will use OrgMembershipQueryAdapter or a custom test double
///         that exposes InvalidateUser; design requires the mutation use-cases call it)
///
///   sealed class ChangeMemberRoleUseCase
///     ChangeMemberRoleUseCase(ICurrentUser currentUser, IMembershipStorePort membershipStore,
///                             IOrgMembershipQueryPort membershipQuery, IOrgAuditLogPort auditLog)
///     Task ExecuteAsync(Guid orgId, Guid targetUserId, OrgRole newRole, CancellationToken ct = default)
///     Behavior:
///       - Unauthenticated → throws UnauthenticatedException (401)
///       - Caller is not owner → throws ForbiddenException (403) [owner-only per spec]
///       - Target membership not found → throws MemberNotFoundException (404)
///       - Owner changes member's role → updates role, appends audit entry
///       - Cache invalidation for target user
///
///   sealed class ForbiddenException : ProblemDetailsException
///     StatusCode = 403
///     Message = "You do not have permission to perform this action."
///
///   sealed class MemberNotFoundException : ProblemDetailsException
///     StatusCode = 404
///     Message = "Member not found."
///
///   sealed class SoleOwnerRemovalException : ProblemDetailsException
///     StatusCode = 400
///     Message = "Cannot remove the sole owner of an organization."
///
/// Note: Tests use a test-double for the invalidation hook (IInvalidationHook)
/// since OrgMembershipQueryAdapter.InvalidateUser is on the concrete class.
/// Use-case receives IOrgMembershipQueryPort but at test time we pass in a spy
/// that also implements an InvalidateUser method — or test via the concrete
/// OrgMembershipQueryAdapter. To keep tests framework-independent the production
/// design must expose InvalidateUser through the concrete adapter while use-cases
/// call it via an explicit type-check OR through a dedicated port method. Design
/// decision: add an overloaded interface method
///   void InvalidateUser(Guid userId)
/// directly on IOrgMembershipQueryPort. Tests assert it is called.
/// </summary>

// ---------------------------------------------------------------------------
// Invalidation-capable test double
// ---------------------------------------------------------------------------

file sealed class InvalidatingMembershipQueryPort : IOrgMembershipQueryPort
{
    private readonly HashSet<Guid> _invalidated = new();

    public Task<Guid[]> GetOrgIdsForUserAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult(Array.Empty<Guid>());

    public Task<bool> IsMemberAsync(Guid userId, Guid orgId, string? minRole = null, CancellationToken ct = default)
        => Task.FromResult(false);

    public void InvalidateUser(Guid userId) => _invalidated.Add(userId);

    public bool WasInvalidated(Guid userId) => _invalidated.Contains(userId);
}

// ---------------------------------------------------------------------------
// Configurable membership query spy
// ---------------------------------------------------------------------------

file sealed class ConfigurableMembershipQueryPort : IOrgMembershipQueryPort
{
    private readonly HashSet<Guid> _invalidated = new();
    private readonly Func<Guid, string?, bool> _isMemberFunc;

    public ConfigurableMembershipQueryPort(Func<Guid, string?, bool> isMemberFunc)
    {
        _isMemberFunc = isMemberFunc;
    }

    public Task<Guid[]> GetOrgIdsForUserAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult(Array.Empty<Guid>());

    public Task<bool> IsMemberAsync(Guid userId, Guid orgId, string? minRole = null, CancellationToken ct = default)
        => Task.FromResult(_isMemberFunc(userId, minRole));

    public void InvalidateUser(Guid userId) => _invalidated.Add(userId);

    public bool WasInvalidated(Guid userId) => _invalidated.Contains(userId);
}

// ===========================================================================
// ListUserOrganizationsUseCase tests
// ===========================================================================

public sealed class ListUserOrganizationsUseCaseTests
{
    private static ICurrentUser MakeAuthUser(Guid userId)
    {
        ICurrentUser user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns(userId);
        user.Email.Returns("user@example.com");
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

    [Fact]
    public async Task Execute_Unauthenticated_ThrowsUnauthenticatedException()
    {
        // Arrange
        ICurrentUser anonUser = MakeAnonUser();
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();

        ListUserOrganizationsUseCase useCase = new(anonUser, membershipStore);

        // Act & Assert
        UnauthenticatedException ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => useCase.ExecuteAsync());

        Assert.Equal(401, ex.StatusCode);

        // Must not touch the store
        await membershipStore.DidNotReceive().ListOrgsForUserAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task Execute_AuthenticatedUserWithNoOrgs_ReturnsEmptyList()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(userId);
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        membershipStore.ListOrgsForUserAsync(userId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<OrgSummaryDto>)[]);

        ListUserOrganizationsUseCase useCase = new(authUser, membershipStore);

        // Act
        IReadOnlyList<OrgSummaryDto> result = await useCase.ExecuteAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task Execute_AuthenticatedUserWithMultipleOrgs_ReturnsAllOrgsWithRole()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(userId);
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();

        IReadOnlyList<OrgSummaryDto> orgs =
        [
            new OrgSummaryDto(Guid.NewGuid(), "Org Alpha", "org-alpha", OrgRole.Owner),
            new OrgSummaryDto(Guid.NewGuid(), "Org Beta", "org-beta", OrgRole.Member),
        ];

        membershipStore.ListOrgsForUserAsync(userId, Arg.Any<CancellationToken>())
            .Returns(orgs);

        ListUserOrganizationsUseCase useCase = new(authUser, membershipStore);

        // Act
        IReadOnlyList<OrgSummaryDto> result = await useCase.ExecuteAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, o => o.UserRole == OrgRole.Owner);
        Assert.Contains(result, o => o.UserRole == OrgRole.Member);
    }
}

// ===========================================================================
// ListOrgMembersUseCase tests
// ===========================================================================

public sealed class ListOrgMembersUseCaseTests
{
    private static ICurrentUser MakeAuthUser(Guid userId)
    {
        ICurrentUser user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns(userId);
        user.Email.Returns("user@example.com");
        return user;
    }

    private static ICurrentUser MakeAnonUser()
    {
        ICurrentUser user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(false);
        user.UserId.Returns((Guid?)null);
        return user;
    }

    [Fact]
    public async Task Execute_Unauthenticated_ThrowsUnauthenticatedException()
    {
        // Arrange — design: endpoint requires auth; use-case validates auth first
        ICurrentUser anonUser = MakeAnonUser();
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IOrgMembershipQueryPort membershipQuery = Substitute.For<IOrgMembershipQueryPort>();

        ListOrgMembersUseCase useCase = new(anonUser, membershipStore, membershipQuery);

        // Act & Assert
        UnauthenticatedException ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => useCase.ExecuteAsync(Guid.NewGuid()));

        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task Execute_CallerNotMember_ThrowsForbiddenException()
    {
        // Arrange — non-member listing members → 403 (non-disclosure per design.md)
        Guid userId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(userId);
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();

        // The caller is NOT a member of this org
        ConfigurableMembershipQueryPort membershipQuery = new((uid, _) => false);

        ListOrgMembersUseCase useCase = new(authUser, membershipStore, membershipQuery);

        // Act & Assert — 403, not 404 per design.md "non-disclosure for org view"
        ForbiddenException ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => useCase.ExecuteAsync(orgId));

        Assert.Equal(403, ex.StatusCode);

        await membershipStore.DidNotReceive().ListMembersAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task Execute_CallerIsMember_ReturnsMembersWithEmailAndRole()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(userId);
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();

        // Caller is a member
        ConfigurableMembershipQueryPort membershipQuery = new((uid, _) => true);

        IReadOnlyList<MemberDto> members =
        [
            new MemberDto(userId, "user@example.com", "User", OrgRole.Member, DateTimeOffset.UtcNow),
            new MemberDto(Guid.NewGuid(), "admin@example.com", "Admin", OrgRole.Admin, DateTimeOffset.UtcNow),
        ];

        membershipStore.ListMembersAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(members);

        ListOrgMembersUseCase useCase = new(authUser, membershipStore, membershipQuery);

        // Act
        IReadOnlyList<MemberDto> result = await useCase.ExecuteAsync(orgId);

        // Assert — members returned with email, name, role
        Assert.Equal(2, result.Count);
        Assert.All(result, m =>
        {
            Assert.NotEmpty(m.Email);
            Assert.NotEmpty(m.DisplayName);
            Assert.NotNull(m.Role);
        });
    }
}

// ===========================================================================
// RemoveMemberUseCase tests
// ===========================================================================

public sealed class RemoveMemberUseCaseTests
{
    private static ICurrentUser MakeAuthUser(Guid userId)
    {
        ICurrentUser user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns(userId);
        user.Email.Returns("user@example.com");
        return user;
    }

    private static ICurrentUser MakeAnonUser()
    {
        ICurrentUser user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(false);
        user.UserId.Returns((Guid?)null);
        return user;
    }

    [Fact]
    public async Task Execute_Unauthenticated_ThrowsUnauthenticatedException()
    {
        // Arrange
        ICurrentUser anonUser = MakeAnonUser();
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        ConfigurableMembershipQueryPort membershipQuery = new((_, _) => false);
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();

        RemoveMemberUseCase useCase = new(anonUser, membershipStore, membershipQuery, auditLog);

        // Act & Assert
        UnauthenticatedException ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => useCase.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(401, ex.StatusCode);
        await membershipStore.DidNotReceive().RemoveMemberAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Fact]
    public async Task Execute_CallerIsPlainMember_ThrowsForbiddenException()
    {
        // Arrange — member (not owner/admin) trying to remove another member
        Guid callerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        Guid targetId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(callerId);
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();

        // Caller is a "member" — does not satisfy minRole="admin"
        ConfigurableMembershipQueryPort membershipQuery =
            new((uid, minRole) => minRole == null || minRole == "member");

        RemoveMemberUseCase useCase = new(authUser, membershipStore, membershipQuery, auditLog);

        // Act & Assert
        ForbiddenException ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => useCase.ExecuteAsync(orgId, targetId));

        Assert.Equal(403, ex.StatusCode);
        await membershipStore.DidNotReceive().RemoveMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Fact]
    public async Task Execute_TargetMemberNotFound_ThrowsMemberNotFoundException()
    {
        // Arrange — caller is admin, but target user is not a member
        Guid callerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        Guid targetId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(callerId);
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();

        // Caller satisfies minRole=admin
        ConfigurableMembershipQueryPort membershipQuery = new((uid, _) => true);

        membershipStore.FindMemberAsync(orgId, targetId, Arg.Any<CancellationToken>())
            .Returns((MemberDto?)null);

        RemoveMemberUseCase useCase = new(authUser, membershipStore, membershipQuery, auditLog);

        // Act & Assert
        MemberNotFoundException ex = await Assert.ThrowsAsync<MemberNotFoundException>(
            () => useCase.ExecuteAsync(orgId, targetId));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task Execute_SoleOwnerRemovingThemselves_ThrowsSoleOwnerRemovalException()
    {
        // Arrange — caller is the only owner; trying to remove themselves
        Guid ownerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(ownerId);
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();

        // Caller is owner
        ConfigurableMembershipQueryPort membershipQuery = new((uid, _) => true);

        MemberDto ownerMemberDto = new(ownerId, "owner@example.com", "Owner", OrgRole.Owner, DateTimeOffset.UtcNow);
        membershipStore.FindMemberAsync(orgId, ownerId, Arg.Any<CancellationToken>())
            .Returns(ownerMemberDto);

        // Only one owner
        membershipStore.CountOwnersAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(1);

        RemoveMemberUseCase useCase = new(authUser, membershipStore, membershipQuery, auditLog);

        // Act & Assert
        SoleOwnerRemovalException ex = await Assert.ThrowsAsync<SoleOwnerRemovalException>(
            () => useCase.ExecuteAsync(orgId, ownerId));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("Cannot remove the sole owner of an organization.", ex.Message);

        await membershipStore.DidNotReceive().RemoveMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Fact]
    public async Task Execute_OwnerRemovesMember_RemovesMembershipAndInvalidatesCache()
    {
        // Arrange
        Guid callerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        Guid targetId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(callerId);
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();

        InvalidatingMembershipQueryPort membershipQuery = new();
        // Caller is admin/owner (true for any minRole check)
        // We override via the test double — pre-primed as "satisfies any minRole"

        // Actually need configurable that always returns true:
        ConfigurableMembershipQueryPort configurableQuery = new((uid, _) => true);

        MemberDto targetMember = new(targetId, "target@example.com", "Target", OrgRole.Member, DateTimeOffset.UtcNow);
        membershipStore.FindMemberAsync(orgId, targetId, Arg.Any<CancellationToken>())
            .Returns(targetMember);

        RemoveMemberUseCase useCase = new(authUser, membershipStore, configurableQuery, auditLog);

        // Act
        await useCase.ExecuteAsync(orgId, targetId);

        // Assert — membership removed
        await membershipStore.Received(1).RemoveMemberAsync(orgId, targetId, Arg.Any<CancellationToken>());

        // Assert — cache invalidated for the target user
        Assert.True(configurableQuery.WasInvalidated(targetId),
            "InvalidateUser must be called for the removed user's ID");
    }

    [Fact]
    public async Task Execute_OwnerRemovesMember_AppendsAuditEntry()
    {
        // Arrange
        Guid callerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        Guid targetId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(callerId);
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();
        ConfigurableMembershipQueryPort membershipQuery = new((uid, _) => true);

        MemberDto targetMember = new(targetId, "target@example.com", "Target", OrgRole.Member, DateTimeOffset.UtcNow);
        membershipStore.FindMemberAsync(orgId, targetId, Arg.Any<CancellationToken>())
            .Returns(targetMember);

        RemoveMemberUseCase useCase = new(authUser, membershipStore, membershipQuery, auditLog);

        // Act
        await useCase.ExecuteAsync(orgId, targetId);

        // Assert — audit appended
        await auditLog.Received(1).AppendAsync(
            orgId,
            callerId,
            "member.removed",
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }
}

// ===========================================================================
// ChangeMemberRoleUseCase tests
// ===========================================================================

public sealed class ChangeMemberRoleUseCaseTests
{
    private static ICurrentUser MakeAuthUser(Guid userId)
    {
        ICurrentUser user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns(userId);
        user.Email.Returns("user@example.com");
        return user;
    }

    private static ICurrentUser MakeAnonUser()
    {
        ICurrentUser user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(false);
        user.UserId.Returns((Guid?)null);
        return user;
    }

    [Fact]
    public async Task Execute_Unauthenticated_ThrowsUnauthenticatedException()
    {
        // Arrange
        ICurrentUser anonUser = MakeAnonUser();
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        ConfigurableMembershipQueryPort membershipQuery = new((_, _) => false);
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();

        ChangeMemberRoleUseCase useCase = new(anonUser, membershipStore, membershipQuery, auditLog);

        // Act & Assert
        UnauthenticatedException ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => useCase.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), OrgRole.Admin));

        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task Execute_CallerIsAdmin_NotOwner_ThrowsForbiddenException()
    {
        // Arrange — role change is owner-only per spec (6.10 "owner promotes member→admin → 200 (owner-only)")
        Guid callerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        Guid targetId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(callerId);
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();

        // Caller satisfies "admin" but NOT "owner"
        ConfigurableMembershipQueryPort membershipQuery =
            new((uid, minRole) => minRole != "owner");

        ChangeMemberRoleUseCase useCase = new(authUser, membershipStore, membershipQuery, auditLog);

        // Act & Assert
        ForbiddenException ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => useCase.ExecuteAsync(orgId, targetId, OrgRole.Admin));

        Assert.Equal(403, ex.StatusCode);
        await membershipStore.DidNotReceive().UpdateMemberRoleAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<OrgRole>());
    }

    [Fact]
    public async Task Execute_TargetNotMember_ThrowsMemberNotFoundException()
    {
        // Arrange — caller is owner, target does not exist
        Guid callerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        Guid targetId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(callerId);
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();

        // Caller is owner
        ConfigurableMembershipQueryPort membershipQuery = new((uid, _) => true);

        membershipStore.FindMemberAsync(orgId, targetId, Arg.Any<CancellationToken>())
            .Returns((MemberDto?)null);

        ChangeMemberRoleUseCase useCase = new(authUser, membershipStore, membershipQuery, auditLog);

        // Act & Assert
        MemberNotFoundException ex = await Assert.ThrowsAsync<MemberNotFoundException>(
            () => useCase.ExecuteAsync(orgId, targetId, OrgRole.Admin));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task Execute_OwnerPromotesMemberToAdmin_UpdatesRoleAndAppendsAudit()
    {
        // Arrange
        Guid callerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        Guid targetId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(callerId);
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IOrgAuditLogPort auditLog = Substitute.For<IOrgAuditLogPort>();
        ConfigurableMembershipQueryPort membershipQuery = new((uid, _) => true);

        MemberDto targetMember = new(targetId, "member@example.com", "Member", OrgRole.Member, DateTimeOffset.UtcNow);
        membershipStore.FindMemberAsync(orgId, targetId, Arg.Any<CancellationToken>())
            .Returns(targetMember);

        ChangeMemberRoleUseCase useCase = new(authUser, membershipStore, membershipQuery, auditLog);

        // Act
        await useCase.ExecuteAsync(orgId, targetId, OrgRole.Admin);

        // Assert — role updated using value object
        await membershipStore.Received(1).UpdateMemberRoleAsync(
            orgId, targetId, OrgRole.Admin, Arg.Any<CancellationToken>());

        // Assert — audit appended
        await auditLog.Received(1).AppendAsync(
            orgId,
            callerId,
            "member.role_changed",
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        // Assert — cache invalidated for target
        Assert.True(membershipQuery.WasInvalidated(targetId),
            "InvalidateUser must be called for the user whose role changed");
    }
}
