namespace ClaudeForge.Core.Identity.Ports;

/// <summary>
/// Information about an organization where a user is the sole owner and no other members exist.
/// </summary>
public sealed record SoleOwnerOrgInfo(Guid OrgId);

/// <summary>
/// Port for performing GDPR-compliant user account deletion operations.
/// Each method targets a specific aspect of the deletion workflow.
/// </summary>
public interface IUserDeletionPort
{
    /// <summary>
    /// Soft-deletes the user by setting <c>deleted_at = now()</c> on the users row.
    /// Does not hard-delete the row so audit records remain intact.
    /// </summary>
    Task SoftDeleteUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Removes all <c>organization_members</c> rows where <c>user_id = userId</c>.
    /// </summary>
    Task RemoveAllMembershipsForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Revokes all active refresh tokens for the user by setting
    /// <c>revoked_at = now()</c> where <c>user_id = userId AND revoked_at IS NULL</c>.
    /// </summary>
    Task RevokeAllRefreshTokensForUserAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>
/// Port for querying and deleting organizations during account-deletion cleanup.
/// </summary>
public interface IOrgDeletionPort
{
    /// <summary>
    /// Returns organizations where the given user is the sole owner AND the only member
    /// (no other members of any role exist). These organizations must be deleted when
    /// the user deletes their account, as they would otherwise be orphaned.
    /// </summary>
    Task<IReadOnlyList<SoleOwnerOrgInfo>> FindSoleOwnerOrgsWithNoOtherMembersAsync(
        Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Hard-deletes the organization. The database CASCADE constraint removes
    /// all child rows (members, invitations, audit log entries for that org).
    /// </summary>
    Task DeleteOrganizationAsync(Guid orgId, CancellationToken ct = default);
}
