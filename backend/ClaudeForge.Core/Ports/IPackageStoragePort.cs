namespace ClaudeForge.Core.Ports;

/// <summary>
/// Outgoing port for immutable plugin package artifact storage.
/// Packages are write-once; attempting to overwrite an existing key throws
/// <see cref="PackageAlreadyExistsException"/>.
/// </summary>
public interface IPackageStoragePort
{
    /// <summary>
    /// Stores <paramref name="content"/> under <paramref name="key"/>.
    /// </summary>
    /// <exception cref="PackageAlreadyExistsException">
    /// Thrown when a package already exists at <paramref name="key"/> (immutability invariant).
    /// </exception>
    Task PutAsync(string key, Stream content, CancellationToken ct = default);

    /// <summary>
    /// Returns a readable stream of the package stored at <paramref name="key"/>.
    /// The caller is responsible for disposing the returned stream.
    /// </summary>
    Task<Stream> GetAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Returns <c>true</c> when a package is stored at <paramref name="key"/>.
    /// </summary>
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Returns metadata (SHA-256 hex + size in bytes) for the package at <paramref name="key"/>.
    /// </summary>
    Task<PackageMetadata> GetMetadataAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Deletes the package stored at <paramref name="key"/> if it exists.
    /// This is a best-effort cleanup operation; callers must not rely on it for consistency guarantees.
    /// Implementations should suppress "not found" errors and only rethrow on genuine infrastructure failures.
    /// </summary>
    Task DeleteAsync(string key, CancellationToken ct = default);
}
