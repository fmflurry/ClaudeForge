using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClaudeForge.Application.Modules.PluginPublishing.Ports;
using ClaudeForge.Core.Domain.Plugins;
using ClaudeForge.Core.Ports;

namespace ClaudeForge.Application.Modules.PluginPublishing.UseCases;

/// <summary>
/// Orchestrates the upload of a new plugin:
/// 1. Validates the package stream is non-empty.
/// 2. Reads and extracts the archive (manifest + README).
/// 3. Validates the manifest fields.
/// 4. Validates the initial version is a valid semver.
/// 5. Checks for duplicate plugin name (case-insensitive).
/// 6. Stores the package artifact via IPackageStoragePort.
/// 7. Persists the plugin and initial version.
/// </summary>
public sealed class UploadPluginUseCase
{
    private readonly IPluginPublishingRepositoryPort _repository;
    private readonly IPackageStoragePort _storage;
    private readonly IPackageReader _packageReader;

    public UploadPluginUseCase(
        IPluginPublishingRepositoryPort repository,
        IPackageStoragePort storage,
        IPackageReader packageReader)
    {
        _repository = repository;
        _storage = storage;
        _packageReader = packageReader;
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

        string sha256 = ComputeSha256(packageBytes);
        long sizeBytes = packageBytes.LongLength;

        // Step 2: read archive from buffered bytes — may throw UnsupportedPackageFormatException,
        //         CorruptedArchiveException, or MissingManifestException (all bubble up)
        PackageContents contents = await _packageReader.ReadAsync(
            new MemoryStream(packageBytes), command.FileName, ct);

        // Step 3: parse + validate manifest
        ParsedManifest manifest = ParseManifest(contents.ManifestBytes);
        ValidateManifest(manifest);

        // Step 4: validate semver on command.InitialVersion
        SemVer semVer = ParseSemVerOrThrow(command.InitialVersion);

        // Step 5: duplicate name check (case-insensitive)
        string nameNormalized = command.Name.ToLowerInvariant();
        bool nameExists = await _repository.ExistsByNameNormalizedAsync(nameNormalized, ct);
        if (nameExists)
            throw new DuplicatePluginNameException(command.Name);

        // Step 6: store package — key convention: plugins/{pluginId}/{version}/package.{ext}
        Guid pluginId = Guid.NewGuid();
        string ext = GetExtension(command.FileName);
        string packageKey = $"plugins/{pluginId}/{command.InitialVersion}/package.{ext}";

        await _storage.PutAsync(packageKey, new MemoryStream(packageBytes), ct);

        // Step 7: persist
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
            ReadmeText: contents.ReadmeText);

        return await _repository.CreatePluginWithInitialVersionAsync(createCommand, ct);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private sealed record ParsedManifest(string Name, string Description, string Author);

    private static ParsedManifest ParseManifest(byte[] manifestBytes)
    {
        string json = Encoding.UTF8.GetString(manifestBytes);
        JsonDocument doc = JsonDocument.Parse(json);
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

    private static string GetExtension(string fileName)
    {
        if (fileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            return "tar.gz";
        if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return "zip";
        return "tar.gz";
    }

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
