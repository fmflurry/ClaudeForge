using Amazon.S3;
using ClaudeForge.Core.Ports;
using ClaudeForge.Infrastructure.Storage;
using ClaudeForge.Tests.Integration.Fixtures;
using System.Security.Cryptography;
using Testcontainers.Minio;

namespace ClaudeForge.Tests.Integration.Storage;

/// <summary>
/// Integration tests for <see cref="OvhObjectStorageAdapter"/> (Task 21.4).
///
/// These tests exercise the full <see cref="IPackageStoragePort"/> contract against a
/// real S3-compatible object store running in a MinIO Testcontainer. They mirror the
/// behaviour of <see cref="PackageStorageAdapterTests"/> (Group 3 / LocalFileSystem) so
/// that the two adapters are held to the same contract.
///
/// ─────────────────────────────────────────────────────────────────────────────────
/// Expected production types the coder MUST create (exact names, namespaces, members):
///
///   Namespace : ClaudeForge.Infrastructure.Storage
///   Class     : OvhObjectStorageAdapter
///   Implements: IPackageStoragePort
///
///   Constructor (used by these tests — this exact overload must exist):
///     OvhObjectStorageAdapter(IAmazonS3 s3Client, string bucketName)
///
///   The coder MAY also expose a second constructor for production DI wiring:
///     OvhObjectStorageAdapter(OvhStorageOptions options)
///   but the IAmazonS3 + bucketName overload is required so tests can inject the
///   Testcontainers-pointed client.
///
///   Note: OvhStorageOptions is tested separately in StorageOptionsValidatorTests.
///
/// Production package dependency (add to ClaudeForge.Infrastructure.csproj):
///   AWSSDK.S3  (latest stable 3.x — e.g. 3.7.x)
/// ─────────────────────────────────────────────────────────────────────────────────
/// </summary>
[Collection(MinioFixture.CollectionName)]
public sealed class OvhObjectStorageAdapterTests
{
    private readonly MinioFixture _fixture;

