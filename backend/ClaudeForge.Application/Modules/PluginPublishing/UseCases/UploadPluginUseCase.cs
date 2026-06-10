using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClaudeForge.Application.Modules.PluginPublishing.Ports;
using ClaudeForge.Core.Domain.Packaging;
using ClaudeForge.Core.Domain.Plugins;
using ClaudeForge.Core.Ports;
using ClaudeForge.Core.Shared.Authorization;
using ClaudeForge.Core.Shared.Exceptions;
using ICategoryLookupPort = ClaudeForge.Application.Modules.PluginPublishing.Ports.ICategoryLookupPort;

namespace ClaudeForge.Application.Modules.PluginPublishing.UseCases;

/// <summary>
/// Orchestrates the upload of a new plugin:
/// 1. Validates the package stream is non-empty.
/// 2. Validates magic bytes against declared extension (H3).
/// 3. Reads and extracts the archive (manifest + README).
/// 4. Validates the manifest fields.
/// 5. Validates the initial version is a valid semver.
/// 6. Checks for duplicate plugin name (case-insensitive).
/// 7. Enforces visibility + org membership access control.
/// 8. Stores the package artifact via IPackageStoragePort.
/// 9. Persists the plugin and initial version.
///    If persistence fails, best-effort deletes the stored artifact (HIGH-3).
/// </summary>
public sealed class UploadPluginUseCase
{
    private readonly IPluginPublishingRepositoryPort _repository;
    private readonly IPackageStoragePort _storage;
    private readonly IPackageReader _packageReader;
    private readonly ICurrentUser _currentUser;
    private readonly IOrgMembershipQueryPort _membershipQuery;
    private readonly IPluginAccessPolicy _accessPolicy;
    private readonly ICategoryLookupPort? _categoryLookup;

    public UploadPluginUseCase(
        IPluginPublishingRepositoryPort repository,
        IPackageStoragePort storage,
        IPackageReader packageReader,
        ICurrentUser currentUser,
        IOrgMembershipQueryPort membershipQuery,
        IPluginAccessPolicy accessPolicy,
        ICategoryLookupPort? categoryLookup = null)
    {
        _repository = repository;
        _storage = storage;
        _packageReader = packageReader;
        _currentUser = currentUser;
        _membershipQuery = membershipQuery;
        _accessPolicy = accessPolicy;
        _categoryLookup = categoryLookup;
    }

    /// <summary>
    /// Backward-compatible constructor for unit tests without identity context.
    /// Behaves as anonymous caller (no auth checks performed).
    /// </summary>
    public UploadPluginUseCase(
        IPluginPublishingRepositoryPort repository,
        IPackageStoragePort storage,
        IPackageReader packageReader)
    {
        _repository = repository;
        _storage = storage;
        _packageReader = packageReader;
        _currentUser = new UploadAnonymousCurrentUser();
        _membershipQuery = new UploadNoOpMembershipQueryPort();
        _accessPolicy = new PluginAccessPolicy();
    }

