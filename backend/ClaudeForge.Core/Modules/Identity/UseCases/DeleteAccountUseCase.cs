using ClaudeForge.Core.Identity.Ports;
using ClaudeForge.Core.Modules.Organizations.Ports;
using ClaudeForge.Core.Modules.Organizations.UseCases;
using ClaudeForge.Core.Shared.Authorization;

namespace ClaudeForge.Core.Modules.Identity.UseCases;

/// <summary>
/// Deletes the currently authenticated user's account.
///
/// Execution order (enforced by tests):
///   1. Guard: throw <see cref="UnauthenticatedException"/> when unauthenticated.
///   2. Find and delete any organizations where the user is the sole owner with no other members.
///   3. Revoke all active refresh tokens for the user.
///   4. Remove all organization membership rows for the user.
///   5. Soft-delete the user (sets deleted_at).
///   6. Invalidate the membership cache for the user.
///
/// This use-case intentionally has NO telemetry parameter to prevent PII leaking
/// to any telemetry sink during account deletion.
/// </summary>
public sealed class DeleteAccountUseCase
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserDeletionPort _userDeletion;
    private readonly IMembershipStorePort _membershipStore;
    private readonly IRefreshTokenStorePort _refreshTokenStore;
    private readonly IOrgMembershipQueryPort _membershipQuery;
    private readonly IOrgDeletionPort _orgDeletion;

    public DeleteAccountUseCase(
        ICurrentUser currentUser,
        IUserDeletionPort userDeletion,
        IMembershipStorePort membershipStore,
        IRefreshTokenStorePort refreshTokenStore,
        IOrgMembershipQueryPort membershipQuery,
        IOrgDeletionPort orgDeletion)
    {
        _currentUser = currentUser;
        _userDeletion = userDeletion;
        _membershipStore = membershipStore;
        _refreshTokenStore = refreshTokenStore;
        _membershipQuery = membershipQuery;
        _orgDeletion = orgDeletion;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
        {
            throw new UnauthenticatedException();
        }

        Guid userId = _currentUser.UserId.Value;

        // Step 2: Delete orphaned sole-owner orgs (no other members).
        IReadOnlyList<SoleOwnerOrgInfo> soleOwnerOrgs =
            await _orgDeletion.FindSoleOwnerOrgsWithNoOtherMembersAsync(userId, ct);

        foreach (SoleOwnerOrgInfo orgInfo in soleOwnerOrgs)
        {
            await _orgDeletion.DeleteOrganizationAsync(orgInfo.OrgId, ct);
        }

        // Step 3: Revoke all active refresh tokens BEFORE the soft-delete.
        await _userDeletion.RevokeAllRefreshTokensForUserAsync(userId, ct);

        // Step 4: Remove all organization membership rows.
        await _userDeletion.RemoveAllMembershipsForUserAsync(userId, ct);

        // Step 5: Soft-delete the user (sets deleted_at = now()).
        await _userDeletion.SoftDeleteUserAsync(userId, ct);

        // Step 6: Invalidate membership cache so subsequent auth checks see no memberships.
        _membershipQuery.InvalidateUser(userId);
    }
}
