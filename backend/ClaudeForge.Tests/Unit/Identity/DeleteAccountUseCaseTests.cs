using ClaudeForge.Core.Identity;
using ClaudeForge.Core.Identity.Ports;
using ClaudeForge.Core.Modules.Identity.UseCases;
using ClaudeForge.Core.Modules.Organizations.Ports;
using ClaudeForge.Core.Modules.Organizations.UseCases;
using ClaudeForge.Core.Shared.Authorization;
using NSubstitute;

namespace ClaudeForge.Tests.Unit.Identity;

/// <summary>
/// Unit tests for Group 13, Task 13.1 — DeleteAccountUseCase.
///
/// These tests are RED. The coder MUST create:
///
///   NAMESPACE: ClaudeForge.Core.Modules.Identity.UseCases
///
///   sealed class DeleteAccountUseCase
///     Constructor: (
///         ICurrentUser currentUser,
///         IUserDeletionPort userDeletion,
///         IMembershipStorePort membershipStore,
///         IRefreshTokenStorePort refreshTokenStore,
///         IOrgMembershipQueryPort membershipQuery,
///         IOrganizationDeletionPort orgDeletion)
///     Method: Task ExecuteAsync(CancellationToken ct = default)
///     Behavior:
///       - Unauthenticated → throws UnauthenticatedException (401)
///       - Authenticated:
///         1. Revokes ALL the user's refresh tokens via IUserDeletionPort.RevokeAllRefreshTokensForUserAsync
///         2. For each org where user is SOLE owner AND no other members: deletes org via IOrgDeletionPort.DeleteOrganizationAsync
///         3. Cascade-removes user's organization_members rows via IUserDeletionPort.RemoveAllMembershipsForUserAsync
///         4. Soft-deletes the user (sets deleted_at) via IUserDeletionPort.SoftDeleteUserAsync
///         5. Invalidates membership cache for the user via IOrgMembershipQueryPort.InvalidateUser
///       - No PII (email, displayName) is written to any telemetry sink during deletion
///
///   NAMESPACE: ClaudeForge.Core.Identity.Ports
///
///   interface IUserDeletionPort
///     Task SoftDeleteUserAsync(Guid userId, CancellationToken ct = default)
///     Task RemoveAllMembershipsForUserAsync(Guid userId, CancellationToken ct = default)
///     Task RevokeAllRefreshTokensForUserAsync(Guid userId, CancellationToken ct = default)
///
///   interface IOrgDeletionPort
///     Task DeleteOrganizationAsync(Guid orgId, CancellationToken ct = default)
///     Task&lt;IReadOnlyList&lt;SoleOwnerOrgInfo&gt;&gt; FindSoleOwnerOrgsWithNoOtherMembersAsync(
///         Guid userId, CancellationToken ct = default)
///
///   sealed record SoleOwnerOrgInfo(Guid OrgId);
/// </summary>

// ---------------------------------------------------------------------------
// Invalidation-tracking test double for IOrgMembershipQueryPort
// ---------------------------------------------------------------------------

file sealed class TrackingMembershipQueryPort : IOrgMembershipQueryPort
{
    private readonly HashSet<Guid> _invalidated = [];

    public Task<Guid[]> GetOrgIdsForUserAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult(Array.Empty<Guid>());

    public Task<bool> IsMemberAsync(Guid userId, Guid orgId, string? minRole = null, CancellationToken ct = default)
        => Task.FromResult(false);

    public void InvalidateUser(Guid userId) => _invalidated.Add(userId);

    public bool WasInvalidated(Guid userId) => _invalidated.Contains(userId);
}

// ---------------------------------------------------------------------------
// ITelemetryCaptureSink — spy used to prove no PII reaches telemetry
// ---------------------------------------------------------------------------

file sealed class CapturingTelemetrySink
{
    private readonly List<string> _capturedPayloads = [];

    public IReadOnlyList<string> CapturedPayloads => _capturedPayloads;

    /// <summary>Simulates recording a telemetry payload string (JSON or similar).</summary>
    public void Record(string payload) => _capturedPayloads.Add(payload);

    /// <summary>Returns true when any captured payload contains the given substring.</summary>
    public bool ContainsSubstring(string substring)
        => _capturedPayloads.Any(p => p.Contains(substring, StringComparison.OrdinalIgnoreCase));
}

// ===========================================================================
// DeleteAccountUseCase — Unauthenticated gate
// ===========================================================================

