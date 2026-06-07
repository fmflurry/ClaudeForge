using ClaudeForge.Application.Modules.PluginCatalog.UseCases;
using ClaudeForge.Application.Modules.PluginPublishing.Ports;
using ClaudeForge.Core.Modules.Organizations.Ports;
using ClaudeForge.Core.Shared.Authorization;
using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Application.Modules.PluginPublishing.UseCases;

/// <summary>
/// Changes the visibility of an existing plugin (public ↔ private).
/// Enforces:
///   - unauthenticated → 401
///   - authenticated non-member → 403
///   - plugin not found (or invisible to caller) → 404
///   - private without ownerOrgId → 400
/// Appends a "plugin.visibility_changed" audit log entry on success.
/// </summary>
public sealed class ChangePluginVisibilityUseCase
{
    private readonly IPluginPublishingRepositoryPort _repository;
    private readonly ICurrentUser _currentUser;
    private readonly IOrgMembershipQueryPort _membershipQuery;
    private readonly IPluginAccessPolicy _accessPolicy;
    private readonly IOrgAuditLogPort _auditLog;

    public ChangePluginVisibilityUseCase(
        IPluginPublishingRepositoryPort repository,
        ICurrentUser currentUser,
        IOrgMembershipQueryPort membershipQuery,
        IPluginAccessPolicy accessPolicy,
        IOrgAuditLogPort auditLog)
    {
        _repository = repository;
        _currentUser = currentUser;
        _membershipQuery = membershipQuery;
        _accessPolicy = accessPolicy;
        _auditLog = auditLog;
    }

    /// <summary>
    /// Changes the visibility of the plugin identified by <paramref name="pluginId"/>.
    /// </summary>
    /// <param name="pluginId">The plugin to update.</param>
    /// <param name="newVisibility">"public" or "private".</param>
    /// <param name="newOwnerOrgId">Required when <paramref name="newVisibility"/> is "private".</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task ExecuteAsync(
        Guid pluginId,
        string newVisibility,
        Guid? newOwnerOrgId,
        CancellationToken ct = default)
    {
        // Authentication gate (always required for visibility changes)
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new AuthenticationException("Authentication is required to change plugin visibility.");

        string normalizedVisibility = newVisibility.ToLowerInvariant();

        // Validate: private requires ownerOrgId
        if (normalizedVisibility == "private" && newOwnerOrgId is null)
            throw new PrivatePluginRequiresOrgException();

        // Load current plugin state
        (string Visibility, Guid? OwnerOrgId)? current =
            await _repository.GetPluginVisibilityAsync(pluginId, ct);

        if (current is null)
            throw new PluginNotFoundException();

        // Determine the org to use for write authorization.
        // Use current ownerOrgId if available, otherwise use the requested newOwnerOrgId.
        Guid? authOrgId = current.Value.OwnerOrgId ?? newOwnerOrgId;

        // If we have no org to check against, the caller cannot be a member → 403
        if (authOrgId is null)
        {
            throw new PluginWriteForbiddenException();
        }

        IReadOnlySet<Guid> callerOrgIds = await ResolveCallerOrgIdsAsync(ct);

        AccessDecision decision = _accessPolicy.DecideWrite(
            _currentUser, authOrgId.Value, callerOrgIds);

        if (decision == AccessDecision.Forbidden)
            throw new PluginWriteForbiddenException();

        // When →public: clear ownerOrgId; when →private: set ownerOrgId
        Guid? resolvedOwnerOrgId = normalizedVisibility == "public" ? null : newOwnerOrgId;

        await _repository.UpdateVisibilityAsync(pluginId, normalizedVisibility, resolvedOwnerOrgId, ct);

        // Audit log entry
        await _auditLog.AppendAsync(
            orgId: authOrgId.Value,
            actorUserId: _currentUser.UserId.Value,
            action: "plugin.visibility_changed",
            target: $"plugin:{pluginId}",
            ct: ct);
    }

    private async Task<IReadOnlySet<Guid>> ResolveCallerOrgIdsAsync(CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
        {
            return new HashSet<Guid>();
        }

        Guid[] orgIds = await _membershipQuery.GetOrgIdsForUserAsync(_currentUser.UserId.Value, ct);
        return new HashSet<Guid>(orgIds);
    }
}
