using ClaudeForge.Core.Identity.Ports;
using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Core.Modules.Identity.UseCases;

/// <summary>
/// Returns the current user's profile and organization memberships.
/// </summary>
public sealed class GetCurrentUserUseCase
{
    private readonly IUserStorePort _userStore;

    public GetCurrentUserUseCase(IUserStorePort userStore)
    {
        _userStore = userStore;
    }

    /// <summary>
    /// Fetches the user by ID and returns their profile plus org memberships.
    /// Throws <see cref="AuthenticationException"/> when the user is not found (→ HTTP 401).
    /// </summary>
    public async Task<CurrentUserResponse> ExecuteAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        UserProfile? profile = await _userStore.FindByIdAsync(userId, ct);

        if (profile is null)
        {
            throw new AuthenticationException($"User {userId} not found.");
        }

        IReadOnlyList<OrgMembershipSummary> memberships = profile.OrgMemberships
            .Select(m => new OrgMembershipSummary(m.OrgId, m.OrgName, m.Role))
            .ToList();

        return new CurrentUserResponse(
            UserId: profile.UserId,
            Email: profile.Email,
            DisplayName: profile.DisplayName,
            OrgMemberships: memberships);
    }
}
