using ClaudeForge.Core.Modules.Organizations.Ports;
using ClaudeForge.Core.Shared.Authorization;

namespace ClaudeForge.Core.Modules.Organizations.UseCases;

/// <summary>
/// Revokes a pending organization invitation. Admin or owner required.
/// </summary>
public sealed class RevokeInvitationUseCase
{
    private readonly ICurrentUser _currentUser;
    private readonly IInvitationStorePort _invitationStore;
    private readonly IMembershipStorePort _membershipStore;
    private readonly IOrgMembershipQueryPort _membershipQuery;
    private readonly IOrgAuditLogPort _auditLog;

    public RevokeInvitationUseCase(
        ICurrentUser currentUser,
        IInvitationStorePort invitationStore,
        IMembershipStorePort membershipStore,
        IOrgMembershipQueryPort membershipQuery,
        IOrgAuditLogPort auditLog)
    {
        _currentUser = currentUser;
        _invitationStore = invitationStore;
        _membershipStore = membershipStore;
        _membershipQuery = membershipQuery;
        _auditLog = auditLog;
    }

    public async Task ExecuteAsync(
        Guid orgId,
        Guid invitationId,
        CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthenticatedException();

        Guid callerId = _currentUser.UserId.Value;

        bool isAdmin = await _membershipQuery.IsMemberAsync(callerId, orgId, "admin", ct);
        if (!isAdmin)
            throw new ForbiddenException();

        InvitationDto? invite = await _invitationStore.FindByIdAsync(invitationId, ct);
        if (invite is null)
            throw new InvitationNotFoundException();

        if (!string.Equals(invite.Status, "pending", StringComparison.Ordinal))
            throw new InvitationGoneException();

        await _invitationStore.UpdateStatusAsync(
            id: invitationId,
            newStatus: "revoked",
            acceptedAt: null,
            revokedAt: DateTimeOffset.UtcNow,
            ct: ct);

        await _auditLog.AppendAsync(
            orgId: orgId,
            actorUserId: callerId,
            action: "invite.revoked",
            target: $"invite:{invitationId}",
            ct: ct);
    }
}
