using ClaudeForge.Core.Modules.Organizations.Ports;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;

namespace ClaudeForge.Infrastructure.Organizations;

/// <summary>
/// EF Core adapter for <see cref="IOrgAuditLogPort"/>. Append-only.
/// </summary>
public sealed class OrgAuditLogAdapter : IOrgAuditLogPort
{
    private readonly MarketplaceDbContext _ctx;

    public OrgAuditLogAdapter(MarketplaceDbContext ctx)
    {
        _ctx = ctx;
    }

    public async Task AppendAsync(
        Guid orgId,
        Guid actorUserId,
        string action,
        string target,
        CancellationToken ct = default)
    {
        OrgAuditEntryEntity entry = new()
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            ActorUserId = actorUserId,
            Action = action,
            Target = target,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _ctx.OrgAuditLog.Add(entry);
        await _ctx.SaveChangesAsync(ct);
    }
}
