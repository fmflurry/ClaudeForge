namespace ClaudeForge.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the <c>plugin_versions</c> table.
/// </summary>
public sealed class PluginVersionEntity
{
    public Guid Id { get; set; }
    public Guid PluginId { get; set; }

    /// <summary>
    /// Semantic version string (e.g. "1.2.3").
    /// Combined with PluginId in a UNIQUE constraint.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Pre-computed bigint sort key from <c>SemVer.ToVersionSort()</c>.
    /// Enables efficient <c>ORDER BY version_sort DESC</c> queries.
    /// </summary>
    public long VersionSort { get; set; }

    public string ReleaseNotes { get; set; } = string.Empty;

    /// <summary>
    /// When <c>true</c>, this is the latest published version.
    /// A partial UNIQUE index on (plugin_id) WHERE is_latest=TRUE enforces
    /// at most one latest version per plugin.
    /// </summary>
    public bool IsLatest { get; set; }

    /// <summary>
    /// Object-storage path: <c>plugins/{pluginId}/{version}/package.tar.gz</c>.
    /// NOT NULL.
    /// </summary>
    public string PackageKey { get; set; } = string.Empty;

    /// <summary>
    /// Archive format: <c>'tar.gz'</c> or <c>'zip'</c>.
    /// </summary>
    public string PackageFormat { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    /// <summary>
    /// SHA-256 hex digest (64 characters).
    /// </summary>
    public string Sha256 { get; set; } = string.Empty;

    public long DownloadCount { get; set; }

    /// <summary>
    /// README extracted from the package archive at upload time.
    /// </summary>
    public string? ReadmeText { get; set; }

    public DateTimeOffset ReleasedAt { get; set; }

    // Navigation property
    public PluginEntity Plugin { get; set; } = null!;
}
