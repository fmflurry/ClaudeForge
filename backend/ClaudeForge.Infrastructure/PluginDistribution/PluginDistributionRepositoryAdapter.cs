using ClaudeForge.Application.Modules.PluginDistribution.Ports;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Infrastructure.PluginDistribution;

/// <summary>
/// EF Core adapter implementing <see cref="IPluginDistributionRepositoryPort"/>.
/// All counter increments use DB-side arithmetic (no read-modify-write) to be race-safe.
/// </summary>
public sealed class PluginDistributionRepositoryAdapter : IPluginDistributionRepositoryPort
{
    private readonly MarketplaceDbContext _context;

    public PluginDistributionRepositoryAdapter(MarketplaceDbContext context)
    {
        _context = context;
    }

    // -------------------------------------------------------------------------
    // IPluginDistributionRepositoryPort
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<DownloadResolutionResult> ResolveAsync(
        Guid pluginId,
        string? version,
        CancellationToken ct = default)
    {
        // Check plugin existence first.
        bool pluginExists = await _context.Plugins
            .AnyAsync(p => p.Id == pluginId, ct);

        if (!pluginExists)
            return new PluginNotFoundResult();

        // Resolve version row.
        PluginVersionEntity? versionRow = version is null
            ? await _context.PluginVersions
                .Where(pv => pv.PluginId == pluginId && pv.IsLatest)
                .AsNoTracking()
                .FirstOrDefaultAsync(ct)
            : await _context.PluginVersions
                .Where(pv => pv.PluginId == pluginId && pv.Version == version)
                .AsNoTracking()
                .FirstOrDefaultAsync(ct);

        if (versionRow is null)
            return new VersionNotFoundResult(version ?? string.Empty);

        // Fetch the plugin name for the filename.
        string pluginName = await _context.Plugins
            .Where(p => p.Id == pluginId)
            .Select(p => p.Name)
            .FirstAsync(ct);

        DownloadResolution resolution = new(
            PluginName: pluginName,
            Version: versionRow.Version,
            PackageKey: versionRow.PackageKey,
            PackageFormat: versionRow.PackageFormat,
            SizeBytes: versionRow.SizeBytes,
            Sha256: versionRow.Sha256);

        return new FoundResult(resolution);
    }

    /// <inheritdoc />
    public async Task IncrementDownloadCountAsync(
        Guid pluginId,
        string version,
        CancellationToken ct = default)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Single transaction: all three updates are atomic so N concurrent calls yield exactly +N.
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx =
            await _context.Database.BeginTransactionAsync(ct);

        try
        {
            // 1. Upsert telemetry_aggregates using PostgreSQL ON CONFLICT DO UPDATE.
            //    DB-side increment (count = count + 1) is race-safe.
            await _context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO telemetry_aggregates (plugin_id, version, event_type, window_start, count)
                VALUES ({0}, {1}, 'download', {2}, 1)
                ON CONFLICT (plugin_id, version, event_type, window_start)
                DO UPDATE SET count = telemetry_aggregates.count + 1
                """,
                [pluginId, version, today],
                ct);

            // 2. Increment plugin_versions.download_count in-place (DB-side arithmetic).
            await _context.PluginVersions
                .Where(pv => pv.PluginId == pluginId && pv.Version == version)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        pv => pv.DownloadCount,
                        pv => pv.DownloadCount + 1),
                    ct);

            // 3. Increment plugins.download_count in-place.
            await _context.Plugins
                .Where(p => p.Id == pluginId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        p => p.DownloadCount,
                        p => p.DownloadCount + 1),
                    ct);

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
