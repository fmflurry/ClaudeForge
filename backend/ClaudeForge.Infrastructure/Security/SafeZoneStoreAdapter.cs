using ClaudeForge.Core.Modules.Organizations.Ports;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Infrastructure.Security;

/// <summary>
/// EF Core adapter for <see cref="ISafeZoneStorePort"/>.
/// Backed by <see cref="MarketplaceDbContext"/>.
/// </summary>
public sealed class SafeZoneStoreAdapter : ISafeZoneStorePort
{
    private readonly MarketplaceDbContext _ctx;

    public SafeZoneStoreAdapter(MarketplaceDbContext ctx)
    {
        _ctx = ctx;
    }

    public async Task<(bool Eligible, string? Reason)> IsAddOnEligibleAsync(
        Guid pluginId,
        CancellationToken ct = default)
    {
        var plugin = await _ctx.Plugins
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == pluginId, ct);

        if (plugin is null)
            return (false, "Plugin not found.");

        if (plugin.SecurityStatus != "passed")
            return (false, $"Plugin security status is '{plugin.SecurityStatus}'. Only plugins with status 'passed' can be approved.");

        return (true, null);
    }

    public async Task<SafeZoneEntryDto?> ApproveAddOnAsync(
        Guid orgId,
        Guid pluginId,
        string pluginVersion,
        Guid approvedBy,
        CancellationToken ct = default)
    {
        // Check if entry already exists (idempotent — update if active)
        SafeZoneAddOnEntity? existing = await _ctx.SafeZonePlugins
            .FirstOrDefaultAsync(
                sz => sz.OrgId == orgId && sz.PluginId == pluginId && sz.PluginVersion == pluginVersion,
                ct);

        if (existing is not null)
        {
            if (existing.IsActive)
            {
                return ToEntryDto(existing);
            }

            // Reactivate
            existing.IsActive = true;
            existing.ApprovedBy = approvedBy;
            existing.ApprovedAt = DateTimeOffset.UtcNow;
            await _ctx.SaveChangesAsync(ct);
            return ToEntryDto(existing);
        }

        SafeZoneAddOnEntity entity = new()
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            PluginId = pluginId,
            PluginVersion = pluginVersion,
            ApprovedBy = approvedBy,
            ApprovedAt = DateTimeOffset.UtcNow,
            IsActive = true,
        };

        _ctx.SafeZonePlugins.Add(entity);
        await _ctx.SaveChangesAsync(ct);

        return ToEntryDto(entity);
    }

    public async Task<IReadOnlyList<SafeZonePluginDetailDto>> ListSafeZonePluginsAsync(
        Guid orgId,
        CancellationToken ct = default)
    {
        List<SafeZonePluginDetailDto> results = await _ctx.SafeZonePlugins
            .AsNoTracking()
            .Where(sz => sz.OrgId == orgId && sz.IsActive)
            .Join(
                _ctx.Plugins.AsNoTracking(),
                sz => sz.PluginId,
                p => p.Id,
                (sz, p) => new SafeZonePluginDetailDto(
                    Id: sz.Id,
                    PluginId: sz.PluginId,
                    Name: p.Name,
                    Slug: p.Slug,
                    PluginVersion: sz.PluginVersion,
                    SecurityScore: p.SecurityScore,
                    SecurityStatus: p.SecurityStatus,
                    ApprovedBy: sz.ApprovedBy,
                    ApprovedAt: sz.ApprovedAt,
                    Label: "APPROVED"))
            .ToListAsync(ct);

        return results;
    }

    public async Task<IReadOnlyList<PendingSafeZonePluginDto>> ListPendingAddOnsAsync(
        Guid orgId,
        CancellationToken ct = default)
    {
        // Get IDs of already-approved plugins for this org
        HashSet<Guid> approvedIds = await _ctx.SafeZonePlugins
            .AsNoTracking()
            .Where(sz => sz.OrgId == orgId && sz.IsActive)
            .Select(sz => sz.PluginId)
            .Distinct()
            .ToHashSetAsync(ct);

        // Plugins that passed security analysis but aren't yet approved
        List<PendingSafeZonePluginDto> results = await _ctx.Plugins
            .AsNoTracking()
            .Where(p => p.SecurityStatus == "passed" && !approvedIds.Contains(p.Id))
            .Select(p => new PendingSafeZonePluginDto(
                PluginId: p.Id,
                Name: p.Name,
                Slug: p.Slug,
                SecurityScore: p.SecurityScore,
                SecurityStatus: p.SecurityStatus))
            .ToListAsync(ct);

        return results;
    }

    public async Task<SafeZoneEntryDto?> FindEntryAsync(
        Guid orgId,
        Guid pluginId,
        string? pluginVersion = null,
        CancellationToken ct = default)
    {
        IQueryable<SafeZoneAddOnEntity> query = _ctx.SafeZonePlugins
            .Where(sz => sz.OrgId == orgId && sz.PluginId == pluginId && sz.IsActive);

        if (pluginVersion is not null)
        {
            query = query.Where(sz => sz.PluginVersion == pluginVersion);
        }

        SafeZoneAddOnEntity? entity = await query.FirstOrDefaultAsync(ct);
        return entity is null ? null : ToEntryDto(entity);
    }

    // ── Global safe zone (3.3.4) ───────────────────────────────────────────

    public async Task<SafeZoneEntryDto?> ApproveAddOnGlobalAsync(
        Guid pluginId,
        string pluginVersion,
        Guid approvedBy,
        CancellationToken ct = default)
    {
        // Use Guid.Empty for global org
        return await ApproveAddOnAsync(Guid.Empty, pluginId, pluginVersion, approvedBy, ct);
    }

    public async Task<IReadOnlyList<SafeZonePluginDetailDto>> ListGlobalSafeZonePluginsAsync(
        CancellationToken ct = default)
    {
        return await ListSafeZonePluginsAsync(Guid.Empty, ct);
    }

    // ── Org-level blocks for global plugins (3.3.5) ────────────────────────

    public async Task BlockGlobalAddOnAsync(
        Guid orgId,
        Guid pluginId,
        Guid blockedBy,
        CancellationToken ct = default)
    {
        bool alreadyBlocked = await _ctx.OrgPluginBlocks
            .AnyAsync(b => b.OrgId == orgId && b.PluginId == pluginId, ct);

        if (!alreadyBlocked)
        {
            _ctx.OrgPluginBlocks.Add(new OrgAddOnBlockEntity
            {
                Id = Guid.NewGuid(),
                OrgId = orgId,
                PluginId = pluginId,
                BlockedBy = blockedBy,
                BlockedAt = DateTimeOffset.UtcNow,
            });
            await _ctx.SaveChangesAsync(ct);
        }
    }

    public async Task UnblockGlobalAddOnAsync(
        Guid orgId,
        Guid pluginId,
        CancellationToken ct = default)
    {
        OrgAddOnBlockEntity? block = await _ctx.OrgPluginBlocks
            .FirstOrDefaultAsync(b => b.OrgId == orgId && b.PluginId == pluginId, ct);

        if (block is not null)
        {
            _ctx.OrgPluginBlocks.Remove(block);
            await _ctx.SaveChangesAsync(ct);
        }
    }

    public async Task<bool> IsGloballyBlockedAsync(
        Guid orgId,
        Guid pluginId,
        CancellationToken ct = default)
    {
        return orgId != Guid.Empty &&
               await _ctx.OrgPluginBlocks
                   .AnyAsync(b => b.OrgId == orgId && b.PluginId == pluginId, ct);
    }

    public async Task<IReadOnlyList<Guid>> ListBlockedGlobalAddOnsAsync(
        Guid orgId,
        CancellationToken ct = default)
    {
        return await _ctx.OrgPluginBlocks
            .AsNoTracking()
            .Where(b => b.OrgId == orgId)
            .Select(b => b.PluginId)
            .ToListAsync(ct);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static SafeZoneEntryDto ToEntryDto(SafeZoneAddOnEntity entity) => new(
        Id: entity.Id,
        OrgId: entity.OrgId,
        PluginId: entity.PluginId,
        PluginVersion: entity.PluginVersion,
        ApprovedBy: entity.ApprovedBy,
        ApprovedAt: entity.ApprovedAt,
        IsActive: entity.IsActive);
}
