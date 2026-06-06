using ClaudeForge.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace ClaudeForge.Tests.Unit.Storage;

/// <summary>
/// Unit tests for startup config/secret validation (Task 21.5).
///
/// These tests exercise <see cref="StorageOptionsValidator"/> directly — no host bootstrap
/// required. They assert fail-fast behaviour: missing required values for the selected
/// provider surface as a <see cref="ValidateOptionsResult"/> failure before the application
/// can serve traffic.
///
/// ─────────────────────────────────────────────────────────────────────────────────
/// Expected production types the coder MUST create:
///
///   Namespace : ClaudeForge.Infrastructure.Storage
///
///   Class: StorageOptions
///   ─────
///     string Type          — "LocalFileSystem" | "OVHObjectStorage"
///     string? LocalPath    — required when Type == "LocalFileSystem"; null otherwise accepted
///     OvhStorageOptions? Ovh — required (non-null with all sub-fields populated) when
///                              Type == "OVHObjectStorage"
///
///   Class: OvhStorageOptions
///   ─────────────────────────
///     string Endpoint    — HTTP(S) URL of the S3-compatible endpoint
///     string BucketName  — name of the target bucket
///     string AccessKey   — S3 access key / OVH credentials
///     string SecretKey   — S3 secret key / OVH credentials
///
///   Class: StorageOptionsValidator  (implements IValidateOptions&lt;StorageOptions&gt;)
///   ─────────────────────────────────
///     ValidateOptionsResult Validate(string? name, StorageOptions options)
///
///     Rules enforced:
///       - Type must be one of the known values ("LocalFileSystem", "OVHObjectStorage");
///         unknown value → failure with message containing "Unknown PackageStorage:Type"
///       - When Type == "LocalFileSystem": LocalPath must not be null/whitespace;
///         missing → failure with message containing "PackageStorage:LocalPath"
///       - When Type == "OVHObjectStorage": Ovh section must be non-null AND all four
///         sub-fields (Endpoint, BucketName, AccessKey, SecretKey) must be non-null/non-whitespace;
///         any missing field → failure with message identifying the missing field
///       - Valid config → ValidateOptionsResult.Success
///
/// ─────────────────────────────────────────────────────────────────────────────────
/// </summary>
public sealed class StorageOptionsValidatorTests
{
    private readonly IValidateOptions<StorageOptions> _validator = new StorageOptionsValidator();

