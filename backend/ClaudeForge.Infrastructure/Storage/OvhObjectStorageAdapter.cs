using System.Security.Cryptography;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using ClaudeForge.Core.Ports;

namespace ClaudeForge.Infrastructure.Storage;

/// <summary>
/// Production implementation of <see cref="IPackageStoragePort"/> backed by an S3-compatible
/// object store (OVH Object Storage, MinIO, AWS S3, etc.).
///
/// Packages are immutable: a second <see cref="PutAsync"/> call for the same key throws
/// <see cref="PackageAlreadyExistsException"/>.
///
/// SHA-256 in <see cref="GetMetadataAsync"/> is computed by streaming the object — S3's
/// native ETag is MD5 and cannot be used as a SHA-256 source.
/// </summary>
public sealed class OvhObjectStorageAdapter : IPackageStoragePort
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;

    /// <summary>
    /// Primary constructor used by integration tests (inject a pre-configured client).
    /// </summary>
    public OvhObjectStorageAdapter(IAmazonS3 s3Client, string bucketName)
    {
        ArgumentNullException.ThrowIfNull(s3Client);
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
        _s3Client = s3Client;
        _bucketName = bucketName;
    }

    /// <summary>
    /// Secondary constructor for production DI wiring — builds an <see cref="AmazonS3Client"/>
    /// from the supplied <see cref="OvhStorageOptions"/>.
    /// </summary>
    public OvhObjectStorageAdapter(OvhStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        AmazonS3Config config = new()
        {
            ServiceURL = options.Endpoint,
            ForcePathStyle = true,
            AuthenticationRegion = "us-east-1"
        };

        _s3Client = new AmazonS3Client(
            new BasicAWSCredentials(options.AccessKey, options.SecretKey),
            config);

        _bucketName = options.BucketName;
    }

    /// <inheritdoc />
    public async Task PutAsync(string key, Stream content, CancellationToken ct = default)
    {
        bool exists = await ExistsAsync(key, ct).ConfigureAwait(false);
        if (exists)
            throw new PackageAlreadyExistsException(key);

        // Buffer the entire content into a MemoryStream so the AWS SDK can compute the
        // x-amz-content-sha256 payload hash correctly over a seekable, length-known stream.
        // Non-seekable streams (e.g. network streams, FormFile streams) cause the SDK to
        // compute an incorrect hash on the first pass and fail with a SHA256 mismatch error
        // against MinIO and other strict S3-compatible stores.
        MemoryStream buffered = new();
        await content.CopyToAsync(buffered, ct).ConfigureAwait(false);
        buffered.Position = 0;

        PutObjectRequest request = new()
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = buffered,
            AutoCloseStream = true,
            // Disable chunked transfer encoding. When chunk encoding is enabled the SDK sends
            // x-amz-content-sha256: STREAMING-AWS4-HMAC-SHA256-PAYLOAD, but MinIO and OVH
            // S3-compatible endpoints reject that value over HTTP. With UseChunkEncoding=false
            // the SDK computes the exact payload hash upfront, which both endpoints accept.
            UseChunkEncoding = false
        };

        await _s3Client.PutObjectAsync(request, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Stream> GetAsync(string key, CancellationToken ct = default)
    {
        GetObjectResponse response = await _s3Client
            .GetObjectAsync(_bucketName, key, ct)
            .ConfigureAwait(false);

        // Copy to a MemoryStream so the caller gets a fully-buffered, seekable stream and
        // the S3 response (and its underlying HTTP connection) can be disposed promptly.
        MemoryStream buffer = new();
        using (response)
        {
            await response.ResponseStream.CopyToAsync(buffer, ct).ConfigureAwait(false);
        }
        buffer.Position = 0;
        return buffer;
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _s3Client
                .GetObjectMetadataAsync(_bucketName, key, ct)
                .ConfigureAwait(false);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<PackageMetadata> GetMetadataAsync(string key, CancellationToken ct = default)
    {
        // Stream the object and compute SHA-256 on the fly.
        // S3's ETag is an MD5 hash (or multi-part composite) — not suitable as SHA-256.
        GetObjectResponse response = await _s3Client
            .GetObjectAsync(_bucketName, key, ct)
            .ConfigureAwait(false);

        using (response)
        {
            using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            byte[] rentedBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(81_920);
            try
            {
                long totalBytes = 0L;
                int read;
                while ((read = await response.ResponseStream
                           .ReadAsync(rentedBuffer, 0, rentedBuffer.Length, ct)
                           .ConfigureAwait(false)) > 0)
                {
                    hasher.AppendData(rentedBuffer, 0, read);
                    totalBytes += read;
                }

                byte[] hashBytes = hasher.GetCurrentHash();
                string sha256Hex = Convert.ToHexStringLower(hashBytes);
                return new PackageMetadata(sha256Hex, totalBytes);
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }
    }
}
