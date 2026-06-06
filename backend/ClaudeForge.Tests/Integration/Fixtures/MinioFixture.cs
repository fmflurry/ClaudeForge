using Amazon.Runtime;
using Amazon.S3;
using Testcontainers.Minio;

namespace ClaudeForge.Tests.Integration.Fixtures;

/// <summary>
/// xUnit shared fixture that starts a MinIO container once per collection, creates the test
/// bucket, and tears down afterwards.
///
/// MinIO is S3-compatible, so the <see cref="IAmazonS3"/> client pointed at it exercises
/// exactly the same AWS SDK code paths that OvhObjectStorageAdapter uses in production.
///
/// Tests in <see cref="OvhObjectStorageAdapterTests"/> reference this fixture via
/// <c>[Collection(MinioFixture.CollectionName)]</c>.
///
/// The bucket name exposed via <see cref="BucketName"/> is what the tests inject into the
/// adapter; the coder must create the bucket in fixture setup before tests start.
/// </summary>
[CollectionDefinition(MinioFixture.CollectionName)]
public sealed class MinioCollection : ICollectionFixture<MinioFixture> { }

public sealed class MinioFixture : IAsyncLifetime
{
    public const string CollectionName = "Minio";

    // Constant credentials used by both the container config and the IAmazonS3 client.
    // These are test-only values; they never leave the dev machine.
    private const string RootUser = "minioadmin";
    private const string RootPassword = "minioadmin";

    /// <summary>Name of the bucket created during <see cref="InitializeAsync"/>.</summary>
    public string BucketName { get; } = "test-claude-plugins";

    private readonly MinioContainer _container = new MinioBuilder()
        .WithUsername(RootUser)
        .WithPassword(RootPassword)
        .Build();

    /// <summary>
    /// Pre-configured S3 client pointing at the running MinIO container.
    /// Injected into <see cref="OvhObjectStorageAdapter"/> by the tests.
    /// </summary>
    public IAmazonS3 S3Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        AmazonS3Config config = new()
        {
            ServiceURL = _container.GetConnectionString(),
            ForcePathStyle = true,   // MinIO requires path-style addressing
            AuthenticationRegion = "us-east-1"
        };

        S3Client = new AmazonS3Client(
            new BasicAWSCredentials(RootUser, RootPassword),
            config);

        // Create the bucket that all tests will share.
        // OvhObjectStorageAdapter itself does NOT create buckets (infra concern handled outside adapter).
        await S3Client.PutBucketAsync(BucketName);
    }

    public async Task DisposeAsync()
    {
        S3Client?.Dispose();
        await _container.DisposeAsync();
    }
}
