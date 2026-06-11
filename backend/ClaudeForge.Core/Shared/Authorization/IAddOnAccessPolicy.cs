namespace ClaudeForge.Core.Shared.Authorization;

/// <summary>
/// Pure domain service that decides whether a caller may read or write a plugin.
/// No I/O — deterministic, side-effect free.
/// </summary>
public interface IAddOnAccessPolicy
{
    /// <summary>
    /// Decides whether <paramref name="caller"/> may read/download the plugin.
    /// </summary>
    /// <param name="caller">The current user (may be anonymous).</param>
    /// <param name="visibility">"public" or "private".</param>
    /// <param name="ownerOrgId">The owning organization's ID, or <c>null</c> for public ownerless plugins.</param>
    /// <param name="callerOrgIds">Set of org IDs the caller belongs to; empty when anonymous or unaffiliated.</param>
    AccessDecision DecideRead(
        ICurrentUser caller,
        string visibility,
        Guid? ownerOrgId,
        IReadOnlySet<Guid> callerOrgIds);

    /// <summary>
    /// Decides whether <paramref name="caller"/> may write (create/update/delete) the plugin.
    /// </summary>
    /// <param name="caller">The current user.</param>
    /// <param name="ownerOrgId">The owning organization's ID.</param>
    /// <param name="callerOrgIds">Set of org IDs the caller belongs to.</param>
    AccessDecision DecideWrite(
        ICurrentUser caller,
        Guid ownerOrgId,
        IReadOnlySet<Guid> callerOrgIds);
}
