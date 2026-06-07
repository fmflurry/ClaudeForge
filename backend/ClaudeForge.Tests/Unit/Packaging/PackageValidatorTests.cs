using System.Formats.Tar;
using System.IO.Compression;
using ClaudeForge.Core.Domain.Packaging;
using ClaudeForge.Core.Ports;
using ClaudeForge.Infrastructure.Packaging;

namespace ClaudeForge.Tests.Unit.Packaging;

/// <summary>
/// Unit tests for the package format validator and archive reader (tasks 3.4/3.5).
///
/// Expected production types (coder must match these names exactly):
///
///   ClaudeForge.Infrastructure.Packaging.IPackageReader
///     Task&lt;PackageContents&gt; ReadAsync(Stream archiveStream, string fileName, CancellationToken ct = default)
///       → fileName is used to detect the format (must end with ".tar.gz" or ".zip";
///         anything else → UnsupportedFormatException)
///
///   ClaudeForge.Infrastructure.Packaging.PackageContents
///     byte[]  ManifestBytes    — raw bytes of plugin.json or manifest.json
///     string? ReadmeText       — text of README.md if present, null if absent
///
///   Exceptions (all in ClaudeForge.Infrastructure.Packaging):
///
///   ClaudeForge.Infrastructure.Packaging.UnsupportedPackageFormatException : Exception
///     Message == "Unsupported package format. Allowed: tar.gz, zip"
///
///   ClaudeForge.Infrastructure.Packaging.CorruptedArchiveException : Exception
///     Message == "Package file is corrupted or not a valid archive"
///
///   ClaudeForge.Infrastructure.Packaging.MissingManifestException : Exception
///     Message == "Package must contain plugin.json or manifest.json at root level"
///
///   ClaudeForge.Infrastructure.Packaging.PackageReader : IPackageReader
///     (parameterless constructor)
///
/// Spec source (verbatim strings from plugin-upload/spec.md):
///   "Unsupported package format. Allowed: tar.gz, zip"
///   "Package file is corrupted or not a valid archive"
///   "Package must contain plugin.json or manifest.json at root level"
/// </summary>
public sealed class PackageValidatorTests
{
    private readonly IPackageReader _reader = new PackageReader();

    // =========================================================================
    // Archive builders — create REAL in-memory archives for test scenarios
    // =========================================================================

    /// <summary>Builds a tar.gz archive in memory with the given entries.</summary>
    private static MemoryStream BuildTarGz(IEnumerable<(string name, string content)> entries)
    {
        MemoryStream output = new();

        using (GZipStream gzip = new(output, CompressionMode.Compress, leaveOpen: true))
        using (TarWriter tar = new(gzip, TarEntryFormat.Pax, leaveOpen: false))
        {
            foreach ((string name, string content) in entries)
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(content);
                PaxTarEntry entry = new(TarEntryType.RegularFile, name)
                {
                    DataStream = new MemoryStream(bytes),
                };
                tar.WriteEntry(entry);
            }
        } // TarWriter.Dispose() finalizes the tar blocks; GZipStream.Dispose() flushes gzip

