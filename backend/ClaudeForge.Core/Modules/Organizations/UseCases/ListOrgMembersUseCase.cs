using ClaudeForge.Core.Modules.Organizations.Ports;
using ClaudeForge.Core.Shared.Authorization;

namespace ClaudeForge.Core.Modules.Organizations.UseCases;

/// <summary>
/// Returns all members of an organization.
/// The caller must be a member; non-members receive 403 (non-disclosure).
/// </summary>
public sealed class ListOrgMembersUseCase
{
    private readonly ICurrentUser _currentUser;
    private readonly IMembershipStorePort _membershipStore;
    private readonly IOrgMembershipQueryPort _membershipQuery;

    public ListOrgMembersUseCase(
        ICurrentUser currentUser,
        IMembershipStorePort membershipStore,
        IOrgMembershipQueryPort membershipQuery)
    {
        _currentUser = currentUser;
        _membershipStore = membershipStore;
        _membershipQuery = membershipQuery;
    }

    public async Task<IReadOnlyList<MemberDto>> ExecuteAsync(
        Guid orgId,
        CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthenticatedException();

        bool isMember = await _membershipQuery.IsMemberAsync(_currentUser.UserId.Value, orgId, ct: ct);
        if (!isMember)
            throw new ForbiddenException();

        return await _membershipStore.ListMembersAsync(orgId, ct);
    }
}