    public OvhObjectStorageAdapterTests(MinioFixture fixture)
    {
        _fixture = fixture;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helper: build a canonical package key
    // ─────────────────────────────────────────────────────────────────────────

    private static string MakeKey(Guid pluginId, string version, string ext = "tar.gz") =>
        $"plugins/{pluginId}/{version}/package.{ext}";

    // ─────────────────────────────────────────────────────────────────────────
    // Helper: compute expected SHA-256 for known bytes
    // ─────────────────────────────────────────────────────────────────────────

    private static string ComputeSha256Hex(byte[] data)
    {
        byte[] hash = SHA256.HashData(data);
        return Convert.ToHexStringLower(hash);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helper: create the adapter pointed at the MinIO fixture
    // ─────────────────────────────────────────────────────────────────────────

    private IPackageStoragePort CreateAdapter() =>
        new OvhObjectStorageAdapter(_fixture.S3Client, _fixture.BucketName);

    // ─────────────────────────────────────────────────────────────────────────
    // Test 1 — round-trip: PutAsync then GetAsync returns identical bytes
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PutAsync_ThenGetAsync_ReturnsSameBytes()
    {
        // Arrange
        IPackageStoragePort storage = CreateAdapter();
        Guid pluginId = Guid.NewGuid();
        string key = MakeKey(pluginId, "1.0.0");
        byte[] originalBytes = "Hello, OVH Object Storage!"u8.ToArray();

        // Act
        await storage.PutAsync(key, new MemoryStream(originalBytes));
        await using Stream result = await storage.GetAsync(key);
        using MemoryStream ms = new();
        await result.CopyToAsync(ms);

        // Assert
        Assert.Equal(originalBytes, ms.ToArray());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 2 — SHA-256 returned by GetMetadataAsync matches expected hash of content
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PutAsync_GetMetadataAsync_Sha256MatchesContentHash()
    {
        // Arrange
        IPackageStoragePort storage = CreateAdapter();
        Guid pluginId = Guid.NewGuid();
        string key = MakeKey(pluginId, "1.1.0");
        byte[] content = new byte[256];
        Random.Shared.NextBytes(content);
        string expectedSha256 = ComputeSha256Hex(content);

        // Act
        await storage.PutAsync(key, new MemoryStream(content));
        PackageMetadata meta = await storage.GetMetadataAsync(key);

        // Assert
        Assert.Equal(expectedSha256, meta.Sha256, StringComparer.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 3 — SizeBytes in metadata matches original byte count
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PutAsync_GetMetadataAsync_SizeBytesIsExact()
    {
        // Arrange
        IPackageStoragePort storage = CreateAdapter();
        Guid pluginId = Guid.NewGuid();
        string key = MakeKey(pluginId, "2.0.0");
        byte[] content = new byte[1_024];
        Random.Shared.NextBytes(content);

        // Act
        await storage.PutAsync(key, new MemoryStream(content));
        PackageMetadata meta = await storage.GetMetadataAsync(key);

        // Assert
        Assert.Equal(1_024L, meta.SizeBytes);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 4 — ExistsAsync returns false for unknown key
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExistsAsync_UnknownKey_ReturnsFalse()
    {
        // Arrange
        IPackageStoragePort storage = CreateAdapter();
        string key = MakeKey(Guid.NewGuid(), "9.9.9");

        // Act
        bool exists = await storage.ExistsAsync(key);

        // Assert
        Assert.False(exists);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 5 — ExistsAsync returns true after PutAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExistsAsync_AfterPut_ReturnsTrue()
    {
        // Arrange
        IPackageStoragePort storage = CreateAdapter();
        Guid pluginId = Guid.NewGuid();
        string key = MakeKey(pluginId, "1.0.0");
        byte[] content = "exists-check-ovh"u8.ToArray();

        // Act
        await storage.PutAsync(key, new MemoryStream(content));
        bool exists = await storage.ExistsAsync(key);

        // Assert
        Assert.True(exists);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 6 — Immutability: PutAsync on existing key throws PackageAlreadyExistsException
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PutAsync_ExistingKey_ThrowsPackageAlreadyExistsException()
    {
        // Arrange
        IPackageStoragePort storage = CreateAdapter();
        Guid pluginId = Guid.NewGuid();
        string key = MakeKey(pluginId, "1.0.0");
        byte[] first = "first-content-s3"u8.ToArray();
        byte[] second = "overwrite-attempt-s3"u8.ToArray();

        await storage.PutAsync(key, new MemoryStream(first));

        // Act & Assert
        await Assert.ThrowsAsync<PackageAlreadyExistsException>(
            () => storage.PutAsync(key, new MemoryStream(second)));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 7 — After failed overwrite attempt, original content is unchanged
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PutAsync_OverwriteAttempt_OriginalContentPreserved()
    {
        // Arrange
        IPackageStoragePort storage = CreateAdapter();
        Guid pluginId = Guid.NewGuid();
        string key = MakeKey(pluginId, "1.0.0");
        byte[] original = "original-data-s3"u8.ToArray();
        byte[] overwrite = "overwrite-data-s3"u8.ToArray();

        await storage.PutAsync(key, new MemoryStream(original));

        // Act — overwrite attempt (ignore the exception)
        try
        {
            await storage.PutAsync(key, new MemoryStream(overwrite));
        }
        catch (PackageAlreadyExistsException) { /* expected */ }

        // Assert — content must still be the original
        await using Stream result = await storage.GetAsync(key);
        using MemoryStream ms = new();
        await result.CopyToAsync(ms);
        Assert.Equal(original, ms.ToArray());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 8 — Key convention: "plugins/{id}/{ver}/package.tar.gz" is accepted
    // (S3 key with forward slashes simulating path-style nested objects)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PutAsync_KeyWithNestedPathSegments_Succeeds()
    {
        // Arrange
        IPackageStoragePort storage = CreateAdapter();
        Guid pluginId = Guid.NewGuid();
        string key = $"plugins/{pluginId}/1.2.3/package.tar.gz";
        byte[] content = "nested-path-s3-test"u8.ToArray();

        // Act & Assert — must not throw
        await storage.PutAsync(key, new MemoryStream(content));
        bool exists = await storage.ExistsAsync(key);
        Assert.True(exists, "Package stored at nested key path must be found by ExistsAsync");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 9 — zip extension is also a valid key format
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PutAsync_ZipExtensionKey_Succeeds()
    {
        // Arrange
        IPackageStoragePort storage = CreateAdapter();
        Guid pluginId = Guid.NewGuid();
        string key = MakeKey(pluginId, "1.0.0", "zip");
        byte[] content = "zip-content-ovh"u8.ToArray();

        // Act
        await storage.PutAsync(key, new MemoryStream(content));

        // Assert
        bool exists = await storage.ExistsAsync(key);
        Assert.True(exists);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 10 — Two different plugins with the same version coexist independently
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PutAsync_SameVersionDifferentPlugins_BothStoredIndependently()
    {
        // Arrange
        IPackageStoragePort storage = CreateAdapter();
        Guid pluginA = Guid.NewGuid();
        Guid pluginB = Guid.NewGuid();
        string keyA = MakeKey(pluginA, "1.0.0");
        string keyB = MakeKey(pluginB, "1.0.0");
        byte[] contentA = "plugin-a-ovh"u8.ToArray();
        byte[] contentB = "plugin-b-ovh"u8.ToArray();

        // Act
        await storage.PutAsync(keyA, new MemoryStream(contentA));
        await storage.PutAsync(keyB, new MemoryStream(contentB));

        // Assert — both exist, contents distinct
        Assert.True(await storage.ExistsAsync(keyA));
        Assert.True(await storage.ExistsAsync(keyB));

        await using Stream streamA = await storage.GetAsync(keyA);
        using MemoryStream msA = new();
        await streamA.CopyToAsync(msA);
        Assert.Equal(contentA, msA.ToArray());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 11 — SHA-256 is exactly 64 lowercase hex characters
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMetadataAsync_Sha256Is64HexChars()
    {
        // Arrange
        IPackageStoragePort storage = CreateAdapter();
        Guid pluginId = Guid.NewGuid();
        string key = MakeKey(pluginId, "3.0.0");
        byte[] content = "sha256-length-check-ovh"u8.ToArray();

        // Act
        await storage.PutAsync(key, new MemoryStream(content));
        PackageMetadata meta = await storage.GetMetadataAsync(key);

        // Assert
        Assert.Equal(64, meta.Sha256.Length);
        Assert.Matches("^[0-9a-f]{64}$", meta.Sha256);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 12 — Empty content is accepted and round-trips correctly
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PutAsync_EmptyContent_StoredAndRetrievedAsEmpty()
    {
        // Arrange
        IPackageStoragePort storage = CreateAdapter();
        Guid pluginId = Guid.NewGuid();
        string key = MakeKey(pluginId, "0.0.1");
        byte[] empty = Array.Empty<byte>();

        // Act
        await storage.PutAsync(key, new MemoryStream(empty));
        await using Stream result = await storage.GetAsync(key);
        using MemoryStream ms = new();
        await result.CopyToAsync(ms);

        // Assert
        Assert.Empty(ms.ToArray());
        PackageMetadata meta = await storage.GetMetadataAsync(key);
        Assert.Equal(0L, meta.SizeBytes);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 13 — Large payload (100 KB) round-trips correctly (no streaming truncation)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PutAsync_LargePayload_RoundTripsWithoutTruncation()
    {
        // Arrange
        IPackageStoragePort storage = CreateAdapter();
        Guid pluginId = Guid.NewGuid();
        string key = MakeKey(pluginId, "5.0.0");
        byte[] content = new byte[100_000];
        Random.Shared.NextBytes(content);

        // Act
        await storage.PutAsync(key, new MemoryStream(content));
        await using Stream result = await storage.GetAsync(key);
        using MemoryStream ms = new();
        await result.CopyToAsync(ms);

        // Assert — same bytes and correct size metadata
        Assert.Equal(content, ms.ToArray());
        PackageMetadata meta = await storage.GetMetadataAsync(key);
        Assert.Equal(100_000L, meta.SizeBytes);
    }
}
