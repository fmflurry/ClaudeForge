using ClaudeForge.Application.Modules.AddOnPublishing.Ports;
using ClaudeForge.Core.Shared.Model;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Infrastructure.AddOnPublishing;

/// <summary>
/// EF Core adapter implementing <see cref="IAddOnPublishingRepositoryPort"/>.
///
/// Key invariants enforced:
///   - CreatePluginWithInitialVersionAsync: atomic insert of plugin + initial version (is_latest = true).
///   - AddVersionAsync: within a transaction, set all prior is_latest = false, then insert new version
///     with is_latest = true. Respects the partial UNIQUE index.
///   - GetVersionHistoryAsync: paginated, ordered by version_sort DESC (semver descending).
/// </summary>
public sealed class AddOnPublishingRepositoryAdapter : IAddOnPublishingRepositoryPort
{
    private readonly MarketplaceDbContext _context;

    public AddOnPublishingRepositoryAdapter(MarketplaceDbContext context)
    {
        _context = context;
    }

    // -------------------------------------------------------------------------
    // CreatePluginWithInitialVersionAsync
    // -------------------------------------------------------------------------

    public async Task<AddOnPublishResult> CreatePluginWithInitialVersionAsync(
        CreateAddOnCommand command,
        CancellationToken ct = default)
    {
        Guid pluginId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        AddOnEntity plugin = new()
        {
            Id = pluginId,
            Name = command.Name,
            NameNormalized = command.NameNormalized,
            Slug = command.Slug,
            Description = command.Description,
            Author = command.Author,
            DownloadCount = 0L,
            CreatedAt = now,
            UpdatedAt = now,
            Visibility = command.Visibility,
            OwnerOrgId = command.OwnerOrgId,
            OwnerUserId = command.OwnerUserId,
        };

        AddOnVersionEntity version = new()
        {
            Id = Guid.NewGuid(),
            PluginId = pluginId,
            Version = command.Version,
            VersionSort = command.VersionSort,
            ReleaseNotes = command.ReleaseNotes,
            IsLatest = true,
            PackageKey = command.PackageKey,
            PackageFormat = command.PackageFormat,
            SizeBytes = command.SizeBytes,
            Sha256 = command.Sha256,
            DownloadCount = 0L,
            ReadmeText = command.ReadmeText,
            ReleasedAt = now,
        };

        plugin.Versions.Add(version);

        _context.Plugins.Add(plugin);
        await _context.SaveChangesAsync(ct);

        return new AddOnPublishResult(pluginId, command.Version);
    }

    // -------------------------------------------------------------------------
    // AddVersionAsync — atomic is_latest flip
    // -------------------------------------------------------------------------

    public async Task<AddOnVersionPublishResult> AddVersionAsync(
        Guid pluginId,
        AddVersionCommand command,
        CancellationToken ct = default)
    {
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx =
            await _context.Database.BeginTransactionAsync(ct);

        try
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;

            // Flip all existing latest versions to false
            await _context.PluginVersions
                .Where(v => v.PluginId == pluginId && v.IsLatest)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(v => v.IsLatest, false),
                    ct);

            // Stamp plugins.updated_at to reflect the new version (LOW-5)
            await _context.Plugins
                .Where(p => p.Id == pluginId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(p => p.UpdatedAt, now),
                    ct);

            // Insert new version as latest
            Guid versionId = Guid.NewGuid();
            AddOnVersionEntity newVersion = new()
            {
                Id = versionId,
                PluginId = pluginId,
                Version = command.Version,
                VersionSort = command.VersionSort,
                ReleaseNotes = command.ReleaseNotes,
                IsLatest = true,
                PackageKey = command.PackageKey,
                PackageFormat = command.PackageFormat,
                SizeBytes = command.SizeBytes,
                Sha256 = command.Sha256,
                DownloadCount = 0L,
                ReadmeText = command.ReadmeText,
                ReleasedAt = now,
            };