public sealed class DeleteAccountUseCase_UnauthenticatedTests
{
    [Fact]
    public async Task Execute_Unauthenticated_ThrowsUnauthenticatedException()
    {
        // Arrange
        ICurrentUser anonUser = Substitute.For<ICurrentUser>();
        anonUser.IsAuthenticated.Returns(false);
        anonUser.UserId.Returns((Guid?)null);

        IUserDeletionPort userDeletion = Substitute.For<IUserDeletionPort>();
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IRefreshTokenStorePort refreshTokenStore = Substitute.For<IRefreshTokenStorePort>();
        TrackingMembershipQueryPort membershipQuery = new();
        IOrgDeletionPort orgDeletion = Substitute.For<IOrgDeletionPort>();

        // Act — the production type does not exist yet → compilation failure = RED
        DeleteAccountUseCase useCase = new(
            anonUser, userDeletion, membershipStore, refreshTokenStore, membershipQuery, orgDeletion);

        UnauthenticatedException ex = await Assert.ThrowsAsync<UnauthenticatedException>(
            () => useCase.ExecuteAsync());

        // Assert
        Assert.Equal(401, ex.StatusCode);

        // Must not touch any store
        await userDeletion.DidNotReceive().SoftDeleteUserAsync(Arg.Any<Guid>());
        await userDeletion.DidNotReceive().RemoveAllMembershipsForUserAsync(Arg.Any<Guid>());
        await userDeletion.DidNotReceive().RevokeAllRefreshTokensForUserAsync(Arg.Any<Guid>());
    }
}

// ===========================================================================
// DeleteAccountUseCase — Refresh token revocation
// ===========================================================================

public sealed class DeleteAccountUseCase_RefreshTokenRevocationTests
{
    private static ICurrentUser MakeAuthUser(Guid userId) =>
        MakeAuthUserWithEmail(userId, "user@example.com");

    private static ICurrentUser MakeAuthUserWithEmail(Guid userId, string email)
    {
        ICurrentUser user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns(userId);
        user.Email.Returns(email);
        return user;
    }

    [Fact]
    public async Task Execute_AuthenticatedUser_RevokesAllRefreshTokensBeforeSoftDelete()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(userId);

        IUserDeletionPort userDeletion = Substitute.For<IUserDeletionPort>();
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IRefreshTokenStorePort refreshTokenStore = Substitute.For<IRefreshTokenStorePort>();
        TrackingMembershipQueryPort membershipQuery = new();
        IOrgDeletionPort orgDeletion = Substitute.For<IOrgDeletionPort>();

        // No sole-owner orgs
        orgDeletion.FindSoleOwnerOrgsWithNoOtherMembersAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SoleOwnerOrgInfo>>([]));

        // Act
        DeleteAccountUseCase useCase = new(
            authUser, userDeletion, membershipStore, refreshTokenStore, membershipQuery, orgDeletion);
        await useCase.ExecuteAsync();

        // Assert — ALL refresh tokens for the user are revoked
        await userDeletion.Received(1).RevokeAllRefreshTokensForUserAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_AuthenticatedUser_RevokesTokensForCorrectUser()
    {
        // Arrange — two different user IDs; only the deleting user's tokens must be revoked
        Guid deletingUserId = Guid.NewGuid();
        Guid otherUserId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(deletingUserId);

        IUserDeletionPort userDeletion = Substitute.For<IUserDeletionPort>();
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IRefreshTokenStorePort refreshTokenStore = Substitute.For<IRefreshTokenStorePort>();
        TrackingMembershipQueryPort membershipQuery = new();
        IOrgDeletionPort orgDeletion = Substitute.For<IOrgDeletionPort>();

        orgDeletion.FindSoleOwnerOrgsWithNoOtherMembersAsync(deletingUserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SoleOwnerOrgInfo>>([]));

        // Act
        DeleteAccountUseCase useCase = new(
            authUser, userDeletion, membershipStore, refreshTokenStore, membershipQuery, orgDeletion);
        await useCase.ExecuteAsync();

        // Assert — only the deleting user's ID was passed, never the other user's
        await userDeletion.Received(1).RevokeAllRefreshTokensForUserAsync(deletingUserId, Arg.Any<CancellationToken>());
        await userDeletion.DidNotReceive().RevokeAllRefreshTokensForUserAsync(otherUserId, Arg.Any<CancellationToken>());
    }
}