    public async Task<PluginPublishResult> ExecuteAsync(
        UploadPluginCommand command,
        CancellationToken ct = default)
    {
        // Step 1: buffer stream early — reject empty and compute SHA-256/size before
        // any further processing (stream may not be seekable; PackageReader consumes it)
        byte[] packageBytes = await BufferStreamAsync(command.PackageStream, ct);

        if (packageBytes.Length == 0)
            throw new MissingPackageFileException();

        // Step 2: validate magic bytes match declared extension (H3)
        string ext = GetExtensionOrThrow(command.FileName, packageBytes);

        string sha256 = ComputeSha256(packageBytes);
        long sizeBytes = packageBytes.LongLength;

        // Step 3: read archive from buffered bytes — may throw UnsupportedPackageFormatException,
        //         CorruptedArchiveException, or MissingManifestException (all bubble up)
        PackageContents contents = await _packageReader.ReadAsync(
            new MemoryStream(packageBytes), command.FileName, ct);

        // Step 4: parse + validate manifest
        ParsedManifest manifest = ParseManifest(contents.ManifestBytes);
        ValidateManifest(manifest);

        // Step 5: validate semver on command.InitialVersion
        SemVer semVer = ParseSemVerOrThrow(command.InitialVersion);

        // Step 6: duplicate name check (case-insensitive)
        string nameNormalized = command.Name.ToLowerInvariant();
        bool nameExists = await _repository.ExistsByNameNormalizedAsync(nameNormalized, ct);
        if (nameExists)
            throw new DuplicatePluginNameException(command.Name);

        // Step 7: enforce visibility + org membership
        string visibility = string.IsNullOrWhiteSpace(command.Visibility)
            ? "public"
            : command.Visibility.ToLowerInvariant();

        Guid? ownerOrgId = command.OwnerOrgId;
        Guid? ownerUserId = _currentUser.IsAuthenticated ? _currentUser.UserId : command.OwnerUserId;

        // Private requires ownerOrgId — fail fast before any storage write
        if (visibility == "private" && ownerOrgId is null)
            throw new PrivatePluginRequiresOrgException();

        // If private with org, caller must be a member of that org
        if (visibility == "private" && ownerOrgId is not null)
        {
            IReadOnlySet<Guid> callerOrgIds = await ResolveCallerOrgIdsAsync(ct);
            AccessDecision decision = _accessPolicy.DecideWrite(
                _currentUser, ownerOrgId.Value, callerOrgIds);

            if (decision == AccessDecision.Forbidden)
                throw new PluginWriteForbiddenException();

            // DecideWrite can return Unauthenticated but the upload endpoint gate handles
            // that case at the HTTP layer when flag is ON. We defensively handle it here too.
            if (decision == AccessDecision.Unauthenticated)
                throw new AuthenticationException("Authentication is required to publish a private plugin.");
        }

        // Step 8: store package — key convention: plugins/{pluginId}/{version}/package.{ext}
        Guid pluginId = Guid.NewGuid();
        string packageKey = $"plugins/{pluginId}/{command.InitialVersion}/package.{ext}";

        await _storage.PutAsync(packageKey, new MemoryStream(packageBytes), ct);

        // Step 9a: resolve category tags if provided
        IReadOnlyList<short>? resolvedCategoryIds = null;
        if (_categoryLookup is not null)
        {
            IReadOnlyDictionary<string, short> vocab = await _categoryLookup.GetAllCategoryKeysAsync(ct);
            resolvedCategoryIds = await ResolveCategoryTagsAsync(command, vocab, ct);
        }

        // Step 9b: persist — if this throws, best-effort delete the orphaned artifact (HIGH-3)
        string slug = BuildSlug(nameNormalized);
        CreatePluginCommand createCommand = new(
            Name: command.Name,
            NameNormalized: nameNormalized,
            Slug: slug,
            Description: command.Description,
            Author: command.Author,
            Version: command.InitialVersion,
            VersionSort: semVer.ToVersionSort(),
            PackageKey: packageKey,
            PackageFormat: ext,
            SizeBytes: sizeBytes,
            Sha256: sha256,
            ReleaseNotes: command.ReleaseNotes,
            ReadmeText: contents.ReadmeText,
            Visibility: visibility,
            OwnerOrgId: ownerOrgId,
            OwnerUserId: ownerUserId,
            ResolvedCategoryIds: resolvedCategoryIds);

        try
        {
            return await _repository.CreatePluginWithInitialVersionAsync(createCommand, ct);
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

    /// <summary>
    /// Resolves command category tag strings (Types, Languages, UseCaseTags) into
    /// category IDs using the provided vocabulary. Throws <see cref="UnknownCategoryTagException"/>
    /// for any value not present in the vocabulary for its dimension.
    /// </summary>
    private static Task<IReadOnlyList<short>?> ResolveCategoryTagsAsync(
        UploadPluginCommand command,
        IReadOnlyDictionary<string, short> vocab,
        CancellationToken ct)
    {
        _ = ct; // synchronous resolution
        var resolved = new List<short>();

        if (command.Types is not null && command.Types.Count > 0)
        {
            List<string> invalid = command.Types
                .Where(v => !vocab.ContainsKey($"type:{v}"))
                .ToList();
            if (invalid.Count > 0)
                throw new UnknownCategoryTagException("type", invalid);
            resolved.AddRange(command.Types.Select(v => vocab[$"type:{v}"]));
        }

        if (command.Languages is not null && command.Languages.Count > 0)
        {
            List<string> invalid = command.Languages
                .Where(v => !vocab.ContainsKey($"language:{v}"))
                .ToList();
            if (invalid.Count > 0)
                throw new UnknownCategoryTagException("language", invalid);
            resolved.AddRange(command.Languages.Select(v => vocab[$"language:{v}"]));
        }

        if (command.UseCaseTags is not null && command.UseCaseTags.Count > 0)
        {
            List<string> invalid = command.UseCaseTags
                .Where(v => !vocab.ContainsKey($"use_case:{v}"))
                .ToList();
            if (invalid.Count > 0)
                throw new UnknownCategoryTagException("use_case", invalid);
            resolved.AddRange(command.UseCaseTags.Select(v => vocab[$"use_case:{v}"]));
        }

        IReadOnlyList<short>? result = resolved.Count > 0 ? resolved.AsReadOnly() : null;
        return Task.FromResult(result);
    }

    private async Task<IReadOnlySet<Guid>> ResolveCallerOrgIdsAsync(CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
        {
            return new HashSet<Guid>();
        }

        Guid[] orgIds = await _membershipQuery.GetOrgIdsForUserAsync(_currentUser.UserId.Value, ct);
        return new HashSet<Guid>(orgIds);
    }

    private sealed record ParsedManifest(string Name, string Description, string Author);

    private static ParsedManifest ParseManifest(byte[] manifestBytes)
    {
        string json = Encoding.UTF8.GetString(manifestBytes);

        // HIGH-5: use `using` to ensure JsonDocument is disposed; bound depth to limit parsing risk.
        using JsonDocument doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
        JsonElement root = doc.RootElement;

        string? name = root.TryGetProperty("name", out JsonElement nameProp)
            ? nameProp.GetString()
            : null;

        string? description = root.TryGetProperty("description", out JsonElement descProp)
            ? descProp.GetString()
            : null;

        string? author = root.TryGetProperty("author", out JsonElement authorProp)
            ? authorProp.GetString()
            : null;

        // Extract values before JsonDocument is disposed (strings are already copied out of the doc).
        return new ParsedManifest(
            Name: name ?? string.Empty,
            Description: description ?? string.Empty,
            Author: author ?? string.Empty);
    }

    private static void ValidateManifest(ParsedManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.Name))
            throw new MissingRequiredFieldException("name");

        if (string.IsNullOrWhiteSpace(manifest.Description))
            throw new MissingRequiredFieldException("description");

        if (string.IsNullOrWhiteSpace(manifest.Author))
            throw new MissingRequiredFieldException("author");
    }

    private static SemVer ParseSemVerOrThrow(string version)
    {
        try
        {
            return SemVer.Parse(version);
        }
        catch (ArgumentException)
        {
            throw new InvalidSemVerException();
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

    private static string BuildSlug(string nameNormalized)
    {
        return nameNormalized.Replace(" ", "-");
    }

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

// -------------------------------------------------------------------------
// Internal stubs for backward-compatible constructor (anonymous caller)
// -------------------------------------------------------------------------

file sealed class UploadAnonymousCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => false;
    public Guid? UserId => null;
    public string? Email => null;
}

file sealed class UploadNoOpMembershipQueryPort : IOrgMembershipQueryPort
{
    public Task<Guid[]> GetOrgIdsForUserAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult(Array.Empty<Guid>());

    public Task<bool> IsMemberAsync(Guid userId, Guid orgId, string? minRole = null, CancellationToken ct = default)
        => Task.FromResult(false);

    public void InvalidateUser(Guid userId) { }
}
