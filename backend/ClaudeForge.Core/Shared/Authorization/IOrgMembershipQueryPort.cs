namespace ClaudeForge.Core.Shared.Authorization;

/// <summary>
/// Port for querying organization membership. Implementations reside in Infrastructure.
/// All method signatures use only primitive/BCL types (Guid, bool, string, CancellationToken)
/// so that the shared-kernel remains free of any Organizations domain dependency.
/// </summary>
public interface IOrgMembershipQueryPort
{
    /// <summary>
    /// Returns the set of organization IDs the given user belongs to.
    /// Returns an empty array when the user has no memberships or does not exist.
    /// </summary>
    Task<Guid[]> GetOrgIdsForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Returns <c>true</c> when the user is a member of the organization with at
    /// least the specified minimum role.
    /// Role hierarchy (lowest to highest): member &lt; admin &lt; owner.
    /// When <paramref name="minRole"/> is <c>null</c>, any membership qualifies.
    /// </summary>
    Task<bool> IsMemberAsync(Guid userId, Guid orgId, string? minRole = null, CancellationToken ct = default);
}