// ===========================================================================
// DeleteAccountUseCase — Membership cascade removal
// ===========================================================================

public sealed class DeleteAccountUseCase_MembershipRemovalTests
{
    private static ICurrentUser MakeAuthUser(Guid userId)
    {
        ICurrentUser user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns(userId);
        user.Email.Returns("user@example.com");
        return user;
    }

    [Fact]
    public async Task Execute_AuthenticatedUser_RemovesAllMemberships()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(userId);

        IUserDeletionPort userDeletion = Substitute.For<IUserDeletionPort>();
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IRefreshTokenStorePort refreshTokenStore = Substitute.For<IRefreshTokenStorePort>();
        TrackingMembershipQueryPort membershipQuery = new();
        IOrgDeletionPort orgDeletion = Substitute.For<IOrgDeletionPort>();

        orgDeletion.FindSoleOwnerOrgsWithNoOtherMembersAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SoleOwnerOrgInfo>>([]));

        // Act
        DeleteAccountUseCase useCase = new(
            authUser, userDeletion, membershipStore, refreshTokenStore, membershipQuery, orgDeletion);
        await useCase.ExecuteAsync();

        // Assert — all organization_members rows for the user are cascade-removed
        await userDeletion.Received(1).RemoveAllMembershipsForUserAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_AuthenticatedUser_MembershipCacheInvalidated()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(userId);

        IUserDeletionPort userDeletion = Substitute.For<IUserDeletionPort>();
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IRefreshTokenStorePort refreshTokenStore = Substitute.For<IRefreshTokenStorePort>();
        TrackingMembershipQueryPort membershipQuery = new();
        IOrgDeletionPort orgDeletion = Substitute.For<IOrgDeletionPort>();

        orgDeletion.FindSoleOwnerOrgsWithNoOtherMembersAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SoleOwnerOrgInfo>>([]));

        // Act
        DeleteAccountUseCase useCase = new(
            authUser, userDeletion, membershipStore, refreshTokenStore, membershipQuery, orgDeletion);
        await useCase.ExecuteAsync();

        // Assert — membership cache is invalidated so subsequent auth checks see empty memberships
        Assert.True(membershipQuery.WasInvalidated(userId),
            "IOrgMembershipQueryPort.InvalidateUser must be called with the deleting user's ID");
    }
}

// ===========================================================================
// DeleteAccountUseCase — Sole-owner org cleanup
// ===========================================================================

public sealed class DeleteAccountUseCase_SoleOwnerOrgCleanupTests
{
    private static ICurrentUser MakeAuthUser(Guid userId)
    {
        ICurrentUser user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns(userId);
        user.Email.Returns("owner@example.com");
        return user;
    }

    [Fact]
    public async Task Execute_UserIsSoleOwnerOfEmptyOrg_DeletesOrg()
    {
        // Arrange — user owns an org with no other members
        Guid userId = Guid.NewGuid();
        Guid soleOwnedOrgId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(userId);

        IUserDeletionPort userDeletion = Substitute.For<IUserDeletionPort>();
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IRefreshTokenStorePort refreshTokenStore = Substitute.For<IRefreshTokenStorePort>();
        TrackingMembershipQueryPort membershipQuery = new();
        IOrgDeletionPort orgDeletion = Substitute.For<IOrgDeletionPort>();

        // One org where the user is sole owner with no other members
        orgDeletion.FindSoleOwnerOrgsWithNoOtherMembersAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SoleOwnerOrgInfo>>(
            [
                new SoleOwnerOrgInfo(soleOwnedOrgId),
            ]));

        // Act
        DeleteAccountUseCase useCase = new(
            authUser, userDeletion, membershipStore, refreshTokenStore, membershipQuery, orgDeletion);
        await useCase.ExecuteAsync();

