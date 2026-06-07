using ClaudeForge.Core.Modules.Organizations.Ports;
using ClaudeForge.Core.Shared.Authorization;

namespace ClaudeForge.Core.Modules.Organizations.UseCases;

/// <summary>
/// Accepts a pending organization invitation for the current user.
/// </summary>
public sealed class AcceptInvitationUseCase
{
    private readonly ICurrentUser _currentUser;
    private readonly IInvitationStorePort _invitationStore;
    private readonly IMembershipStorePort _membershipStore;
    private readonly IOrgMembershipQueryPort _membershipQuery;
    private readonly IOrgAuditLogPort _auditLog;

    public AcceptInvitationUseCase(
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

        Guid userId = _currentUser.UserId.Value;
        string? userEmail = _currentUser.Email;

        InvitationDto? invite = await _invitationStore.FindByIdAsync(invitationId, ct);

        // 404 if not found, if org doesn't match, or if email doesn't match current user
        if (invite is null
            || invite.OrgId != orgId
            || string.IsNullOrEmpty(userEmail)
            || !string.Equals(invite.EmailNormalized, userEmail.ToLowerInvariant(), StringComparison.Ordinal))
        {
            throw new InvitationNotFoundException();
        }

        if (!string.Equals(invite.Status, "pending", StringComparison.Ordinal))
            throw new InvitationGoneException();

        await _invitationStore.UpdateStatusAsync(
            id: invitationId,
            newStatus: "accepted",
            acceptedAt: DateTimeOffset.UtcNow,
            revokedAt: null,
            ct: ct);

        await _membershipStore.AddMemberAsync(orgId, userId, invite.Role, ct);

        _membershipQuery.InvalidateUser(userId);

        await _auditLog.AppendAsync(
            orgId: orgId,
            actorUserId: userId,
            action: "invite.accepted",
            target: $"invite:{invitationId}",
            ct: ct);
    }
}
