using ClaudeForge.Application.Modules.AddOnDistribution.Ports;
using ClaudeForge.Application.Modules.AddOnDistribution.UseCases;
using ClaudeForge.Core.Ports;
using NSubstitute;

namespace ClaudeForge.Tests.Unit.AddOnDistribution;

/// <summary>
/// Unit tests for Group 6 — DownloadAddOnUseCase.
///
/// Uses NSubstitute for all ports — no real database or filesystem.
///
/// Expected production types (coder MUST match these names exactly):
///
///   NAMESPACE: ClaudeForge.Application.Modules.AddOnDistribution.Ports
///
///   IAddOnDistributionRepositoryPort
///     Task&lt;DownloadResolutionResult&gt; ResolveAsync(
///         Guid pluginId, string? version, CancellationToken ct = default)
///     Task IncrementDownloadCountAsync(
///         Guid pluginId, string version, CancellationToken ct = default)
///
///   DownloadResolutionResult  — discriminated union (sealed hierarchy):
///
///     abstract record DownloadResolutionResult
///
///     sealed record PluginNotFoundResult : DownloadResolutionResult
///       (no payload — plugin row does not exist)
///
///     sealed record VersionNotFoundResult(string Version) : DownloadResolutionResult
///       (plugin exists but the requested explicit version does not)
///
///     sealed record FoundResult(DownloadResolution Resolution) : DownloadResolutionResult
///
///   DownloadResolution (sealed record)
///     string PluginName
///     string Version
///     string PackageKey
///     string PackageFormat     // "tar.gz" | "zip"
///     long   SizeBytes
///     string Sha256
///
///   NAMESPACE: ClaudeForge.Application.Modules.AddOnDistribution.UseCases
///
///   DownloadAddOnUseCase
///     DownloadAddOnUseCase(
///         IAddOnDistributionRepositoryPort repo,
///         IPackageStoragePort storage)
///     Task&lt;DownloadResult&gt; ExecuteAsync(
///         Guid pluginId, string? version, CancellationToken ct = default)
///
///   DownloadResult (sealed record)
///     Stream  Stream
///     string  FileName        // e.g. "my-plugin-1.0.0.tar.gz"
///     string  ContentType     // "application/gzip" | "application/zip"
///     long    SizeBytes
///     string  Sha256
///
///   --- Domain exceptions (ProblemDetailsException subclasses) ---
///
///   AddOnNotFoundException : ProblemDetailsException
///     Message    == "Plugin not found"
///     StatusCode == 404
///     (Reuse ClaudeForge.Application.Modules.AddOnCatalog.UseCases.AddOnNotFoundException)
///
///   VersionNotFoundException : ProblemDetailsException
///     Message    == "Plugin version {version} not found"   (e.g. "Plugin version 9.9.9 not found")
///     StatusCode == 404
///
///   InvalidVersionFormatException : ProblemDetailsException
///     Message    == "Invalid version format. Expected semver (e.g., 1.0.0)"
///     StatusCode == 400
///     (New type in PluginDistribution namespace, or reuse if already exists — coder decides)
///
/// VERBATIM spec strings confirmed from plugin-download/spec.md:
///   "Plugin not found"
///   "Plugin version 9.9.9 not found"      (pattern: "Plugin version {version} not found")
///   "Invalid version format. Expected semver (e.g., 1.0.0)"
/// </summary>
public sealed class DownloadPluginUseCaseTests
{
    // =========================================================================
    // Shared helpers
    // =========================================================================

    private static DownloadResolution MakeResolution(
        string pluginName = "test-plugin",
        string version = "1.0.0",
        string packageFormat = "tar.gz",
        long sizeBytes = 1024,
        string sha256 = "abc123def456abc123def456abc123def456abc123def456abc123def456abc1") =>
        new(
            PluginName: pluginName,
            Version: version,
            PackageKey: $"plugins/{Guid.NewGuid()}/{version}/package.{packageFormat}",
            PackageFormat: packageFormat,
            SizeBytes: sizeBytes,
            Sha256: sha256);