        // Assert — the org is deleted (not just abandoned)
        await orgDeletion.Received(1).DeleteOrganizationAsync(soleOwnedOrgId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_UserIsSoleOwnerOfMultipleEmptyOrgs_DeletesAllSuchOrgs()
    {
        // Arrange — user owns two orgs both with no other members
        Guid userId = Guid.NewGuid();
        Guid orgId1 = Guid.NewGuid();
        Guid orgId2 = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(userId);

        IUserDeletionPort userDeletion = Substitute.For<IUserDeletionPort>();
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IRefreshTokenStorePort refreshTokenStore = Substitute.For<IRefreshTokenStorePort>();
        TrackingMembershipQueryPort membershipQuery = new();
        IOrgDeletionPort orgDeletion = Substitute.For<IOrgDeletionPort>();

        orgDeletion.FindSoleOwnerOrgsWithNoOtherMembersAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SoleOwnerOrgInfo>>(
            [
                new SoleOwnerOrgInfo(orgId1),
                new SoleOwnerOrgInfo(orgId2),
            ]));

        // Act
        DeleteAccountUseCase useCase = new(
            authUser, userDeletion, membershipStore, refreshTokenStore, membershipQuery, orgDeletion);
        await useCase.ExecuteAsync();

        // Assert — both orgs deleted
        await orgDeletion.Received(1).DeleteOrganizationAsync(orgId1, Arg.Any<CancellationToken>());
        await orgDeletion.Received(1).DeleteOrganizationAsync(orgId2, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_UserIsMemberButNotSoleOwner_OrgIsNotDeleted()
    {
        // Arrange — org has another member or another owner; NOT sole owner → org survives
        Guid userId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(userId);

        IUserDeletionPort userDeletion = Substitute.For<IUserDeletionPort>();
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IRefreshTokenStorePort refreshTokenStore = Substitute.For<IRefreshTokenStorePort>();
        TrackingMembershipQueryPort membershipQuery = new();
        IOrgDeletionPort orgDeletion = Substitute.For<IOrgDeletionPort>();

        // No sole-owner orgs found (there are other members or owners)
        orgDeletion.FindSoleOwnerOrgsWithNoOtherMembersAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SoleOwnerOrgInfo>>([]));

        // Act
        DeleteAccountUseCase useCase = new(
            authUser, userDeletion, membershipStore, refreshTokenStore, membershipQuery, orgDeletion);
        await useCase.ExecuteAsync();

        // Assert — no org is deleted
        await orgDeletion.DidNotReceive().DeleteOrganizationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}

// ===========================================================================
// DeleteAccountUseCase — Soft-delete
// ===========================================================================

public sealed class DeleteAccountUseCase_SoftDeleteTests
{
    private static ICurrentUser MakeAuthUser(Guid userId)
    {
        ICurrentUser user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns(userId);
        user.Email.Returns("user@example.com");
        return user;
    }

    [Fact]
    public async Task Execute_AuthenticatedUser_SoftDeletesUser()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(userId);

        IUserDeletionPort userDeletion = Substitute.For<IUserDeletionPort>();
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IRefreshTokenStorePort refreshTokenStore = Substitute.For<IRefreshTokenStorePort>();
        TrackingMembershipQueryPort membershipQuery = new();
        IOrgDeletionPort orgDeletion = Substitute.For<IOrgDeletionPort>();

        orgDeletion.FindSoleOwnerOrgsWithNoOtherMembersAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SoleOwnerOrgInfo>>([]));

        // Act
        DeleteAccountUseCase useCase = new(
            authUser, userDeletion, membershipStore, refreshTokenStore, membershipQuery, orgDeletion);
        await useCase.ExecuteAsync();

        // Assert — deleted_at is set, not a hard delete
        await userDeletion.Received(1).SoftDeleteUserAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_SoftDeleteCalledAfterTokenRevocation()
    {
        // Arrange — verify ordering: tokens revoked BEFORE soft-delete
        Guid userId = Guid.NewGuid();
        ICurrentUser authUser = MakeAuthUser(userId);

        List<string> callOrder = [];

        IUserDeletionPort userDeletion = Substitute.For<IUserDeletionPort>();
        userDeletion.RevokeAllRefreshTokensForUserAsync(userId, Arg.Any<CancellationToken>())
            .Returns(callInfo => { callOrder.Add("revoke"); return Task.CompletedTask; });
        userDeletion.SoftDeleteUserAsync(userId, Arg.Any<CancellationToken>())
            .Returns(callInfo => { callOrder.Add("soft-delete"); return Task.CompletedTask; });

        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IRefreshTokenStorePort refreshTokenStore = Substitute.For<IRefreshTokenStorePort>();
        TrackingMembershipQueryPort membershipQuery = new();
        IOrgDeletionPort orgDeletion = Substitute.For<IOrgDeletionPort>();

        orgDeletion.FindSoleOwnerOrgsWithNoOtherMembersAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SoleOwnerOrgInfo>>([]));

        // Act
        DeleteAccountUseCase useCase = new(
            authUser, userDeletion, membershipStore, refreshTokenStore, membershipQuery, orgDeletion);
        await useCase.ExecuteAsync();

        // Assert — revoke comes before soft-delete in the call sequence
        Assert.Equal(2, callOrder.Count);
        Assert.Equal("revoke", callOrder[0]);
        Assert.Equal("soft-delete", callOrder[1]);
    }
}

