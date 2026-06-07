using ClaudeForge.Core.Identity;
using ClaudeForge.Core.Modules.Organizations.Ports;
using ClaudeForge.Core.Shared.Authorization;

namespace ClaudeForge.Core.Modules.Organizations.UseCases;

/// <summary>
/// Removes a member from an organization.
/// Requires admin or owner role. Prevents removal of the sole owner.
/// </summary>
public sealed class RemoveMemberUseCase
{
    private readonly ICurrentUser _currentUser;
    private readonly IMembershipStorePort _membershipStore;
    private readonly IOrgMembershipQueryPort _membershipQuery;
    private readonly IOrgAuditLogPort _auditLog;

    public RemoveMemberUseCase(
        ICurrentUser currentUser,
        IMembershipStorePort membershipStore,
        IOrgMembershipQueryPort membershipQuery,
        IOrgAuditLogPort auditLog)
    {
        _currentUser = currentUser;
        _membershipStore = membershipStore;
        _membershipQuery = membershipQuery;
        _auditLog = auditLog;
    }

    public async Task ExecuteAsync(
        Guid orgId,
        Guid targetUserId,
        CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthenticatedException();

        Guid callerId = _currentUser.UserId.Value;

        bool isAdmin = await _membershipQuery.IsMemberAsync(callerId, orgId, "admin", ct);
        if (!isAdmin)
            throw new ForbiddenException();

        MemberDto? targetMember = await _membershipStore.FindMemberAsync(orgId, targetUserId, ct);
        if (targetMember is null)
            throw new MemberNotFoundException();

        // Prevent sole-owner removal
        if (targetMember.Role == OrgRole.Owner)
        {
            int ownerCount = await _membershipStore.CountOwnersAsync(orgId, ct);
            if (ownerCount <= 1)
                throw new SoleOwnerRemovalException();
        }

        await _membershipStore.RemoveMemberAsync(orgId, targetUserId, ct);

        _membershipQuery.InvalidateUser(targetUserId);

        await _auditLog.AppendAsync(
            orgId: orgId,
            actorUserId: callerId,
            action: "member.removed",
            target: $"user:{targetUserId}",
            ct: ct);
    }
}
