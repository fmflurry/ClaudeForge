namespace ClaudeForge.Infrastructure.Storage;

/// <summary>
/// Connection details for the OVH Object Storage / S3-compatible endpoint.
/// </summary>
public sealed class OvhStorageOptions
{
    /// <summary>HTTP(S) URL of the S3-compatible endpoint, e.g. "https://s3.ovh.fr".</summary>
    public string Endpoint { get; init; } = string.Empty;

    /// <summary>Name of the target bucket.</summary>
    public string BucketName { get; init; } = string.Empty;

    /// <summary>S3 access key / OVH credential.</summary>
    public string AccessKey { get; init; } = string.Empty;

    /// <summary>S3 secret key / OVH credential.</summary>
    public string SecretKey { get; init; } = string.Empty;
}
