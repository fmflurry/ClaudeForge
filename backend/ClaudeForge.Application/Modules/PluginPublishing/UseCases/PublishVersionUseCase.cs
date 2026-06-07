using System.Security.Cryptography;
using ClaudeForge.Application.Modules.PluginPublishing.Ports;
using ClaudeForge.Core.Domain.Packaging;
using ClaudeForge.Core.Domain.Plugins;
using ClaudeForge.Core.Ports;

namespace ClaudeForge.Application.Modules.PluginPublishing.UseCases;

/// <summary>
/// Orchestrates publishing a new version to an existing plugin:
/// 1. Verifies the plugin exists (404 if not).
/// 2. Validates the version format via SemVer.Parse.
/// 3. Checks for duplicate (pluginId, version).
/// 4. Buffers stream + validates magic bytes against declared extension (H3).
/// 5. Reads the package archive.
/// 6. Stores the package artifact (with locally-computed SHA-256 + size).
/// 7. Persists the new version (atomically flips is_latest).
///    If persistence fails, best-effort deletes the stored artifact (HIGH-3).
/// </summary>
public sealed class PublishVersionUseCase
{
    private readonly IPluginPublishingRepositoryPort _repository;
    private readonly IPackageStoragePort _storage;
    private readonly IPackageReader _packageReader;

    public PublishVersionUseCase(
        IPluginPublishingRepositoryPort repository,
        IPackageStoragePort storage,
        IPackageReader packageReader)
    {
        _repository = repository;
        _storage = storage;
        _packageReader = packageReader;
    }

    public async Task<PluginVersionPublishResult> ExecuteAsync(
        PublishVersionCommand command,
        CancellationToken ct = default)
    {
        // Step 1: verify plugin exists
        bool pluginExists = await _repository.PluginExistsAsync(command.PluginId, ct);
        if (!pluginExists)
            throw new PluginNotFoundForVersionException();

        // Step 2: validate version format
        SemVer semVer = ParseVersionOrThrow(command.Version);

        // Step 3: check for duplicate version
        bool versionExists = await _repository.VersionExistsAsync(command.PluginId, command.Version, ct);
        if (versionExists)
            throw new DuplicateVersionException(command.Version);

        // Step 4: buffer stream + validate magic bytes before reading archive
        // (IPackageReader consumes the stream; we need bytes for both operations)
        byte[] packageBytes = await BufferStreamAsync(command.PackageStream, ct);
        string ext = GetExtensionOrThrow(command.FileName, packageBytes);
        string sha256 = ComputeSha256(packageBytes);
        long sizeBytes = packageBytes.LongLength;

        // Step 5: read archive from buffered bytes — may throw packaging exceptions (bubble up)
        await _packageReader.ReadAsync(new MemoryStream(packageBytes), command.FileName, ct);

        // Step 6: store package
        string packageKey = $"plugins/{command.PluginId}/{command.Version}/package.{ext}";

        await _storage.PutAsync(packageKey, new MemoryStream(packageBytes), ct);

        // Step 7: persist new version (is_latest flip handled atomically in adapter).
        // If persistence fails, best-effort delete the orphaned storage object (HIGH-3).
        AddVersionCommand addCommand = new(
            Version: command.Version,
            VersionSort: semVer.ToVersionSort(),
            PackageKey: packageKey,
            PackageFormat: ext,
            SizeBytes: sizeBytes,
            Sha256: sha256,
            ReleaseNotes: command.ReleaseNotes);

        try
        {
            return await _repository.AddVersionAsync(command.PluginId, addCommand, ct);
        }
        catch
        {
            // Best-effort cleanup of the orphaned storage object — do not suppress the original exception.
            try { await _storage.DeleteAsync(packageKey, ct); } catch { /* ignore cleanup errors */ }
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static SemVer ParseVersionOrThrow(string version)
    {
        try
        {
            return SemVer.Parse(version);
        }
        catch (ArgumentException)
        {
            throw new InvalidVersionFormatException();
        }
    }

    /// <summary>
    /// Returns the canonical extension ("tar.gz" or "zip") for the file, validated against
    /// actual magic bytes.
    /// Throws <see cref="UnsupportedPackageFormatException"/> for unknown extensions (H3).
    /// Throws <see cref="CorruptedArchiveException"/> when magic bytes don't match the declared
    /// extension (content/format mismatch — the file is not what it claims to be) (H3).
    /// </summary>
    private static string GetExtensionOrThrow(string fileName, byte[] packageBytes)
    {
        if (fileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
        {
            if (!IsGzipMagic(packageBytes))
                throw new CorruptedArchiveException();
            return "tar.gz";
        }

        if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            if (!IsZipMagic(packageBytes))
                throw new CorruptedArchiveException();
            return "zip";
        }

        // Unknown extension — reject instead of defaulting.
        throw new UnsupportedPackageFormatException();
    }

    private static bool IsGzipMagic(byte[] bytes) =>
        bytes.Length >= 2 && bytes[0] == 0x1F && bytes[1] == 0x8B;

    private static bool IsZipMagic(byte[] bytes) =>
        bytes.Length >= 4 &&
        bytes[0] == 0x50 && bytes[1] == 0x4B &&
        bytes[2] == 0x03 && bytes[3] == 0x04;

    private static async Task<byte[]> BufferStreamAsync(Stream stream, CancellationToken ct)
    {
        if (stream.CanSeek)
            stream.Position = 0;

        using MemoryStream ms = new();
        await stream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    private static string ComputeSha256(byte[] bytes)
    {
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
}