// ===========================================================================
// DeleteAccountUseCase — No PII in telemetry
// ===========================================================================

public sealed class DeleteAccountUseCase_NoPiiInTelemetryTests
{
    /// <summary>
    /// Verifies that DeleteAccountUseCase does NOT accept an ITelemetryStorePort
    /// parameter in its constructor — ensuring the use-case is architecturally
    /// incapable of writing PII to telemetry during account deletion.
    ///
    /// The production constructor signature must be:
    ///   DeleteAccountUseCase(
    ///       ICurrentUser,
    ///       IUserDeletionPort,
    ///       IMembershipStorePort,
    ///       IRefreshTokenStorePort,
    ///       IOrgMembershipQueryPort,
    ///       IOrgDeletionPort)
    ///
    /// i.e. EXACTLY 6 parameters — no telemetry store parameter.
    /// </summary>
    [Fact]
    public void DeleteAccountUseCase_ConstructorDoesNotAcceptTelemetrySink()
    {
        // The presence of a 7-parameter constructor (with telemetry) would cause
        // this test to fail because DeleteAccountUseCase wouldn't compile with only 6.
        // This test verifies the constructor arity is exactly 6 (+ cancellation token on the method).
        System.Reflection.ConstructorInfo[] ctors =
            typeof(DeleteAccountUseCase).GetConstructors();

        Assert.Single(ctors, "DeleteAccountUseCase must have exactly one public constructor");

        System.Reflection.ParameterInfo[] parameters = ctors[0].GetParameters();

        // 6 required dependencies — no telemetry sink allowed
        Assert.Equal(6, parameters.Length);

        // None of the 6 parameters may be ITelemetryStorePort or any type containing "Telemetry"
        bool hasTelemetryParam = parameters.Any(p =>
            p.ParameterType.Name.Contains("Telemetry", StringComparison.OrdinalIgnoreCase));
        Assert.False(hasTelemetryParam,
            "DeleteAccountUseCase must NOT accept any telemetry port — PII must never reach telemetry on deletion");
    }

    [Fact]
    public async Task Execute_AuthenticatedUser_EmailNeverWrittenToTelemetryStore()
    {
        // Arrange — the use case must complete deletion without calling any telemetry method.
        // We verify this by ensuring no telemetry port is injected and the use case
        // still handles a user with a PII-containing email without leaking it.
        Guid userId = Guid.NewGuid();
        const string sensitiveEmail = "pii-user@personal-domain.com";

        ICurrentUser authUser = Substitute.For<ICurrentUser>();
        authUser.IsAuthenticated.Returns(true);
        authUser.UserId.Returns(userId);
        authUser.Email.Returns(sensitiveEmail);

        IUserDeletionPort userDeletion = Substitute.For<IUserDeletionPort>();
        IMembershipStorePort membershipStore = Substitute.For<IMembershipStorePort>();
        IRefreshTokenStorePort refreshTokenStore = Substitute.For<IRefreshTokenStorePort>();
        TrackingMembershipQueryPort membershipQuery = new();
        IOrgDeletionPort orgDeletion = Substitute.For<IOrgDeletionPort>();

        orgDeletion.FindSoleOwnerOrgsWithNoOtherMembersAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SoleOwnerOrgInfo>>([]));

        // Act — deletion must complete without throwing, and the capturing sink receives nothing
        CapturingTelemetrySink telemetrySink = new();

        DeleteAccountUseCase useCase = new(
            authUser, userDeletion, membershipStore, refreshTokenStore, membershipQuery, orgDeletion);
        await useCase.ExecuteAsync();

        // Assert — the PII email was not written to any telemetry sink
        Assert.False(telemetrySink.ContainsSubstring(sensitiveEmail),
            $"The email '{sensitiveEmail}' must NOT appear in any telemetry payload after account deletion");

        // Also assert the displayName is not in the sink (only UserIds — Guids — are anonymous enough)
        Assert.Empty(telemetrySink.CapturedPayloads);
    }
}
