using ClaudeForge.Core.Identity;
using ClaudeForge.Core.Modules.Organizations.Ports;
using ClaudeForge.Core.Shared.Authorization;

namespace ClaudeForge.Core.Modules.Organizations.UseCases;

/// <summary>
/// Changes a member's role within an organization. Owner-only action.
/// </summary>
public sealed class ChangeMemberRoleUseCase
{
    private readonly ICurrentUser _currentUser;
    private readonly IMembershipStorePort _membershipStore;
    private readonly IOrgMembershipQueryPort _membershipQuery;
    private readonly IOrgAuditLogPort _auditLog;

    public ChangeMemberRoleUseCase(
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
        OrgRole newRole,
        CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthenticatedException();

        Guid callerId = _currentUser.UserId.Value;

        bool isOwner = await _membershipQuery.IsMemberAsync(callerId, orgId, "owner", ct);
        if (!isOwner)
            throw new ForbiddenException();

        MemberDto? targetMember = await _membershipStore.FindMemberAsync(orgId, targetUserId, ct);
        if (targetMember is null)
            throw new MemberNotFoundException();

        await _membershipStore.UpdateMemberRoleAsync(orgId, targetUserId, newRole, ct);

        _membershipQuery.InvalidateUser(targetUserId);

        await _auditLog.AppendAsync(
            orgId: orgId,
            actorUserId: callerId,
            action: "member.role_changed",
            target: $"user:{targetUserId}:role:{newRole.Value}",
            ct: ct);
    }
}
