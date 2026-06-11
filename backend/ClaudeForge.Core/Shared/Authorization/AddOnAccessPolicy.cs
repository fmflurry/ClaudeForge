namespace ClaudeForge.Core.Shared.Authorization;

/// <summary>
/// Default implementation of <see cref="IAddOnAccessPolicy"/>.
/// Pure logic — no constructor dependencies, no I/O.
/// </summary>
public sealed class AddOnAccessPolicy : IAddOnAccessPolicy
{
    /// <inheritdoc />
    public AccessDecision DecideRead(
        ICurrentUser caller,
        string visibility,
        Guid? ownerOrgId,
        IReadOnlySet<Guid> callerOrgIds)
    {
        // Public plugins are always readable regardless of caller identity.
        if (string.Equals(visibility, "public", StringComparison.OrdinalIgnoreCase))
        {
            return AccessDecision.Allow;
        }

        // Private plugin: anonymous callers receive Unauthenticated (→ HTTP 401).
        if (!caller.IsAuthenticated)
        {
            return AccessDecision.Unauthenticated;
        }

        // Private plugin, authenticated caller: membership check.
        // No ownerOrgId means the caller can never be a member → NotFound.
        if (ownerOrgId is null || !callerOrgIds.Contains(ownerOrgId.Value))
        {
            return AccessDecision.NotFound;
        }

        return AccessDecision.Allow;
    }

    /// <inheritdoc />
    public AccessDecision DecideWrite(
        ICurrentUser caller,
        Guid ownerOrgId,
        IReadOnlySet<Guid> callerOrgIds)
    {
        if (!caller.IsAuthenticated)
        {
            return AccessDecision.Unauthenticated;
        }

        if (!callerOrgIds.Contains(ownerOrgId))
        {
            return AccessDecision.Forbidden;
        }

        return AccessDecision.Allow;
    }
}
