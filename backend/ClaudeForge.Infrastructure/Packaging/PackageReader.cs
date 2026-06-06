using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using ClaudeForge.Core.Domain.Packaging;
using ClaudeForge.Core.Ports;

namespace ClaudeForge.Infrastructure.Packaging;

/// <summary>
/// BCL-based implementation of <see cref="IPackageReader"/>.
///
/// Format detection rules (applied to <c>fileName</c>):
///   ends with ".tar.gz"  → GZip-decompress then read as TAR (BCL System.Formats.Tar)
///   ends with ".zip"     → read as ZIP (BCL System.IO.Compression)
///   anything else        → <see cref="UnsupportedPackageFormatException"/>
///
/// Manifest detection:
///   Accepted names: "plugin.json" or "manifest.json"
///   MUST be at root level — entry name must not contain a path separator ('/' or '\').
///
/// README detection:
///   "README.md" at root level. Null when absent.
/// </summary>
public sealed class PackageReader : IPackageReader
{
    private static readonly HashSet<string> ManifestNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "plugin.json",
        "manifest.json",
    };

    private const string ReadmeName = "README.md";

    /// <inheritdoc />
    public async Task<PackageContents> ReadAsync(
        Stream archiveStream,
        string fileName,
        CancellationToken ct = default)
    {
        if (fileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            return await ReadTarGzAsync(archiveStream, ct);

        if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return await ReadZipAsync(archiveStream, ct);

        throw new UnsupportedPackageFormatException();
    }

    // -------------------------------------------------------------------------
    // tar.gz reader
    // -------------------------------------------------------------------------

    private static async Task<PackageContents> ReadTarGzAsync(Stream archiveStream, CancellationToken ct)
    {
        // Buffer everything first so we can handle non-seekable streams and detect corruption early.
        using MemoryStream buffered = new();
        await archiveStream.CopyToAsync(buffered, ct);
        buffered.Position = 0;

        if (buffered.Length == 0)
            throw new CorruptedArchiveException();

        byte[]? manifestBytes = null;
        string? readmeText = null;

        try
        {
            await using GZipStream gzip = new(buffered, CompressionMode.Decompress, leaveOpen: true);
            using TarReader tar = new(gzip, leaveOpen: true);

            while (true)
            {
                TarEntry? entry;
                try
                {
                    entry = await tar.GetNextEntryAsync(copyData: true, cancellationToken: ct);
                }
                catch (Exception ex)
                {
                    // Any exception while advancing the TAR reader means corruption.
                    throw new CorruptedArchiveException(ex);
                }

                if (entry is null)
                    break;

                // Only care about regular files (type == RegularFile or V7RegularFile).
                if (entry.EntryType is not TarEntryType.RegularFile and not TarEntryType.V7RegularFile)
                    continue;

                string name = NormalizeEntryName(entry.Name);

                if (IsRootLevel(name))
                {
                    string baseName = Path.GetFileName(name);

                    if (manifestBytes is null && ManifestNames.Contains(baseName) && entry.DataStream is not null)
                    {
                        using MemoryStream ms = new();
                        await entry.DataStream.CopyToAsync(ms, ct);
                        manifestBytes = ms.ToArray();
                    }
                    else if (readmeText is null &&
                             string.Equals(baseName, ReadmeName, StringComparison.OrdinalIgnoreCase) &&
                             entry.DataStream is not null)
                    {
                        using MemoryStream ms = new();
                        await entry.DataStream.CopyToAsync(ms, ct);
                        readmeText = Encoding.UTF8.GetString(ms.ToArray());
                    }
                }
            }
        }
        catch (CorruptedArchiveException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CorruptedArchiveException(ex);
        }

        if (manifestBytes is null)
            throw new MissingManifestException();

        return new PackageContents(manifestBytes, readmeText);
    }

    // -------------------------------------------------------------------------
    // zip reader
    // -------------------------------------------------------------------------

    private static async Task<PackageContents> ReadZipAsync(Stream archiveStream, CancellationToken ct)
    {
        using MemoryStream buffered = new();
        await archiveStream.CopyToAsync(buffered, ct);
        buffered.Position = 0;

        if (buffered.Length == 0)
            throw new CorruptedArchiveException();

        ZipArchive zip;
        try
        {
            zip = new ZipArchive(buffered, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (Exception ex)
        {
            throw new CorruptedArchiveException(ex);
        }

        using (zip)
        {
            byte[]? manifestBytes = null;
            string? readmeText = null;

            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                string name = NormalizeEntryName(entry.FullName);

                if (!IsRootLevel(name))
                    continue;

                string baseName = Path.GetFileName(name);

                if (manifestBytes is null && ManifestNames.Contains(baseName))
                {
                    try
                    {
                        await using Stream s = entry.Open();
                        using MemoryStream ms = new();
                        await s.CopyToAsync(ms, ct);
                        manifestBytes = StripUtf8Bom(ms.ToArray());
                    }
                    catch (Exception ex)
                    {
                        throw new CorruptedArchiveException(ex);
                    }
                }
                else if (readmeText is null &&
                         string.Equals(baseName, ReadmeName, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        await using Stream s = entry.Open();
                        using MemoryStream ms = new();
                        await s.CopyToAsync(ms, ct);
                        readmeText = Encoding.UTF8.GetString(StripUtf8Bom(ms.ToArray()));
                    }
                    catch (Exception ex)
                    {
                        throw new CorruptedArchiveException(ex);
                    }
                }
            }

            if (manifestBytes is null)
                throw new MissingManifestException();

            return new PackageContents(manifestBytes, readmeText);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Strips a UTF-8 BOM preamble (0xEF, 0xBB, 0xBF) from <paramref name="bytes"/> if present.
    /// Some zip creation tools (e.g. <c>StreamWriter(stream, Encoding.UTF8)</c>) write a BOM;
    /// manifest bytes should be BOM-free for consistent downstream processing.
    /// </summary>
    private static byte[] StripUtf8Bom(byte[] bytes)
    {
        ReadOnlySpan<byte> bom = [0xEF, 0xBB, 0xBF];
        return bytes.Length >= 3 && bytes.AsSpan(0, 3).SequenceEqual(bom)
            ? bytes[3..]
            : bytes;
    }

    /// <summary>
    /// Normalizes entry names to use forward slashes and strips leading './' prefixes.
    /// </summary>
    private static string NormalizeEntryName(string name)
    {
        // Normalize backslash to forward slash.
        name = name.Replace('\\', '/');

        // Strip leading "./" which some tools emit.
        if (name.StartsWith("./", StringComparison.Ordinal))
            name = name[2..];

        return name;
    }

    /// <summary>
    /// Returns <c>true</c> when the normalized entry name has no path separator,
    /// i.e. the file sits at the archive root.
    /// </summary>
    private static bool IsRootLevel(string normalizedName)
    {
        return !normalizedName.Contains('/');
    }
}