        output.Position = 0;
        return output;
    }

    /// <summary>Builds a zip archive in memory with the given entries.</summary>
    private static MemoryStream BuildZip(IEnumerable<(string name, string content)> entries)
    {
        MemoryStream output = new();

        using (ZipArchive zip = new(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, string content) in entries)
            {
                ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.Fastest);
                using StreamWriter writer = new(entry.Open(), System.Text.Encoding.UTF8);
                writer.Write(content);
            }
        }

        output.Position = 0;
        return output;
    }

    // =========================================================================
    // Group A — tar.gz happy paths
    // =========================================================================

    [Fact]
    public async Task ReadAsync_TarGz_ContainsPluginJson_ReturnsManifestBytes()
    {
        // Arrange
        const string manifestJson = """{"name":"my-plugin","version":"1.0.0"}""";
        MemoryStream archive = BuildTarGz([("plugin.json", manifestJson)]);

        // Act
        PackageContents result = await _reader.ReadAsync(archive, "my-plugin-1.0.0.tar.gz");

        // Assert
        Assert.NotNull(result.ManifestBytes);
        Assert.NotEmpty(result.ManifestBytes);
        string extracted = System.Text.Encoding.UTF8.GetString(result.ManifestBytes);
        Assert.Equal(manifestJson, extracted);
    }

    [Fact]
    public async Task ReadAsync_TarGz_WithReadme_ExtractsReadmeText()
    {
        // Arrange
        const string manifestJson = """{"name":"readme-plugin","version":"1.0.0"}""";
        const string readmeContent = "# My Plugin\n\nThis is the README.";
        MemoryStream archive = BuildTarGz(
        [
            ("plugin.json", manifestJson),
            ("README.md", readmeContent),
        ]);

        // Act
        PackageContents result = await _reader.ReadAsync(archive, "package.tar.gz");

        // Assert
        Assert.NotNull(result.ReadmeText);
        Assert.Equal(readmeContent, result.ReadmeText);
    }

    [Fact]
    public async Task ReadAsync_TarGz_WithoutReadme_ReadmeTextIsNull()
    {
        // Arrange
        MemoryStream archive = BuildTarGz([("plugin.json", "{}")]);

        // Act
        PackageContents result = await _reader.ReadAsync(archive, "package.tar.gz");

        // Assert
        Assert.Null(result.ReadmeText);
    }

    // =========================================================================
    // Group B — zip happy paths
    // =========================================================================

    [Fact]
    public async Task ReadAsync_Zip_ContainsPluginJson_ReturnsManifestBytes()
    {
        // Arrange
        const string manifestJson = """{"name":"zip-plugin","version":"2.0.0"}""";
        MemoryStream archive = BuildZip([("plugin.json", manifestJson)]);

        // Act
        PackageContents result = await _reader.ReadAsync(archive, "my-plugin.zip");

        // Assert
        string extracted = System.Text.Encoding.UTF8.GetString(result.ManifestBytes);
        Assert.Equal(manifestJson, extracted);
    }

    [Fact]
    public async Task ReadAsync_Zip_ContainsManifestJson_BothNamesAccepted()
    {
        // Arrange — "manifest.json" is an accepted alternative name to "plugin.json"
        const string manifestJson = """{"name":"alt-manifest","version":"1.0.0"}""";
        MemoryStream archive = BuildZip([("manifest.json", manifestJson)]);

        // Act
        PackageContents result = await _reader.ReadAsync(archive, "package.zip");

        // Assert
        string extracted = System.Text.Encoding.UTF8.GetString(result.ManifestBytes);
        Assert.Equal(manifestJson, extracted);
    }

    [Fact]
    public async Task ReadAsync_Zip_WithReadme_ExtractsReadmeText()
    {
        // Arrange
        const string readmeContent = "# Zip Plugin\n\nDocumentation here.";
        MemoryStream archive = BuildZip(
        [
            ("manifest.json", "{}"),
            ("README.md", readmeContent),
        ]);

        // Act
        PackageContents result = await _reader.ReadAsync(archive, "package.zip");

        // Assert
        Assert.Equal(readmeContent, result.ReadmeText);
    }

    // =========================================================================
    // Group C — spec-exact error: missing manifest at root
    // =========================================================================

    [Fact]
    public async Task ReadAsync_TarGz_MissingManifest_ThrowsMissingManifestException()
    {
        // Arrange — archive exists and is valid, but contains no plugin.json / manifest.json
        MemoryStream archive = BuildTarGz([("README.md", "# No Manifest"), ("src/index.ts", "export {}")]);

        // Act & Assert
        MissingManifestException ex = await Assert.ThrowsAsync<MissingManifestException>(
            () => _reader.ReadAsync(archive, "no-manifest.tar.gz"));

        Assert.Equal("Package must contain plugin.json or manifest.json at root level", ex.Message);
    }

    [Fact]
    public async Task ReadAsync_Zip_MissingManifest_ThrowsMissingManifestException()
    {
        // Arrange
        MemoryStream archive = BuildZip([("src/index.ts", "export {}")]);

        // Act & Assert
        MissingManifestException ex = await Assert.ThrowsAsync<MissingManifestException>(
            () => _reader.ReadAsync(archive, "no-manifest.zip"));

        Assert.Equal("Package must contain plugin.json or manifest.json at root level", ex.Message);
    }

    [Fact]
    public async Task ReadAsync_TarGz_ManifestInSubdirectory_IsNotAccepted()
    {
        // Arrange — plugin.json is NOT at root level; it's nested under a subdirectory
        MemoryStream archive = BuildTarGz([("subdir/plugin.json", "{}")]);

        // Act & Assert — nested manifest does not satisfy "at root level" requirement
        await Assert.ThrowsAsync<MissingManifestException>(
            () => _reader.ReadAsync(archive, "nested.tar.gz"));
    }

    // =========================================================================
    // Group D — spec-exact error: corrupted / invalid archive
    // =========================================================================

    [Fact]
    public async Task ReadAsync_GarbageBytes_TarGzFileName_ThrowsCorruptedArchiveException()
    {
        // Arrange — random garbage bytes with a .tar.gz filename
        byte[] garbage = new byte[128];
        Random.Shared.NextBytes(garbage);
        MemoryStream stream = new(garbage);

        // Act & Assert
        CorruptedArchiveException ex = await Assert.ThrowsAsync<CorruptedArchiveException>(
            () => _reader.ReadAsync(stream, "corrupted.tar.gz"));

        Assert.Equal("Package file is corrupted or not a valid archive", ex.Message);
    }

    [Fact]
    public async Task ReadAsync_GarbageBytes_ZipFileName_ThrowsCorruptedArchiveException()
    {
        // Arrange
        byte[] garbage = new byte[128];
        Random.Shared.NextBytes(garbage);
        MemoryStream stream = new(garbage);

        // Act & Assert
        CorruptedArchiveException ex = await Assert.ThrowsAsync<CorruptedArchiveException>(
            () => _reader.ReadAsync(stream, "corrupted.zip"));

        Assert.Equal("Package file is corrupted or not a valid archive", ex.Message);
    }

    [Fact]
    public async Task ReadAsync_EmptyStream_TarGzFileName_ThrowsCorruptedArchiveException()
    {
        // Arrange — empty stream is not a valid archive
        MemoryStream empty = new();

        // Act & Assert
        await Assert.ThrowsAsync<CorruptedArchiveException>(
            () => _reader.ReadAsync(empty, "empty.tar.gz"));
    }

    [Fact]
    public async Task ReadAsync_TruncatedGzip_ThrowsCorruptedArchiveException()
    {
        // Arrange — valid gzip magic bytes but truncated body
        byte[] truncated = [0x1f, 0x8b, 0x08, 0x00]; // gzip header only
        MemoryStream stream = new(truncated);

        // Act & Assert
        await Assert.ThrowsAsync<CorruptedArchiveException>(
            () => _reader.ReadAsync(stream, "truncated.tar.gz"));
    }

    // =========================================================================
    // Group E — spec-exact error: unsupported format
    // =========================================================================

    [Fact]
    public async Task ReadAsync_RarExtension_ThrowsUnsupportedPackageFormatException()
    {
        // Arrange
        MemoryStream stream = new("not-a-valid-format"u8.ToArray());

        // Act & Assert
        UnsupportedPackageFormatException ex = await Assert.ThrowsAsync<UnsupportedPackageFormatException>(
            () => _reader.ReadAsync(stream, "plugin.rar"));

        Assert.Equal("Unsupported package format. Allowed: tar.gz, zip", ex.Message);
    }

    [Fact]
    public async Task ReadAsync_SevenZipExtension_ThrowsUnsupportedPackageFormatException()
    {
        // Arrange
        MemoryStream stream = new("content"u8.ToArray());

        // Act & Assert
        UnsupportedPackageFormatException ex = await Assert.ThrowsAsync<UnsupportedPackageFormatException>(
            () => _reader.ReadAsync(stream, "archive.7z"));

        Assert.Equal("Unsupported package format. Allowed: tar.gz, zip", ex.Message);
    }

    [Fact]
    public async Task ReadAsync_ExeExtension_ThrowsUnsupportedPackageFormatException()
    {
        // Arrange
        MemoryStream stream = new("content"u8.ToArray());

        // Act & Assert
        UnsupportedPackageFormatException ex = await Assert.ThrowsAsync<UnsupportedPackageFormatException>(
            () => _reader.ReadAsync(stream, "installer.exe"));

        Assert.Equal("Unsupported package format. Allowed: tar.gz, zip", ex.Message);
    }

    [Fact]
    public async Task ReadAsync_PlainTarWithoutGzExtension_ThrowsUnsupportedPackageFormatException()
    {
        // Arrange — ".tar" alone is not in the allowed set (only "tar.gz")
        MemoryStream stream = new("content"u8.ToArray());

        // Act & Assert
        await Assert.ThrowsAsync<UnsupportedPackageFormatException>(
            () => _reader.ReadAsync(stream, "archive.tar"));
    }

    [Fact]
    public async Task ReadAsync_NullOrEmptyFileName_ThrowsUnsupportedPackageFormatException()
    {
        // Arrange
        MemoryStream stream = new("content"u8.ToArray());

        // Act & Assert — no extension = unsupported
        await Assert.ThrowsAsync<UnsupportedPackageFormatException>(
            () => _reader.ReadAsync(stream, "noextension"));
    }

    // =========================================================================
    // Group F — content correctness edge cases
    // =========================================================================

    [Fact]
    public async Task ReadAsync_TarGz_ManifestWithUnicodeContent_RoundTripsCorrectly()
    {
        // Arrange — manifest containing unicode/emoji characters
        const string manifest = """{"name":"emoji-🔌","description":"Ünïcödé plugin"}""";
        MemoryStream archive = BuildTarGz([("plugin.json", manifest)]);

        // Act
        PackageContents result = await _reader.ReadAsync(archive, "unicode.tar.gz");

        // Assert
        string extracted = System.Text.Encoding.UTF8.GetString(result.ManifestBytes);
        Assert.Equal(manifest, extracted);
    }

    [Fact]
    public async Task ReadAsync_Zip_MultipleFilesAtRoot_OnlyManifestAndReadmeExtracted()
    {
        // Arrange — archive has many files; only plugin.json and README.md matter
        const string manifest = """{"name":"multi-file","version":"1.0.0"}""";
        const string readme = "# Multi File Plugin";
        MemoryStream archive = BuildZip(
        [
            ("plugin.json", manifest),
            ("README.md", readme),
            ("src/index.ts", "export {}"),
            ("docs/api.md", "# API"),
            ("assets/icon.png", "PNG_DATA"),
        ]);

        // Act
        PackageContents result = await _reader.ReadAsync(archive, "multi.zip");

        // Assert
        Assert.Equal(manifest, System.Text.Encoding.UTF8.GetString(result.ManifestBytes));
        Assert.Equal(readme, result.ReadmeText);
    }
}
