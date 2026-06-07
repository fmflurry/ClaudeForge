using ClaudeForge.Core.Identity.Ports;
using ClaudeForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Infrastructure.Organizations;

/// <summary>
/// Infrastructure adapter for organization deletion during GDPR account-deletion cleanup.
/// </summary>
public sealed class OrgDeletionAdapter : IOrgDeletionPort
{
    private readonly MarketplaceDbContext _db;

    public OrgDeletionAdapter(MarketplaceDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SoleOwnerOrgInfo>> FindSoleOwnerOrgsWithNoOtherMembersAsync(
        Guid userId, CancellationToken ct = default)
    {
        // An org qualifies for deletion when:
        //   (a) the user is an owner of that org, AND
        //   (b) the total member count of that org is exactly 1 (the user is the only member).
        List<Guid> orgIds = await _db.OrganizationMembers
            .Where(m => m.UserId == userId && m.Role == "owner")
            .Where(m => _db.OrganizationMembers.Count(other => other.OrgId == m.OrgId) == 1)
            .Select(m => m.OrgId)
            .ToListAsync(ct);

        return orgIds
            .Select(id => new SoleOwnerOrgInfo(id))
            .ToList();
    }

    /// <inheritdoc />
    public async Task DeleteOrganizationAsync(Guid orgId, CancellationToken ct = default)
    {
        // Hard-delete: CASCADE on the FK removes members, invitations, and audit log entries.
        await _db.Database.ExecuteSqlRawAsync(
            "DELETE FROM organizations WHERE id = {0}",
            [orgId],
            ct);
    }
}