    private static (
        IAddOnDistributionRepositoryPort repo,
        IPackageStoragePort storage,
        DownloadAddOnUseCase useCase) BuildUseCase(
        DownloadResolutionResult? resolveResult = null)
    {
        IAddOnDistributionRepositoryPort repo =
            Substitute.For<IAddOnDistributionRepositoryPort>();
        IPackageStoragePort storage =
            Substitute.For<IPackageStoragePort>();

        DownloadResolution resolution = MakeResolution();
        resolveResult ??= new FoundResult(resolution);

        repo.ResolveAsync(Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(resolveResult);

        // Default storage: return a non-empty stream
        storage.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Stream>(new MemoryStream("package-bytes"u8.ToArray())));

        DownloadAddOnUseCase useCase = new(repo, storage);
        return (repo, storage, useCase);
    }

    // =========================================================================
    // Happy path — version null resolves to latest
    // =========================================================================

    [Fact]
    public async Task ExecuteAsync_NullVersion_ResolvesAndStreamsLatestVersion()
    {
        // Arrange
        DownloadResolution resolution = MakeResolution(pluginName: "my-plugin", version: "2.0.0");
        (_, _, DownloadAddOnUseCase useCase) =
            BuildUseCase(new FoundResult(resolution));

        // Act
        DownloadResult result = await useCase.ExecuteAsync(Guid.NewGuid(), null);

        // Assert
        Assert.Equal("2.0.0", result.FileName.Contains("2.0.0") ? "2.0.0" : result.FileName);
        Assert.NotNull(result.Stream);
        Assert.True(result.SizeBytes > 0 || result.SizeBytes == resolution.SizeBytes);
    }

