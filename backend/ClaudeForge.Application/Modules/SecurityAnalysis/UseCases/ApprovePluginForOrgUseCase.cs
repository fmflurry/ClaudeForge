using ClaudeForge.Core.Modules.Organizations.Ports;
using ClaudeForge.Core.Modules.Organizations.UseCases;
using ClaudeForge.Core.Shared.Authorization;
using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Application.Modules.SecurityAnalysis.UseCases;

/// <summary>
/// Approves a plugin for an organization's safe zone.
/// Only admin/owner can approve. Plugin must have security_status = "passed".
/// </summary>
public sealed class ApproveAddOnForOrgUseCase
{
    private readonly ICurrentUser _currentUser;
    private readonly IMembershipStorePort _membershipStore;
    private readonly ISafeZoneStorePort _safeZoneStore;

    public ApproveAddOnForOrgUseCase(
        ICurrentUser currentUser,
        IMembershipStorePort membershipStore,
        ISafeZoneStorePort safeZoneStore)
    {
        _currentUser = currentUser;
        _membershipStore = membershipStore;
        _safeZoneStore = safeZoneStore;
    }

    /// <summary>
    /// Approves a plugin for the org's safe zone.
    /// </summary>
    public async Task<SafeZoneEntryDto?> ExecuteAsync(
        Guid orgId,
        Guid pluginId,
        string pluginVersion,
        CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthenticatedException();

        Guid userId = _currentUser.UserId.Value;

        // Verify requester is admin/owner
        MemberDto? member = await _membershipStore.FindMemberAsync(orgId, userId, ct);
        if (member is null)
            throw new ForbiddenException();

        if (member.Role.Value is not ("admin" or "owner"))
            throw new ForbiddenException();

        // Verify plugin is eligible (passed security analysis)
        (bool eligible, string? reason) = await _safeZoneStore.IsAddOnEligibleAsync(pluginId, ct);
        if (!eligible)
            throw new ProblemDetailsException(reason ?? "Plugin is not eligible for safe zone approval.");

        // Approve
        return await _safeZoneStore.ApproveAddOnAsync(orgId, pluginId, pluginVersion, userId, ct);
    }
}