            _context.PluginVersions.Add(newVersion);
            await _context.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);

            return new AddOnVersionPublishResult(pluginId, versionId, command.Version);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // Existence checks
    // -------------------------------------------------------------------------

    public Task<bool> ExistsByNameNormalizedAsync(
        string nameNormalized,
        CancellationToken ct = default)
    {
        return _context.Plugins.AnyAsync(
            p => p.NameNormalized == nameNormalized, ct);
    }

    public Task<bool> PluginExistsAsync(
        Guid pluginId,
        CancellationToken ct = default)
    {
        return _context.Plugins.AnyAsync(p => p.Id == pluginId, ct);
    }

    public Task<bool> VersionExistsAsync(
        Guid pluginId,
        string version,
        CancellationToken ct = default)
    {
        return _context.PluginVersions.AnyAsync(
            v => v.PluginId == pluginId && v.Version == version, ct);
    }

    // -------------------------------------------------------------------------
    // GetVersionHistoryAsync — paginated, semver descending
    // -------------------------------------------------------------------------

    public async Task<(IReadOnlyList<VersionHistoryDto> Items, int TotalCount)> GetVersionHistoryAsync(
        Guid pluginId,
        PaginationRequest pagination,
        CancellationToken ct = default)
    {
        IQueryable<AddOnVersionEntity> query = _context.PluginVersions
            .Where(v => v.PluginId == pluginId)
            .AsNoTracking();

        int totalCount = await query.CountAsync(ct);

        int skip = (pagination.Page - 1) * pagination.Limit;

        List<AddOnVersionEntity> entities = await query
            .OrderByDescending(v => v.VersionSort)
            .Skip(skip)
            .Take(pagination.Limit)
            .ToListAsync(ct);

        IReadOnlyList<VersionHistoryDto> items = entities
            .Select(v => new VersionHistoryDto(
                Id: v.Id,
                Version: v.Version,
                VersionSort: v.VersionSort,
                IsLatest: v.IsLatest,
                ReleasedAt: v.ReleasedAt,
                ReleaseNotes: v.ReleaseNotes,
                DownloadCount: v.DownloadCount))
            .ToList();

        return (items, totalCount);
    }

    // -------------------------------------------------------------------------
    // GetPluginVisibilityAsync / UpdateVisibilityAsync
    // -------------------------------------------------------------------------

    public async Task<(string Visibility, Guid? OwnerOrgId)?> GetPluginVisibilityAsync(
        Guid pluginId,
        CancellationToken ct = default)
    {
        AddOnEntity? plugin = await _context.Plugins
            .AsNoTracking()
            .Where(p => p.Id == pluginId)
            .Select(p => new AddOnEntity { Id = p.Id, Visibility = p.Visibility, OwnerOrgId = p.OwnerOrgId })
            .FirstOrDefaultAsync(ct);

        if (plugin is null)
            return null;

        return (plugin.Visibility, plugin.OwnerOrgId);
    }

    public async Task UpdateVisibilityAsync(
        Guid pluginId,
        string visibility,
        Guid? ownerOrgId,
        CancellationToken ct = default)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await _context.Plugins
            .Where(p => p.Id == pluginId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(p => p.Visibility, visibility)
                    .SetProperty(p => p.OwnerOrgId, ownerOrgId)
                    .SetProperty(p => p.UpdatedAt, now),
                ct);
    }

    // -------------------------------------------------------------------------
    // GetVersionAsync — single version detail
    // -------------------------------------------------------------------------

    public async Task<VersionDetailDto?> GetVersionAsync(
        Guid pluginId,
        string version,
        CancellationToken ct = default)
    {
        AddOnVersionEntity? entity = await _context.PluginVersions
            .Where(v => v.PluginId == pluginId && v.Version == version)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        if (entity is null)
            return null;

        return new VersionDetailDto(
            Id: entity.Id,
            PluginId: entity.PluginId,
            Version: entity.Version,
            IsLatest: entity.IsLatest,
            ReleasedAt: entity.ReleasedAt,
            ReleaseNotes: entity.ReleaseNotes,
            DownloadCount: entity.DownloadCount,
            SizeBytes: entity.SizeBytes,
            Sha256: entity.Sha256,
            PackageFormat: entity.PackageFormat);
    }
}