    [Fact]
    public async Task ExecuteAsync_NullVersion_PassesNullToResolveAsync()
    {
        // Arrange
        Guid pluginId = Guid.NewGuid();
        (IAddOnDistributionRepositoryPort repo, _, DownloadAddOnUseCase useCase) =
            BuildUseCase();

        // Act
        await useCase.ExecuteAsync(pluginId, null);

        // Assert — null is forwarded so the repo can pick is_latest
        await repo.Received(1).ResolveAsync(pluginId, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_VersionStringLatest_PassesNullOrLatestToResolveAsync()
    {
        // Arrange
        Guid pluginId = Guid.NewGuid();
        (IAddOnDistributionRepositoryPort repo, _, DownloadAddOnUseCase useCase) =
            BuildUseCase();

        // Act — "latest" is treated equivalently to null (default latest)
        await useCase.ExecuteAsync(pluginId, "latest");

        // Assert — either null or "latest" forwarded; the resolution is the is_latest row
        await repo.Received(1).ResolveAsync(
            pluginId,
            Arg.Is<string?>(v => v == null || v == "latest"),
            Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Happy path — explicit version resolution
    // =========================================================================

    [Fact]
    public async Task ExecuteAsync_ExplicitVersion_PassesVersionToResolveAsync()
    {
        // Arrange
        Guid pluginId = Guid.NewGuid();
        (IAddOnDistributionRepositoryPort repo, _, DownloadAddOnUseCase useCase) =
            BuildUseCase(new FoundResult(MakeResolution(version: "1.5.0")));

        // Act
        await useCase.ExecuteAsync(pluginId, "1.5.0");

        // Assert
        await repo.Received(1).ResolveAsync(pluginId, "1.5.0", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ExplicitVersion_ReturnsCorrectDownloadResult()
    {
        // Arrange
        DownloadResolution resolution = MakeResolution(
            pluginName: "test-plugin",
            version: "1.2.3",
            packageFormat: "tar.gz",
            sizeBytes: 2048,
            sha256: "aabbccddaabbccddaabbccddaabbccddaabbccddaabbccddaabbccddaabbccdd");

        (_, _, DownloadAddOnUseCase useCase) =
            BuildUseCase(new FoundResult(resolution));

        // Act
        DownloadResult result = await useCase.ExecuteAsync(Guid.NewGuid(), "1.2.3");

        // Assert — spec: filename is "{name}-{version}.tar.gz"
        Assert.Equal("test-plugin-1.2.3.tar.gz", result.FileName);
        Assert.Equal("application/gzip", result.ContentType);
        Assert.Equal(2048, result.SizeBytes);
        Assert.Equal("aabbccddaabbccddaabbccddaabbccddaabbccddaabbccddaabbccddaabbccdd",
            result.Sha256);
        Assert.NotNull(result.Stream);
    }

    [Fact]
    public async Task ExecuteAsync_ZipPackageFormat_ReturnsApplicationZipContentType()
    {
        // Arrange
        DownloadResolution resolution = MakeResolution(
            pluginName: "zip-plugin",
            version: "1.0.0",
            packageFormat: "zip");

        (_, _, DownloadAddOnUseCase useCase) =
            BuildUseCase(new FoundResult(resolution));

        // Act
        DownloadResult result = await useCase.ExecuteAsync(Guid.NewGuid(), "1.0.0");

        // Assert — spec: Content-Type is application/zip for zip format
        Assert.Equal("application/zip", result.ContentType);
        Assert.Equal("zip-plugin-1.0.0.zip", result.FileName);
    }

    [Fact]
    public async Task ExecuteAsync_TarGzFormat_FilenameHasTarGzExtension()
    {
        // Arrange
        DownloadResolution resolution = MakeResolution(
            pluginName: "my-plugin",
            version: "3.0.0",
            packageFormat: "tar.gz");

        (_, _, DownloadAddOnUseCase useCase) =
            BuildUseCase(new FoundResult(resolution));

        // Act
        DownloadResult result = await useCase.ExecuteAsync(Guid.NewGuid(), "3.0.0");

        // Assert — spec: Content-Disposition attachment filename="{name}-{version}.tar.gz"
        Assert.Equal("my-plugin-3.0.0.tar.gz", result.FileName);
        Assert.Equal("application/gzip", result.ContentType);
    }

    // =========================================================================
    // Happy path — storage is called with the resolved package key
    // =========================================================================

    [Fact]
    public async Task ExecuteAsync_OnFound_CallsStorageGetAsyncWithPackageKey()
    {
        // Arrange
        string expectedKey = "plugins/some-id/1.0.0/package.tar.gz";
        DownloadResolution resolution = new(
            PluginName: "test-plugin",
            Version: "1.0.0",
            PackageKey: expectedKey,
            PackageFormat: "tar.gz",
            SizeBytes: 512,
            Sha256: "aaaa1111aaaa1111aaaa1111aaaa1111aaaa1111aaaa1111aaaa1111aaaa1111");

        (_, IPackageStoragePort storage, DownloadAddOnUseCase useCase) =
            BuildUseCase(new FoundResult(resolution));

        // Act
        await useCase.ExecuteAsync(Guid.NewGuid(), "1.0.0");

        // Assert — GetAsync called with the exact key from the resolution
        await storage.Received(1).GetAsync(expectedKey, Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Counter increment — ONLY on success
    // =========================================================================

    [Fact]
    public async Task ExecuteAsync_OnSuccessfulDownload_CallsIncrementDownloadCountAsync()
    {
        // Arrange
        Guid pluginId = Guid.NewGuid();
        DownloadResolution resolution = MakeResolution(version: "1.0.0");
        (IAddOnDistributionRepositoryPort repo, _, DownloadAddOnUseCase useCase) =
            BuildUseCase(new FoundResult(resolution));

        // Act
        await useCase.ExecuteAsync(pluginId, "1.0.0");

        // Assert — counter must be incremented exactly once
        await repo.Received(1).IncrementDownloadCountAsync(
            pluginId,
            "1.0.0",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccessfulLatestDownload_IncrementUsesResolvedVersion()
    {
        // Arrange
        Guid pluginId = Guid.NewGuid();
        DownloadResolution resolution = MakeResolution(version: "2.5.0");
        (IAddOnDistributionRepositoryPort repo, _, DownloadAddOnUseCase useCase) =
            BuildUseCase(new FoundResult(resolution));

        // Act — no explicit version → latest resolved to "2.5.0"
        await useCase.ExecuteAsync(pluginId, null);

        // Assert — increment uses the RESOLVED version, not the input "null"
        await repo.Received(1).IncrementDownloadCountAsync(
            pluginId,
            "2.5.0",
            Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // 404 — unknown plugin (PluginNotFoundResult)
    // VERBATIM spec string (plugin-download/spec.md): "Plugin not found"
    // =========================================================================

    [Fact]
    public async Task ExecuteAsync_PluginNotFound_ThrowsPluginNotFoundException()
    {
        // Arrange
        (_, _, DownloadAddOnUseCase useCase) = BuildUseCase(new PluginNotFoundResult());

        // Act & Assert
        ClaudeForge.Application.Modules.AddOnCatalog.UseCases.AddOnNotFoundException ex =
            await Assert.ThrowsAsync<ClaudeForge.Application.Modules.AddOnCatalog.UseCases.AddOnNotFoundException>(
                () => useCase.ExecuteAsync(Guid.NewGuid(), null));

        Assert.Equal("Plugin not found", ex.Message);
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task ExecuteAsync_PluginNotFound_NeverCallsIncrementDownloadCount()
    {
        // Arrange
        Guid pluginId = Guid.NewGuid();
        (IAddOnDistributionRepositoryPort repo, _, DownloadAddOnUseCase useCase) =
            BuildUseCase(new PluginNotFoundResult());

        // Act
        await Assert.ThrowsAsync<ClaudeForge.Application.Modules.AddOnCatalog.UseCases.AddOnNotFoundException>(
            () => useCase.ExecuteAsync(pluginId, null));

        // Assert — NEVER increment on 404
        await repo.DidNotReceive().IncrementDownloadCountAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_PluginNotFound_NeverCallsStorageGetAsync()
    {
        // Arrange
        (_, IPackageStoragePort storage, DownloadAddOnUseCase useCase) =
            BuildUseCase(new PluginNotFoundResult());

        // Act
        await Assert.ThrowsAsync<ClaudeForge.Application.Modules.AddOnCatalog.UseCases.AddOnNotFoundException>(
            () => useCase.ExecuteAsync(Guid.NewGuid(), null));

        // Assert — storage not touched on 404
        await storage.DidNotReceive().GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // 404 — unknown explicit version (VersionNotFoundResult)
    // VERBATIM spec string (plugin-download/spec.md): "Plugin version 9.9.9 not found"
    // =========================================================================

    [Fact]
    public async Task ExecuteAsync_VersionNotFound_ThrowsVersionNotFoundException()
    {
        // Arrange — plugin exists but version 9.9.9 does not
        (_, _, DownloadAddOnUseCase useCase) =
            BuildUseCase(new VersionNotFoundResult("9.9.9"));

        // Act & Assert
        VersionNotFoundException ex =
            await Assert.ThrowsAsync<VersionNotFoundException>(
                () => useCase.ExecuteAsync(Guid.NewGuid(), "9.9.9"));

        // VERBATIM spec string: "Plugin version 9.9.9 not found"
        Assert.Equal("Plugin version 9.9.9 not found", ex.Message);
        Assert.Equal(404, ex.StatusCode);
    }

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("2.5.3")]
    [InlineData("0.1.0")]
    public async Task ExecuteAsync_VersionNotFound_MessageContainsRequestedVersion(string version)
    {
        // Arrange
        (_, _, DownloadAddOnUseCase useCase) =
            BuildUseCase(new VersionNotFoundResult(version));

        // Act & Assert
        VersionNotFoundException ex =
            await Assert.ThrowsAsync<VersionNotFoundException>(
                () => useCase.ExecuteAsync(Guid.NewGuid(), version));

        Assert.Equal($"Plugin version {version} not found", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_VersionNotFound_NeverCallsIncrementDownloadCount()
    {
        // Arrange
        Guid pluginId = Guid.NewGuid();
        (IAddOnDistributionRepositoryPort repo, _, DownloadAddOnUseCase useCase) =
            BuildUseCase(new VersionNotFoundResult("9.9.9"));

        // Act
        await Assert.ThrowsAsync<VersionNotFoundException>(
            () => useCase.ExecuteAsync(pluginId, "9.9.9"));

        // Assert — NEVER increment on 404
        await repo.DidNotReceive().IncrementDownloadCountAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_VersionNotFound_NeverCallsStorageGetAsync()
    {
        // Arrange
        (_, IPackageStoragePort storage, DownloadAddOnUseCase useCase) =
            BuildUseCase(new VersionNotFoundResult("9.9.9"));

        // Act
        await Assert.ThrowsAsync<VersionNotFoundException>(
            () => useCase.ExecuteAsync(Guid.NewGuid(), "9.9.9"));

        // Assert
        await storage.DidNotReceive().GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // 400 — invalid version format
    // VERBATIM spec string (plugin-download/spec.md):
    //   "Invalid version format. Expected semver (e.g., 1.0.0)"
    // =========================================================================

    [Theory]
    [InlineData("not-a-version")]
    [InlineData("v1.0.0")]
    [InlineData("1.0")]
    [InlineData("1.0.0-beta")]
    [InlineData("1.0.0+build123")]
    [InlineData("abc")]
    public async Task ExecuteAsync_InvalidVersionFormat_ThrowsInvalidVersionFormatException(
        string badVersion)
    {
        // Arrange — use case must validate before calling repo
        IAddOnDistributionRepositoryPort repo =
            Substitute.For<IAddOnDistributionRepositoryPort>();
        IPackageStoragePort storage =
            Substitute.For<IPackageStoragePort>();

        DownloadAddOnUseCase useCase = new(repo, storage);

        // Act & Assert
        InvalidDownloadVersionFormatException ex =
            await Assert.ThrowsAsync<InvalidDownloadVersionFormatException>(
                () => useCase.ExecuteAsync(Guid.NewGuid(), badVersion));

        // VERBATIM spec string
        Assert.Equal(
            "Invalid version format. Expected semver (e.g., 1.0.0)",
            ex.Message);
        Assert.Equal(400, ex.StatusCode);
    }

    [Theory]
    [InlineData("not-a-version")]
    [InlineData("v1.0.0")]
    [InlineData("1.0")]
    public async Task ExecuteAsync_InvalidVersionFormat_NeverCallsRepo(string badVersion)
    {
        // Arrange
        IAddOnDistributionRepositoryPort repo =
            Substitute.For<IAddOnDistributionRepositoryPort>();
        IPackageStoragePort storage =
            Substitute.For<IPackageStoragePort>();

        DownloadAddOnUseCase useCase = new(repo, storage);

        // Act
        await Assert.ThrowsAsync<InvalidDownloadVersionFormatException>(
            () => useCase.ExecuteAsync(Guid.NewGuid(), badVersion));

        // Assert — repo must NOT be called when version format is invalid
        await repo.DidNotReceive().ResolveAsync(
            Arg.Any<Guid>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Edge case — null/"latest" are valid (not subject to semver validation)
    // =========================================================================

    [Theory]
    [InlineData(null)]
    [InlineData("latest")]
    public async Task ExecuteAsync_NullOrLatestVersion_IsNotRejectedByFormatValidation(
        string? version)
    {
        // Arrange
        (_, _, DownloadAddOnUseCase useCase) =
            BuildUseCase(new FoundResult(MakeResolution()));

        // Act — must NOT throw InvalidDownloadVersionFormatException
        DownloadResult result = await useCase.ExecuteAsync(Guid.NewGuid(), version);

        // Assert
        Assert.NotNull(result);
    }
}
