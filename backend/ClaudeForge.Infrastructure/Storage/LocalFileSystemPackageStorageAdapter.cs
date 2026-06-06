using System.Security.Cryptography;
using ClaudeForge.Core.Ports;

namespace ClaudeForge.Infrastructure.Storage;

/// <summary>
/// Dev-environment implementation of <see cref="IPackageStoragePort"/> backed by the local filesystem.
/// Packages are stored under <c>rootPath</c> with key path segments mapped to nested directories,
/// e.g. key <c>plugins/{pluginId}/{version}/package.tar.gz</c> becomes
/// <c>{rootPath}/plugins/{pluginId}/{version}/package.tar.gz</c>.
///
/// Packages are immutable: a second <see cref="PutAsync"/> call for the same key throws
/// <see cref="PackageAlreadyExistsException"/>.
/// </summary>
public sealed class LocalFileSystemPackageStorageAdapter : IPackageStoragePort
{
    private readonly string _rootPath;

    public LocalFileSystemPackageStorageAdapter(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        _rootPath = rootPath;
    }

    /// <inheritdoc />
    public async Task PutAsync(string key, Stream content, CancellationToken ct = default)
    {
        string filePath = ResolvePath(key);

        if (File.Exists(filePath))
            throw new PackageAlreadyExistsException(key);

        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await using FileStream fileStream = new(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fileStream, ct);
    }

    /// <inheritdoc />
    public Task<Stream> GetAsync(string key, CancellationToken ct = default)
    {
        string filePath = ResolvePath(key);
        Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        string filePath = ResolvePath(key);
        return Task.FromResult(File.Exists(filePath));
    }

    /// <inheritdoc />
    public async Task<PackageMetadata> GetMetadataAsync(string key, CancellationToken ct = default)
    {
        string filePath = ResolvePath(key);

        byte[] fileBytes = await File.ReadAllBytesAsync(filePath, ct);
        byte[] hashBytes = SHA256.HashData(fileBytes);
        string sha256Hex = Convert.ToHexStringLower(hashBytes);

        return new PackageMetadata(sha256Hex, fileBytes.LongLength);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Resolves a storage key to an absolute filesystem path by replacing forward-slash
    /// separators with the platform directory separator and combining with <see cref="_rootPath"/>.
    /// </summary>
    private string ResolvePath(string key)
    {
        // Normalize forward-slash key separators to the OS path separator,
        // then combine with the root to produce an absolute path.
        string normalizedKey = key.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(_rootPath, normalizedKey);
    }
}
