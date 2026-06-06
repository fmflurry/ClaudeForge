namespace ClaudeForge.Infrastructure.Storage;

/// <summary>
/// Strongly-typed configuration for the package storage provider (section "PackageStorage").
/// </summary>
public sealed class StorageOptions
{
    /// <summary>
    /// Selects the storage backend. Allowed values: "LocalFileSystem", "OVHObjectStorage".
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Root directory path used when <see cref="Type"/> is "LocalFileSystem".
    /// </summary>
    public string? LocalPath { get; init; }

    /// <summary>
    /// OVH / S3-compatible storage settings used when <see cref="Type"/> is "OVHObjectStorage".
    /// </summary>
    public OvhStorageOptions? Ovh { get; init; }
}
