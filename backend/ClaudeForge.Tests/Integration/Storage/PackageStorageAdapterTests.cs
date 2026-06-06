using ClaudeForge.Core.Ports;
using ClaudeForge.Infrastructure.Storage;
using System.Security.Cryptography;

namespace ClaudeForge.Tests.Integration.Storage;

/// <summary>
/// Integration tests for IPackageStoragePort contract fulfilled by
/// LocalFileSystemPackageStorageAdapter.
///
/// Expected production types (coder must match these names exactly):
///
///   ClaudeForge.Core.Ports.IPackageStoragePort
///     Task PutAsync(string key, Stream content, CancellationToken ct = default)
///       → throws PackageAlreadyExistsException if key already stored (immutability)
///
///     Task&lt;Stream&gt; GetAsync(string key, CancellationToken ct = default)
///
///     Task&lt;bool&gt; ExistsAsync(string key, CancellationToken ct = default)
///
///     Task&lt;PackageMetadata&gt; GetMetadataAsync(string key, CancellationToken ct = default)
///       → returns sha256 (hex string, 64 chars) + sizeBytes
///
///   ClaudeForge.Core.Ports.PackageMetadata
///     string Sha256    — lower-hex SHA-256 of the stored bytes
///     long   SizeBytes — exact byte count
///
///   ClaudeForge.Core.Ports.PackageAlreadyExistsException : Exception
///     (no special members required beyond Message)
///
///   ClaudeForge.Infrastructure.Storage.LocalFileSystemPackageStorageAdapter
///     LocalFileSystemPackageStorageAdapter(string rootPath)
///     implements IPackageStoragePort
///
///   Key convention tested: "plugins/{pluginId}/{version}/package.{ext}"
/// </summary>
public sealed class PackageStorageAdapterTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly IPackageStoragePort _storage;

    public PackageStorageAdapterTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"cftest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
        _storage = new LocalFileSystemPackageStorageAdapter(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    // -------------------------------------------------------------------------
    // Helper: build a canonical package key
    // -------------------------------------------------------------------------

    private static string MakeKey(Guid pluginId, string version, string ext = "tar.gz") =>
        $"plugins/{pluginId}/{version}/package.{ext}";

    // -------------------------------------------------------------------------
    // Helper: compute expected SHA-256 for known bytes
    // -------------------------------------------------------------------------

    private static string ComputeSha256Hex(byte[] data)
    {
        byte[] hash = SHA256.HashData(data);
        return Convert.ToHexStringLower(hash);
    }

    // -------------------------------------------------------------------------
    // Test 1 — round-trip: PutAsync then GetAsync returns identical bytes
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PutAsync_ThenGetAsync_ReturnsSameBytes()
    {
        // Arrange
        Guid pluginId = Guid.NewGuid();
        string key = MakeKey(pluginId, "1.0.0");
        byte[] originalBytes = "Hello, ClaudeForge!"u8.ToArray();

        // Act
        await _storage.PutAsync(key, new MemoryStream(originalBytes));
        await using Stream result = await _storage.GetAsync(key);
        using MemoryStream resultBytes = new();
        await result.CopyToAsync(resultBytes);

        // Assert
        Assert.Equal(originalBytes, resultBytes.ToArray());
    }

    // -------------------------------------------------------------------------
    // Test 2 — SHA-256 returned by GetMetadataAsync matches expected hash of content
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PutAsync_GetMetadataAsync_Sha256MatchesContentHash()
    {
        // Arrange
        Guid pluginId = Guid.NewGuid();
        string key = MakeKey(pluginId, "1.1.0");
        byte[] content = new byte[256];
        Random.Shared.NextBytes(content);
        string expectedSha256 = ComputeSha256Hex(content);

        // Act
        await _storage.PutAsync(key, new MemoryStream(content));
        PackageMetadata meta = await _storage.GetMetadataAsync(key);

        // Assert
        Assert.Equal(expectedSha256, meta.Sha256, StringComparer.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------
    // Test 3 — SizeBytes in metadata matches original byte count
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PutAsync_GetMetadataAsync_SizeBytesIsExact()
    {
        // Arrange
        Guid pluginId = Guid.NewGuid();
        string key = MakeKey(pluginId, "2.0.0");
        byte[] content = new byte[1_024];
        Random.Shared.NextBytes(content);

        // Act
        await _storage.PutAsync(key, new MemoryStream(content));
        PackageMetadata meta = await _storage.GetMetadataAsync(key);

        // Assert
        Assert.Equal(1_024L, meta.SizeBytes);
    }

    // -------------------------------------------------------------------------
    // Test 4 — ExistsAsync returns false for unknown key
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExistsAsync_UnknownKey_ReturnsFalse()
    {
        // Arrange
        string key = MakeKey(Guid.NewGuid(), "1.0.0");

        // Act
        bool exists = await _storage.ExistsAsync(key);

        // Assert
        Assert.False(exists);
    }

    // -------------------------------------------------------------------------
    // Test 5 — ExistsAsync returns true after PutAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExistsAsync_AfterPut_ReturnsTrue()
    {
        // Arrange
        Guid pluginId = Guid.NewGuid();
        string key = MakeKey(pluginId, "1.0.0");
        byte[] content = "exists-check"u8.ToArray();

        // Act
        await _storage.PutAsync(key, new MemoryStream(content));
        bool exists = await _storage.ExistsAsync(key);

        // Assert
        Assert.True(exists);
    }

    // -------------------------------------------------------------------------
    // Test 6 — Immutability: PutAsync on existing key throws PackageAlreadyExistsException
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PutAsync_ExistingKey_ThrowsPackageAlreadyExistsException()
    {
        // Arrange
        Guid pluginId = Guid.NewGuid();
        string key = MakeKey(pluginId, "1.0.0");
        byte[] first = "first-content"u8.ToArray();
        byte[] second = "overwrite-attempt"u8.ToArray();

        await _storage.PutAsync(key, new MemoryStream(first));

        // Act & Assert
        await Assert.ThrowsAsync<PackageAlreadyExistsException>(
            () => _storage.PutAsync(key, new MemoryStream(second)));
    }

    // -------------------------------------------------------------------------
    // Test 7 — After failed overwrite attempt, original content is unchanged
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PutAsync_OverwriteAttempt_OriginalContentPreserved()
    {
        // Arrange
        Guid pluginId = Guid.NewGuid();
        string key = MakeKey(pluginId, "1.0.0");
        byte[] original = "original-data"u8.ToArray();
        byte[] overwrite = "overwrite-data"u8.ToArray();

        await _storage.PutAsync(key, new MemoryStream(original));

        // Act — overwrite attempt (ignore the exception)
        try
        {
            await _storage.PutAsync(key, new MemoryStream(overwrite));
        }
        catch (PackageAlreadyExistsException) { /* expected */ }

        // Assert — content must still be the original
        await using Stream result = await _storage.GetAsync(key);
        using MemoryStream resultBytes = new();
        await result.CopyToAsync(resultBytes);
        Assert.Equal(original, resultBytes.ToArray());
    }

    // -------------------------------------------------------------------------
    // Test 8 — Key convention: path separator chars in key map to nested dirs
    // (i.e., "plugins/{id}/{ver}/package.tar.gz" is accepted without error)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PutAsync_KeyWithNestedPathSegments_Succeeds()
    {
        // Arrange
        Guid pluginId = Guid.NewGuid();
        string key = $"plugins/{pluginId}/1.2.3/package.tar.gz";
        byte[] content = "nested-path-test"u8.ToArray();

        // Act & Assert — must not throw
        await _storage.PutAsync(key, new MemoryStream(content));
        bool exists = await _storage.ExistsAsync(key);
        Assert.True(exists, "Package stored at nested key path must be found by ExistsAsync");
    }

    // -------------------------------------------------------------------------
    // Test 9 — zip extension is also a valid key format
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PutAsync_ZipExtensionKey_Succeeds()
    {
        // Arrange
        Guid pluginId = Guid.NewGuid();
        string key = MakeKey(pluginId, "1.0.0", "zip");
        byte[] content = "zip-content"u8.ToArray();

        // Act
        await _storage.PutAsync(key, new MemoryStream(content));

        // Assert
        bool exists = await _storage.ExistsAsync(key);
        Assert.True(exists);
    }

    // -------------------------------------------------------------------------
    // Test 10 — Two different plugins with the same version can coexist
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PutAsync_SameVersionDifferentPlugins_BothStoredIndependently()
    {
        // Arrange
        Guid pluginA = Guid.NewGuid();
        Guid pluginB = Guid.NewGuid();
        string keyA = MakeKey(pluginA, "1.0.0");
        string keyB = MakeKey(pluginB, "1.0.0");
        byte[] contentA = "plugin-a"u8.ToArray();
        byte[] contentB = "plugin-b"u8.ToArray();

        // Act
        await _storage.PutAsync(keyA, new MemoryStream(contentA));
        await _storage.PutAsync(keyB, new MemoryStream(contentB));

        // Assert — both exist, contents distinct
        Assert.True(await _storage.ExistsAsync(keyA));
        Assert.True(await _storage.ExistsAsync(keyB));

        await using Stream streamA = await _storage.GetAsync(keyA);
        using MemoryStream msA = new();
        await streamA.CopyToAsync(msA);
        Assert.Equal(contentA, msA.ToArray());
    }

    // -------------------------------------------------------------------------
    // Test 11 — SHA-256 is exactly 64 hex characters
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetMetadataAsync_Sha256Is64HexChars()
    {
        // Arrange
        Guid pluginId = Guid.NewGuid();
        string key = MakeKey(pluginId, "3.0.0");
        byte[] content = "sha256-length-check"u8.ToArray();

        // Act
        await _storage.PutAsync(key, new MemoryStream(content));
        PackageMetadata meta = await _storage.GetMetadataAsync(key);

        // Assert
        Assert.Equal(64, meta.Sha256.Length);
        Assert.Matches("^[0-9a-f]{64}$", meta.Sha256);
    }

    // -------------------------------------------------------------------------
    // Test 12 — Empty content is accepted and round-trips correctly
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PutAsync_EmptyContent_StoredAndRetrievedAsEmpty()
    {
        // Arrange
        Guid pluginId = Guid.NewGuid();
        string key = MakeKey(pluginId, "0.0.1");
        byte[] empty = Array.Empty<byte>();

        // Act
        await _storage.PutAsync(key, new MemoryStream(empty));
        await using Stream result = await _storage.GetAsync(key);
        using MemoryStream ms = new();
        await result.CopyToAsync(ms);

        // Assert
        Assert.Empty(ms.ToArray());
        PackageMetadata meta = await _storage.GetMetadataAsync(key);
        Assert.Equal(0L, meta.SizeBytes);
    }
}
