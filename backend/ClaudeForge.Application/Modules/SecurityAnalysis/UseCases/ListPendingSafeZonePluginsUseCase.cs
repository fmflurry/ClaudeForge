using ClaudeForge.Core.Modules.Organizations.Ports;
using ClaudeForge.Core.Modules.Organizations.UseCases;
using ClaudeForge.Core.Shared.Authorization;
using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Application.Modules.SecurityAnalysis.UseCases;

/// <summary>
/// Lists plugins that passed security analysis but aren't yet approved for the org's safe zone.
/// Only admin/owner can view pending queue.
/// </summary>
public sealed class ListPendingSafeZonePluginsUseCase
{
    private readonly ICurrentUser _currentUser;
    private readonly IMembershipStorePort _membershipStore;
    private readonly ISafeZoneStorePort _safeZoneStore;

    public ListPendingSafeZonePluginsUseCase(
        ICurrentUser currentUser,
        IMembershipStorePort membershipStore,
        ISafeZoneStorePort safeZoneStore)
    {
        _currentUser = currentUser;
        _membershipStore = membershipStore;
        _safeZoneStore = safeZoneStore;
    }

    public async Task<IReadOnlyList<PendingSafeZonePluginDto>> ExecuteAsync(
        Guid orgId,
        CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthenticatedException();

        Guid userId = _currentUser.UserId.Value;

        // Verify membership (any role can view)
        MemberDto? member = await _membershipStore.FindMemberAsync(orgId, userId, ct);
        if (member is null)
            throw new ForbiddenException();

        return await _safeZoneStore.ListPendingPluginsAsync(orgId, ct);
    }
}
