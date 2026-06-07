using ClaudeForge.Core.Modules.Organizations.Ports;
using ClaudeForge.Core.Shared.Authorization;

namespace ClaudeForge.Core.Modules.Organizations.UseCases;

/// <summary>
/// Returns all organizations the current user belongs to.
/// </summary>
public sealed class ListUserOrganizationsUseCase
{
    private readonly ICurrentUser _currentUser;
    private readonly IMembershipStorePort _membershipStore;

    public ListUserOrganizationsUseCase(
        ICurrentUser currentUser,
        IMembershipStorePort membershipStore)
    {
        _currentUser = currentUser;
        _membershipStore = membershipStore;
    }

    public async Task<IReadOnlyList<OrgSummaryDto>> ExecuteAsync(CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthenticatedException();

        return await _membershipStore.ListOrgsForUserAsync(_currentUser.UserId.Value, ct);
    }
}
