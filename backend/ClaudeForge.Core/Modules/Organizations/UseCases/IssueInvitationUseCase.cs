using ClaudeForge.Core.Identity;
using ClaudeForge.Core.Modules.Organizations.Ports;
using ClaudeForge.Core.Shared.Authorization;

namespace ClaudeForge.Core.Modules.Organizations.UseCases;

/// <summary>
/// Issues an organization invitation to the specified email address.
/// Admin or owner required. Email send is best-effort.
/// </summary>
public sealed class IssueInvitationUseCase
{
    private readonly ICurrentUser _currentUser;
    private readonly IMembershipStorePort _membershipStore;
    private readonly IInvitationStorePort _invitationStore;
    private readonly IInvitationEmailPort _emailPort;
    private readonly IOrgMembershipQueryPort _membershipQuery;
    private readonly IOrgAuditLogPort _auditLog;

    public IssueInvitationUseCase(
        ICurrentUser currentUser,
        IMembershipStorePort membershipStore,
        IInvitationStorePort invitationStore,
        IInvitationEmailPort emailPort,
        IOrgMembershipQueryPort membershipQuery,
        IOrgAuditLogPort auditLog)
    {
        _currentUser = currentUser;
        _membershipStore = membershipStore;
        _invitationStore = invitationStore;
        _emailPort = emailPort;
        _membershipQuery = membershipQuery;
        _auditLog = auditLog;
    }

    public async Task<InvitationDto> ExecuteAsync(
        Guid orgId,
        string email,
        OrgRole role,
        CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthenticatedException();

        Guid callerId = _currentUser.UserId.Value;

        bool isAdmin = await _membershipQuery.IsMemberAsync(callerId, orgId, "admin", ct);
        if (!isAdmin)
            throw new ForbiddenException();

        string emailNormalized = email.ToLowerInvariant();

        MemberDto? existingMember = await _membershipStore.FindMemberByEmailAsync(orgId, emailNormalized, ct);
        if (existingMember is not null)
            throw new AlreadyMemberException();

        InvitationDto? existingInvite = await _invitationStore.FindPendingByOrgAndEmailAsync(orgId, emailNormalized, ct);
        if (existingInvite is not null)
            throw new DuplicateInvitationException();

        string token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

        CreateInvitationRecord record = new(
            Id: Guid.NewGuid(),
            OrgId: orgId,
            EmailNormalized: emailNormalized,
            InvitedBy: callerId,
            Role: role,
            Token: token,
            ExpiresAt: DateTimeOffset.UtcNow.AddDays(7));

        InvitationDto created = await _invitationStore.CreateAsync(record, ct);

        // Best-effort email — failure is swallowed; the invitation record is still valid.
        try
        {
            await _emailPort.SendInvitationAsync(
                toEmail: email,
                orgName: string.Empty,
                inviterName: _currentUser.Email ?? string.Empty,
                invitationToken: token,
                ct: ct);
        }
        catch (Exception)
        {
            // Intentionally swallowed — email delivery failure must not block the operation.
        }

        await _auditLog.AppendAsync(
            orgId: orgId,
            actorUserId: callerId,
            action: "invite.sent",
            target: $"email:{emailNormalized}",
            ct: ct);

        return created;
    }
}
