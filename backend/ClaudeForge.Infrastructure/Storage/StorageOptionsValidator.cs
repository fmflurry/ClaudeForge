using Microsoft.Extensions.Options;

namespace ClaudeForge.Infrastructure.Storage;

/// <summary>
/// Validates <see cref="StorageOptions"/> at startup so the application fails fast when
/// required configuration values are missing or invalid.
///
/// All validation errors are accumulated before returning so that every missing field is
/// surfaced in a single failure message rather than short-circuiting on the first error.
/// </summary>
public sealed class StorageOptionsValidator : IValidateOptions<StorageOptions>
{
    private const string LocalFileSystemType = "LocalFileSystem";
    private const string OvhObjectStorageType = "OVHObjectStorage";

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, StorageOptions options)
    {
        List<string> errors = new();

        if (string.IsNullOrWhiteSpace(options.Type))
        {
            errors.Add("PackageStorage:Type is required.");
        }
        else if (options.Type is not LocalFileSystemType and not OvhObjectStorageType)
        {
            errors.Add($"Unknown PackageStorage:Type '{options.Type}'. Allowed values: {LocalFileSystemType}, {OvhObjectStorageType}.");
        }
        else if (options.Type == LocalFileSystemType)
        {
            if (string.IsNullOrWhiteSpace(options.LocalPath))
            {
                errors.Add("PackageStorage:LocalPath is required when Type is LocalFileSystem.");
            }
        }
        else if (options.Type == OvhObjectStorageType)
        {
            if (options.Ovh is null)
            {
                errors.Add("OVH storage options (PackageStorage:Ovh) are required when Type is OVHObjectStorage.");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(options.Ovh.Endpoint))
                    errors.Add("PackageStorage:Ovh:Endpoint is required.");

                if (string.IsNullOrWhiteSpace(options.Ovh.BucketName))
                    errors.Add("PackageStorage:Ovh:BucketName is required.");

                if (string.IsNullOrWhiteSpace(options.Ovh.AccessKey))
                    errors.Add("PackageStorage:Ovh:AccessKey is required.");

                if (string.IsNullOrWhiteSpace(options.Ovh.SecretKey))
                    errors.Add("PackageStorage:Ovh:SecretKey is required.");
            }
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}