    // ─────────────────────────────────────────────────────────────────────────
    // Happy-path: LocalFileSystem with a valid LocalPath
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_LocalFileSystem_WithLocalPath_ReturnsSuccess()
    {
        // Arrange
        StorageOptions options = new()
        {
            Type = "LocalFileSystem",
            LocalPath = "/packages"
        };

        // Act
        ValidateOptionsResult result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded, $"Expected success but got failure: {result.FailureMessage}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Happy-path: OVHObjectStorage with all four Ovh sub-fields populated
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_OvhObjectStorage_WithAllFields_ReturnsSuccess()
    {
        // Arrange
        StorageOptions options = new()
        {
            Type = "OVHObjectStorage",
            Ovh = new OvhStorageOptions
            {
                Endpoint = "https://s3.ovh.fr",
                BucketName = "claude-plugins",
                AccessKey = "my-access-key",
                SecretKey = "my-secret-key"
            }
        };

        // Act
        ValidateOptionsResult result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded, $"Expected success but got failure: {result.FailureMessage}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Failure: LocalFileSystem — LocalPath is null
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_LocalFileSystem_MissingLocalPath_ReturnsFailure()
    {
        // Arrange
        StorageOptions options = new()
        {
            Type = "LocalFileSystem",
            LocalPath = null
        };

        // Act
        ValidateOptionsResult result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.NotNull(result.FailureMessage);
        Assert.Contains("PackageStorage:LocalPath", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Failure: LocalFileSystem — LocalPath is whitespace only
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_LocalFileSystem_WhitespaceLocalPath_ReturnsFailure()
    {
        // Arrange
        StorageOptions options = new()
        {
            Type = "LocalFileSystem",
            LocalPath = "   "
        };

        // Act
        ValidateOptionsResult result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.NotNull(result.FailureMessage);
        Assert.Contains("PackageStorage:LocalPath", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Failure: OVHObjectStorage — Ovh section is null entirely
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_OvhObjectStorage_NullOvhSection_ReturnsFailure()
    {
        // Arrange
        StorageOptions options = new()
        {
            Type = "OVHObjectStorage",
            Ovh = null
        };

        // Act
        ValidateOptionsResult result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.NotNull(result.FailureMessage);
        // Must mention the Ovh section or OVHObjectStorage to be diagnosable
        Assert.Contains("OVH", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Failure: OVHObjectStorage — Endpoint missing
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_OvhObjectStorage_MissingEndpoint_ReturnsFailureWithEndpointMentioned()
    {
        // Arrange
        StorageOptions options = new()
        {
            Type = "OVHObjectStorage",
            Ovh = new OvhStorageOptions
            {
                Endpoint = "",           // missing
                BucketName = "claude-plugins",
                AccessKey = "key",
                SecretKey = "secret"
            }
        };

        // Act
        ValidateOptionsResult result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.NotNull(result.FailureMessage);
        Assert.Contains("Endpoint", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Failure: OVHObjectStorage — BucketName missing
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_OvhObjectStorage_MissingBucketName_ReturnsFailureWithBucketNameMentioned()
    {
        // Arrange
        StorageOptions options = new()
        {
            Type = "OVHObjectStorage",
            Ovh = new OvhStorageOptions
            {
                Endpoint = "https://s3.ovh.fr",
                BucketName = "",         // missing
                AccessKey = "key",
                SecretKey = "secret"
            }
        };

        // Act
        ValidateOptionsResult result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.NotNull(result.FailureMessage);
        Assert.Contains("BucketName", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Failure: OVHObjectStorage — AccessKey missing
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_OvhObjectStorage_MissingAccessKey_ReturnsFailureWithAccessKeyMentioned()
    {
        // Arrange
        StorageOptions options = new()
        {
            Type = "OVHObjectStorage",
            Ovh = new OvhStorageOptions
            {
                Endpoint = "https://s3.ovh.fr",
                BucketName = "claude-plugins",
                AccessKey = null!,       // missing
                SecretKey = "secret"
            }
        };

        // Act
        ValidateOptionsResult result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.NotNull(result.FailureMessage);
        Assert.Contains("AccessKey", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Failure: OVHObjectStorage — SecretKey missing
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_OvhObjectStorage_MissingSecretKey_ReturnsFailureWithSecretKeyMentioned()
    {
        // Arrange
        StorageOptions options = new()
        {
            Type = "OVHObjectStorage",
            Ovh = new OvhStorageOptions
            {
                Endpoint = "https://s3.ovh.fr",
                BucketName = "claude-plugins",
                AccessKey = "key",
                SecretKey = ""           // missing
            }
        };

        // Act
        ValidateOptionsResult result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.NotNull(result.FailureMessage);
        Assert.Contains("SecretKey", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Failure: Unknown Type value
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_UnknownType_ReturnsFailureWithTypeMentioned()
    {
        // Arrange
        StorageOptions options = new()
        {
            Type = "AzureBlobStorage"   // not in the known set
        };

        // Act
        ValidateOptionsResult result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.NotNull(result.FailureMessage);
        Assert.Contains("PackageStorage:Type", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Failure: Type is null / empty
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_NullOrEmptyType_ReturnsFailure(string? type)
    {
        // Arrange
        StorageOptions options = new()
        {
            Type = type!
        };

        // Act
        ValidateOptionsResult result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.NotNull(result.FailureMessage);
        Assert.Contains("PackageStorage:Type", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Failure: OVHObjectStorage — multiple fields missing — all are reported
    // (Ensures the validator accumulates errors rather than short-circuiting)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_OvhObjectStorage_MultipleFieldsMissing_FailureMessageMentionsAllMissing()
    {
        // Arrange
        StorageOptions options = new()
        {
            Type = "OVHObjectStorage",
            Ovh = new OvhStorageOptions
            {
                Endpoint = "",
                BucketName = "",
                AccessKey = "",
                SecretKey = ""
            }
        };

        // Act
        ValidateOptionsResult result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.NotNull(result.FailureMessage);

        // All four missing field names should appear in the combined failure message
        Assert.Contains("Endpoint", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BucketName", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AccessKey", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SecretKey", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Edge: validator name parameter is null — should work for all cases
    // (IValidateOptions.Validate receives null name when invoked by the framework)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_NullName_ValidConfig_ReturnsSuccess()
    {
        // Arrange
        StorageOptions options = new()
        {
            Type = "LocalFileSystem",
            LocalPath = "/packages"
        };

        // Act — name is null (framework default)
        ValidateOptionsResult result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Edge: OVHObjectStorage — Ovh section present but Endpoint is whitespace only
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_OvhObjectStorage_WhitespaceEndpoint_ReturnsFailure()
    {
        // Arrange
        StorageOptions options = new()
        {
            Type = "OVHObjectStorage",
            Ovh = new OvhStorageOptions
            {
                Endpoint = "   ",
                BucketName = "claude-plugins",
                AccessKey = "key",
                SecretKey = "secret"
            }
        };

        // Act
        ValidateOptionsResult result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.NotNull(result.FailureMessage);
        Assert.Contains("Endpoint", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }
}
