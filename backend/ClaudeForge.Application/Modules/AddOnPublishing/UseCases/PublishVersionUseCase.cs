using System.Security.Cryptography;
using ClaudeForge.Application.Modules.AddOnPublishing.Ports;
using ClaudeForge.Core.Domain.Packaging;
using ClaudeForge.Core.Domain.Plugins;
using ClaudeForge.Core.Ports;
using ClaudeForge.Core.Shared.Authorization;
using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Application.Modules.AddOnPublishing.UseCases;

/// <summary>
/// Orchestrates publishing a new version to an existing plugin:
/// 0. Enforces authentication and authorization — only the org that owns the plugin
///    (or the original creator for ownerless public plugins) may publish a new version.
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
    private readonly IAddOnPublishingRepositoryPort _repository;
    private readonly IPackageStoragePort _storage;
    private readonly IPackageReader _packageReader;
    private readonly ICurrentUser _currentUser;
    private readonly IOrgMembershipQueryPort _membershipQuery;
    private readonly IAddOnAccessPolicy _accessPolicy;

    public PublishVersionUseCase(
        IAddOnPublishingRepositoryPort repository,
        IPackageStoragePort storage,
        IPackageReader packageReader,
        ICurrentUser currentUser,
        IOrgMembershipQueryPort membershipQuery,
        IAddOnAccessPolicy accessPolicy)
    {
        _repository = repository;
        _storage = storage;
        _packageReader = packageReader;
        _currentUser = currentUser;
        _membershipQuery = membershipQuery;
        _accessPolicy = accessPolicy;
    }

    public async Task<AddOnVersionPublishResult> ExecuteAsync(
        PublishVersionCommand command,
        CancellationToken ct = default)
    {
        // Step 0: authenticate + authorize before touching any data.
        // Load the plugin's persisted owner information (never trusting caller-supplied org).
        (string Visibility, Guid? OwnerOrgId, Guid? OwnerUserId)? pluginOwnership =
            await _repository.GetPluginVisibilityAsync(command.PluginId, ct);

        if (pluginOwnership is null)
            throw new AddOnNotFoundForVersionException();

        await AuthorizeWriteAsync(pluginOwnership.Value, ct);

        // Step 1: verify plugin exists (redundant after the ownership load, but kept for clarity)
        // The existence check is already satisfied by GetPluginVisibilityAsync returning non-null above.

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
    // Authorization
    // -------------------------------------------------------------------------

    private async Task AuthorizeWriteAsync(
        (string Visibility, Guid? OwnerOrgId, Guid? OwnerUserId) ownership,
        CancellationToken ct)
    {
        // Authentication is always required to publish a version.
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new AuthenticationException("Authentication is required to publish a plugin version.");

        if (ownership.OwnerOrgId is not null)
        {
            // Plugin is org-owned — caller must be a member of that org.
            IReadOnlySet<Guid> callerOrgIds = await ResolveCallerOrgIdsAsync(ct);
            AccessDecision decision = _accessPolicy.DecideWrite(
                _currentUser, ownership.OwnerOrgId.Value, callerOrgIds);

            if (decision == AccessDecision.Forbidden)
                throw new AddOnWriteForbiddenException();
        }
        else
        {
            // Ownerless plugin: only the original creator (OwnerUserId) may publish a version.
            // If there is no OwnerUserId, the plugin is fully anonymous — reject all writes
            // (defense-in-depth; anonymous plugins should not exist after the upload auth gate
            // is fully enforced, but we must fail-secure here regardless).
            if (ownership.OwnerUserId is null ||
                _currentUser.UserId != ownership.OwnerUserId)
            {
                throw new AddOnWriteForbiddenException();
            }
        }
    }

    private async Task<IReadOnlySet<Guid>> ResolveCallerOrgIdsAsync(CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return new HashSet<Guid>();

        Guid[] orgIds = await _membershipQuery.GetOrgIdsForUserAsync(_currentUser.UserId.Value, ct);
        return new HashSet<Guid>(orgIds);
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
