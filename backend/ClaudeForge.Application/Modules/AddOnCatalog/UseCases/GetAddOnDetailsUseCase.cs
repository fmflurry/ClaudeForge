using ClaudeForge.Application.Modules.AddOnCatalog.Ports;
using ClaudeForge.Core.Shared.Authorization;

namespace ClaudeForge.Application.Modules.AddOnCatalog.UseCases;

/// <summary>
/// Retrieves full plugin details including version history.
/// Throws <see cref="AddOnNotFoundException"/> when the plugin does not exist
/// OR when the plugin is private and the caller is not a member of the owning organization
/// (non-disclosure: same 404 as truly missing).
/// </summary>
public sealed class GetAddOnDetailsUseCase
{
    private readonly IAddOnRepositoryPort _repository;
    private readonly ICurrentUser? _currentUser;
    private readonly IOrgMembershipQueryPort? _membershipQuery;

    /// <summary>
    /// Full constructor for production use with viewerOrgIds filtering.
    /// </summary>
    public GetAddOnDetailsUseCase(
        IAddOnRepositoryPort repository,
        ICurrentUser currentUser,
        IOrgMembershipQueryPort membershipQuery)
    {
        _repository = repository;
        _currentUser = currentUser;
        _membershipQuery = membershipQuery;
    }

    /// <summary>
    /// Backward-compatible constructor for unit tests and contexts without identity.
    /// Behaves as anonymous caller (public plugins only).
    /// </summary>
    public GetAddOnDetailsUseCase(IAddOnRepositoryPort repository)
    {
        _repository = repository;
        _currentUser = null;
        _membershipQuery = null;
    }

    public async Task<AddOnDetailDto> ExecuteAsync(Guid pluginId, CancellationToken ct = default)
    {
        AddOnDetailDto? detail;

        if (_currentUser is not null && _membershipQuery is not null)
        {
            IReadOnlySet<Guid> viewerOrgIds = await ResolveViewerOrgIdsAsync(ct);
            detail = await _repository.GetAddOnByIdAsync(pluginId, viewerOrgIds, ct);
        }
        else
        {
            // Backward-compat path (anonymous / unit-test mode)
            detail = await _repository.GetAddOnByIdAsync(pluginId, ct);
        }

        if (detail is null)
        {
            throw new AddOnNotFoundException();
        }

        return detail;
    }

    private async Task<IReadOnlySet<Guid>> ResolveViewerOrgIdsAsync(CancellationToken ct)
    {
        if (_currentUser is null || _membershipQuery is null ||
            !_currentUser.IsAuthenticated || _currentUser.UserId is null)
        {
            return new HashSet<Guid>();
        }

        Guid[] orgIds = await _membershipQuery.GetOrgIdsForUserAsync(_currentUser.UserId.Value, ct);
        return new HashSet<Guid>(orgIds);
    }
}
