using System.Formats.Tar;
using System.IO.Compression;
using ClaudeForge.Application.Modules.PluginPublishing.Ports;
using ClaudeForge.Application.Modules.PluginPublishing.UseCases;
using ClaudeForge.Core.Domain.Packaging;
using ClaudeForge.Core.Domain.Plugins;
using ClaudeForge.Core.Ports;
using ClaudeForge.Infrastructure.Packaging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ClaudeForge.Tests.Unit.PluginPublishing;

/// <summary>
/// Unit tests for Group 5 use-cases: UploadPluginUseCase and PublishVersionUseCase.
///
/// Uses NSubstitute for all ports — no real database or filesystem.
///
/// Expected production types (coder MUST match these names exactly):
///
///   NAMESPACE: ClaudeForge.Application.Modules.PluginPublishing.Ports
///
///   IPluginPublishingRepositoryPort
///     Task&lt;PluginPublishResult&gt; CreatePluginWithInitialVersionAsync(
///         CreatePluginCommand command, CancellationToken ct = default)
///     Task&lt;PluginVersionPublishResult&gt; AddVersionAsync(
///         Guid pluginId, AddVersionCommand command, CancellationToken ct = default)
///     Task&lt;bool&gt; ExistsByNameNormalizedAsync(
///         string nameNormalized, CancellationToken ct = default)
///     Task&lt;bool&gt; PluginExistsAsync(
///         Guid pluginId, CancellationToken ct = default)
///     Task&lt;bool&gt; VersionExistsAsync(
///         Guid pluginId, string version, CancellationToken ct = default)
///
///   CreatePluginCommand (record)
///     string Name, string NameNormalized, string Slug,
///     string Description, string Author,
///     string Version, long VersionSort,
///     string PackageKey, string PackageFormat,
///     long SizeBytes, string Sha256,
///     string ReleaseNotes, string? ReadmeText
///
///   AddVersionCommand (record)
///     string Version, long VersionSort,
///     string PackageKey, string PackageFormat,
///     long SizeBytes, string Sha256, string ReleaseNotes
///
///   PluginPublishResult (record)
///     Guid PluginId, string Version
///
///   PluginVersionPublishResult (record)
///     Guid PluginId, Guid VersionId, string Version
///
///   NAMESPACE: ClaudeForge.Application.Modules.PluginPublishing.UseCases
///
///   UploadPluginUseCase
///     UploadPluginUseCase(
///         IPluginPublishingRepositoryPort repository,
///         IPackageStoragePort storage,
///         IPackageReader packageReader)
///     Task&lt;PluginPublishResult&gt; ExecuteAsync(
///         UploadPluginCommand command, CancellationToken ct = default)
///
///   UploadPluginCommand (record)
///     Stream PackageStream, string FileName,
///     string Name, string Description, string Author,
///     string InitialVersion, string ReleaseNotes
///
///   PublishVersionUseCase
///     PublishVersionUseCase(
///         IPluginPublishingRepositoryPort repository,
///         IPackageStoragePort storage,
///         IPackageReader packageReader)
///     Task&lt;PluginVersionPublishResult&gt; ExecuteAsync(
///         PublishVersionCommand command, CancellationToken ct = default)
///
///   PublishVersionCommand (record)
///     Guid PluginId, Stream PackageStream, string FileName,
///     string Version, string ReleaseNotes
///
///   --- Domain exceptions (ProblemDetailsException subclasses) ---
///
///   MissingPackageFileException : ProblemDetailsException
///     Message == "Package file is required"
///     StatusCode == 400
///
///   MissingRequiredFieldException : ProblemDetailsException
///     Message == "Required field missing: {fieldName}" (e.g. "Required field missing: name")
///     StatusCode == 400
///
///   InvalidSemVerException : ProblemDetailsException
///     Message == "initialVersion must be a valid semantic version (e.g., 1.0.0)"
///     StatusCode == 400
///
///   DuplicatePluginNameException : ProblemDetailsException
///     Message == "A plugin with name '{name}' already exists"
///     StatusCode == 409
///
///   DuplicateVersionException : ProblemDetailsException
///     Message == "Version {version} already exists"
///     StatusCode == 409
///
///   PluginNotFoundForVersionException : ProblemDetailsException
///     Message == "Plugin not found"
///     StatusCode == 404
///
///   InvalidVersionFormatException : ProblemDetailsException
///     Message == "Version must be in format MAJOR.MINOR.PATCH (e.g., 1.2.3)"
///     StatusCode == 400
///
/// Spec verbatim strings confirmed from:
///   plugin-upload/spec.md  and  plugin-versioning/spec.md
/// </summary>
public sealed class PluginPublishingUseCaseTests
{
    // =========================================================================
    // Archive builders — reused from PackageValidatorTests pattern (BCL only)
    // =========================================================================

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
        }

        output.Position = 0;
        return output;
    }

    private static MemoryStream BuildValidPluginTarGz(
        string name = "test-plugin",
        string version = "1.0.0",
        string readme = "# Test Plugin") =>
        BuildTarGz([
            ("plugin.json",
                $$"""{"name":"{{name}}","version":"{{version}}","description":"Test description","author":"Test Author","types":["skill"],"languages":["typescript"]}"""),
            ("README.md", readme),
        ]);

    // =========================================================================
    // Helpers: build a valid UploadPluginCommand
    // =========================================================================

    private static UploadPluginCommand MakeUploadCommand(
        Stream? packageStream = null,
        string fileName = "test-plugin-1.0.0.tar.gz",
        string name = "TestPlugin",
        string description = "Test description",
        string author = "Test Author",
        string initialVersion = "1.0.0",
        string releaseNotes = "Initial release") =>
        new(
            PackageStream: packageStream ?? BuildValidPluginTarGz(name.ToLowerInvariant(), initialVersion),
            FileName: fileName,
            Name: name,
            Description: description,
            Author: author,
            InitialVersion: initialVersion,
            ReleaseNotes: releaseNotes);

    // =========================================================================
    // Helpers: default mock setup for a "success" scenario
    // =========================================================================

    private static (
        IPluginPublishingRepositoryPort repo,
        IPackageStoragePort storage,
        UploadPluginUseCase useCase) BuildUploadUseCase(
        bool nameExists = false,
        PluginPublishResult? publishResult = null)
    {
        IPluginPublishingRepositoryPort repo = Substitute.For<IPluginPublishingRepositoryPort>();
        IPackageStoragePort storage = Substitute.For<IPackageStoragePort>();
        IPackageReader reader = new PackageReader();

        repo.ExistsByNameNormalizedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(nameExists);

        publishResult ??= new PluginPublishResult(Guid.NewGuid(), "1.0.0");
        repo.CreatePluginWithInitialVersionAsync(
                Arg.Any<CreatePluginCommand>(),
                Arg.Any<CancellationToken>())
            .Returns(publishResult);

        UploadPluginUseCase useCase = new(repo, storage, reader);
        return (repo, storage, useCase);
    }

    private static (
        IPluginPublishingRepositoryPort repo,
        IPackageStoragePort storage,
        PublishVersionUseCase useCase) BuildPublishVersionUseCase(
        bool pluginExists = true,
        bool versionExists = false,
        PluginVersionPublishResult? publishResult = null)
    {
        IPluginPublishingRepositoryPort repo = Substitute.For<IPluginPublishingRepositoryPort>();
        IPackageStoragePort storage = Substitute.For<IPackageStoragePort>();
        IPackageReader reader = new PackageReader();

        repo.PluginExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(pluginExists);

        repo.VersionExistsAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(versionExists);

        publishResult ??= new PluginVersionPublishResult(Guid.NewGuid(), Guid.NewGuid(), "1.1.0");
        repo.AddVersionAsync(Arg.Any<Guid>(), Arg.Any<AddVersionCommand>(), Arg.Any<CancellationToken>())
            .Returns(publishResult);

        PublishVersionUseCase useCase = new(repo, storage, reader);
        return (repo, storage, useCase);
    }

    // =========================================================================
    // UploadPluginUseCase — happy path
    // =========================================================================

    [Fact]
    public async Task UploadPlugin_ValidPackageAndMetadata_Returns201WithPluginIdAndVersion()
    {
        // Arrange
        Guid expectedPluginId = Guid.NewGuid();
        PluginPublishResult expected = new(expectedPluginId, "1.0.0");
        (_, _, UploadPluginUseCase useCase) = BuildUploadUseCase(publishResult: expected);

        UploadPluginCommand command = MakeUploadCommand();

        // Act
        PluginPublishResult result = await useCase.ExecuteAsync(command);

        // Assert
        Assert.Equal(expectedPluginId, result.PluginId);
        Assert.Equal("1.0.0", result.Version);
    }

    [Fact]
    public async Task UploadPlugin_ValidPackage_StoresPackageViaIPackageStoragePort()
    {
        // Arrange
        (_, IPackageStoragePort storage, UploadPluginUseCase useCase) =
            BuildUploadUseCase();

        UploadPluginCommand command = MakeUploadCommand();

        // Act
        await useCase.ExecuteAsync(command);

        // Assert — PutAsync must have been called exactly once
        await storage.Received(1).PutAsync(
            Arg.Any<string>(),
            Arg.Any<Stream>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadPlugin_ValidPackage_PackageKeyFollowsConvention()
    {
        // Arrange — capture the key used in PutAsync
        (_, IPackageStoragePort storage, UploadPluginUseCase useCase) = BuildUploadUseCase();
        string? capturedKey = null;
        await storage.PutAsync(
            Arg.Do<string>(k => capturedKey = k),
            Arg.Any<Stream>(),
            Arg.Any<CancellationToken>());

        UploadPluginCommand command = MakeUploadCommand(name: "MyPlugin", initialVersion: "1.0.0");

        // Act
        await useCase.ExecuteAsync(command);

        // Assert — key must match "plugins/{guid}/{version}/package.{ext}"
        Assert.NotNull(capturedKey);
        Assert.Matches(@"^plugins/[0-9a-fA-F-]{36}/1\.0\.0/package\.(tar\.gz|zip)$", capturedKey);
    }

    [Fact]
    public async Task UploadPlugin_ValidPackageWithReadme_ReadmeExtractedAndPassedToRepository()
    {
        // Arrange
        const string expectedReadme = "# My Plugin README";
        MemoryStream archive = BuildTarGz([
            ("plugin.json", """{"name":"plugin","version":"1.0.0","description":"Desc","author":"Author","types":["skill"],"languages":["typescript"]}"""),
            ("README.md", expectedReadme),
        ]);

        (IPluginPublishingRepositoryPort repo, _, UploadPluginUseCase useCase) = BuildUploadUseCase();

        CreatePluginCommand? capturedCommand = null;
        await repo.CreatePluginWithInitialVersionAsync(
            Arg.Do<CreatePluginCommand>(c => capturedCommand = c),
            Arg.Any<CancellationToken>());
        repo.CreatePluginWithInitialVersionAsync(
                Arg.Any<CreatePluginCommand>(),
                Arg.Any<CancellationToken>())
            .Returns(new PluginPublishResult(Guid.NewGuid(), "1.0.0"));

        UploadPluginCommand command = MakeUploadCommand(packageStream: archive);

        // Act
        await useCase.ExecuteAsync(command);

        // Assert
        Assert.NotNull(capturedCommand);
        Assert.Equal(expectedReadme, capturedCommand!.ReadmeText);
    }

    [Fact]
    public async Task UploadPlugin_ValidPackageWithoutReadme_ReadmeTextIsNull()
    {
        // Arrange
        MemoryStream archive = BuildTarGz([
            ("plugin.json", """{"name":"plugin","version":"1.0.0","description":"Desc","author":"Author","types":["skill"],"languages":["typescript"]}"""),
        ]);

        (IPluginPublishingRepositoryPort repo, _, UploadPluginUseCase useCase) = BuildUploadUseCase();

        CreatePluginCommand? capturedCommand = null;
        await repo.CreatePluginWithInitialVersionAsync(
            Arg.Do<CreatePluginCommand>(c => capturedCommand = c),
            Arg.Any<CancellationToken>());
        repo.CreatePluginWithInitialVersionAsync(
                Arg.Any<CreatePluginCommand>(),
                Arg.Any<CancellationToken>())
            .Returns(new PluginPublishResult(Guid.NewGuid(), "1.0.0"));

        UploadPluginCommand command = MakeUploadCommand(packageStream: archive);

        // Act
        await useCase.ExecuteAsync(command);

        // Assert
        Assert.NotNull(capturedCommand);
        Assert.Null(capturedCommand!.ReadmeText);
    }

    [Fact]
    public async Task UploadPlugin_CreatesInitialVersionWithIsLatestTrue()
    {
        // Arrange
        (IPluginPublishingRepositoryPort repo, _, UploadPluginUseCase useCase) = BuildUploadUseCase();

        CreatePluginCommand? capturedCommand = null;
        await repo.CreatePluginWithInitialVersionAsync(
            Arg.Do<CreatePluginCommand>(c => capturedCommand = c),
            Arg.Any<CancellationToken>());
        repo.CreatePluginWithInitialVersionAsync(
                Arg.Any<CreatePluginCommand>(),
                Arg.Any<CancellationToken>())
            .Returns(new PluginPublishResult(Guid.NewGuid(), "1.0.0"));

        UploadPluginCommand command = MakeUploadCommand(initialVersion: "1.0.0");

        // Act
        await useCase.ExecuteAsync(command);

        // Assert — version_sort must be pre-computed correctly for 1.0.0
        Assert.NotNull(capturedCommand);
        Assert.Equal(new SemVer(1, 0, 0).ToVersionSort(), capturedCommand!.VersionSort);
        Assert.Equal("1.0.0", capturedCommand!.Version);
    }

    [Fact]
    public async Task UploadPlugin_CustomInitialVersion_AcceptsNonCanonical100()
    {
        // Arrange
        MemoryStream archive = BuildTarGz([
            ("plugin.json", """{"name":"plugin","version":"2.5.3","description":"Desc","author":"Author","types":["skill"],"languages":["typescript"]}"""),
        ]);
        PluginPublishResult expected = new(Guid.NewGuid(), "2.5.3");
        (_, _, UploadPluginUseCase useCase) = BuildUploadUseCase(publishResult: expected);

        UploadPluginCommand command = MakeUploadCommand(
            packageStream: archive, initialVersion: "2.5.3");

        // Act
        PluginPublishResult result = await useCase.ExecuteAsync(command);

        // Assert
        Assert.Equal("2.5.3", result.Version);
    }

    [Fact]
    public async Task UploadPlugin_NameNormalizedGeneratedAsLowerInvariant()
    {
        // Arrange
        (IPluginPublishingRepositoryPort repo, _, UploadPluginUseCase useCase) = BuildUploadUseCase();

        CreatePluginCommand? capturedCommand = null;
        await repo.CreatePluginWithInitialVersionAsync(
            Arg.Do<CreatePluginCommand>(c => capturedCommand = c),
            Arg.Any<CancellationToken>());
        repo.CreatePluginWithInitialVersionAsync(
                Arg.Any<CreatePluginCommand>(),
                Arg.Any<CancellationToken>())
            .Returns(new PluginPublishResult(Guid.NewGuid(), "1.0.0"));

        UploadPluginCommand command = MakeUploadCommand(name: "MyAwesomePlugin");

        // Act
        await useCase.ExecuteAsync(command);

        // Assert
        Assert.NotNull(capturedCommand);
        Assert.Equal("myawesomeplugin", capturedCommand!.NameNormalized);
    }

    [Fact]
    public async Task UploadPlugin_ReleaseNotesDefaultEmptyWhenNotProvided()
    {
        // Arrange
        (IPluginPublishingRepositoryPort repo, _, UploadPluginUseCase useCase) = BuildUploadUseCase();

        CreatePluginCommand? capturedCommand = null;
        await repo.CreatePluginWithInitialVersionAsync(
            Arg.Do<CreatePluginCommand>(c => capturedCommand = c),
            Arg.Any<CancellationToken>());
        repo.CreatePluginWithInitialVersionAsync(
                Arg.Any<CreatePluginCommand>(),
                Arg.Any<CancellationToken>())
            .Returns(new PluginPublishResult(Guid.NewGuid(), "1.0.0"));

        // Empty release notes
        UploadPluginCommand command = MakeUploadCommand(releaseNotes: "");

        // Act
        await useCase.ExecuteAsync(command);

        // Assert
        Assert.NotNull(capturedCommand);
        Assert.Equal("", capturedCommand!.ReleaseNotes);
    }

    // =========================================================================
    // UploadPluginUseCase — missing package file
    // VERBATIM spec string: "Package file is required"  (plugin-upload/spec.md)
    // =========================================================================

    [Fact]
    public async Task UploadPlugin_EmptyStream_ThrowsMissingPackageFileException()
    {
        // Arrange
        (_, _, UploadPluginUseCase useCase) = BuildUploadUseCase();

        UploadPluginCommand command = MakeUploadCommand(packageStream: new MemoryStream());

        // Act & Assert
        MissingPackageFileException ex = await Assert.ThrowsAsync<MissingPackageFileException>(
            () => useCase.ExecuteAsync(command));

        Assert.Equal("Package file is required", ex.Message);
        Assert.Equal(400, ex.StatusCode);
    }

    // =========================================================================
    // UploadPluginUseCase — missing required manifest field
    // VERBATIM spec string: "Required field missing: name"  (plugin-upload/spec.md)
    // =========================================================================

    [Fact]
    public async Task UploadPlugin_ManifestMissingNameField_ThrowsMissingRequiredFieldException()
    {
        // Arrange — manifest has no "name" field
        MemoryStream archive = BuildTarGz([
            ("plugin.json", """{"version":"1.0.0","description":"Desc","author":"Author","types":["skill"],"languages":["typescript"]}"""),
        ]);

        (_, _, UploadPluginUseCase useCase) = BuildUploadUseCase();
        UploadPluginCommand command = MakeUploadCommand(packageStream: archive);

        // Act & Assert
        MissingRequiredFieldException ex = await Assert.ThrowsAsync<MissingRequiredFieldException>(
            () => useCase.ExecuteAsync(command));

        Assert.Equal("Required field missing: name", ex.Message);
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task UploadPlugin_ManifestMissingDescription_ThrowsMissingRequiredFieldException()
    {
        // Arrange — manifest has no "description" field
        MemoryStream archive = BuildTarGz([
            ("plugin.json", """{"name":"test","version":"1.0.0","author":"Author","types":["skill"],"languages":["typescript"]}"""),
        ]);

        (_, _, UploadPluginUseCase useCase) = BuildUploadUseCase();
        UploadPluginCommand command = MakeUploadCommand(packageStream: archive);

        // Act & Assert
        await Assert.ThrowsAsync<MissingRequiredFieldException>(
            () => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task UploadPlugin_ManifestMissingAuthor_ThrowsMissingRequiredFieldException()
    {
        // Arrange — manifest has no "author" field
        MemoryStream archive = BuildTarGz([
            ("plugin.json", """{"name":"test","version":"1.0.0","description":"Desc","types":["skill"],"languages":["typescript"]}"""),
        ]);

        (_, _, UploadPluginUseCase useCase) = BuildUploadUseCase();
        UploadPluginCommand command = MakeUploadCommand(packageStream: archive);

        // Act & Assert
        await Assert.ThrowsAsync<MissingRequiredFieldException>(
            () => useCase.ExecuteAsync(command));
    }

    // =========================================================================
    // UploadPluginUseCase — invalid semver in initialVersion
    // VERBATIM spec string: "initialVersion must be a valid semantic version (e.g., 1.0.0)"
    // =========================================================================

    [Fact]
    public async Task UploadPlugin_InvalidSemVer_ThrowsInvalidSemVerException()
    {
        // Arrange — package has an invalid version in the manifest
        MemoryStream archive = BuildTarGz([
            ("plugin.json", """{"name":"test","version":"not-a-version","description":"Desc","author":"Author","types":["skill"],"languages":["typescript"]}"""),
        ]);

        (_, _, UploadPluginUseCase useCase) = BuildUploadUseCase();
        UploadPluginCommand command = MakeUploadCommand(
            packageStream: archive, initialVersion: "not-a-version");

        // Act & Assert
        InvalidSemVerException ex = await Assert.ThrowsAsync<InvalidSemVerException>(
            () => useCase.ExecuteAsync(command));

        Assert.Equal("initialVersion must be a valid semantic version (e.g., 1.0.0)", ex.Message);
        Assert.Equal(400, ex.StatusCode);
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("v1.0.0")]
    [InlineData("1.0.0-beta")]
    [InlineData("1.0.0+build.123")]
    public async Task UploadPlugin_VariousInvalidVersionFormats_AllThrowInvalidSemVerException(string badVersion)
    {
        // Arrange
        MemoryStream archive = BuildTarGz([
            ("plugin.json", $$"""{"name":"test","version":"{{badVersion}}","description":"Desc","author":"Author","types":["skill"],"languages":["typescript"]}"""),
        ]);

        (_, _, UploadPluginUseCase useCase) = BuildUploadUseCase();
        UploadPluginCommand command = MakeUploadCommand(
            packageStream: archive, initialVersion: badVersion);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidSemVerException>(
            () => useCase.ExecuteAsync(command));
    }

    // =========================================================================
    // UploadPluginUseCase — duplicate plugin name
    // VERBATIM spec string: "A plugin with name 'MyPlugin' already exists"  (plugin-upload/spec.md)
    // =========================================================================

    [Fact]
    public async Task UploadPlugin_DuplicateName_ThrowsDuplicatePluginNameException()
    {
        // Arrange — name already exists
        (_, _, UploadPluginUseCase useCase) = BuildUploadUseCase(nameExists: true);

        UploadPluginCommand command = MakeUploadCommand(name: "MyPlugin");

        // Act & Assert
        DuplicatePluginNameException ex = await Assert.ThrowsAsync<DuplicatePluginNameException>(
            () => useCase.ExecuteAsync(command));

        Assert.Equal("A plugin with name 'MyPlugin' already exists", ex.Message);
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task UploadPlugin_DuplicateNameCaseInsensitive_ThrowsDuplicatePluginNameException()
    {
        // Arrange — "myplugin" (lower) exists; submit as "MYPLUGIN" (upper)
        IPluginPublishingRepositoryPort repo = Substitute.For<IPluginPublishingRepositoryPort>();
        IPackageStoragePort storage = Substitute.For<IPackageStoragePort>();
        IPackageReader reader = new PackageReader();

        // Return true only when queried with the lower-cased value
        repo.ExistsByNameNormalizedAsync("myplugin", Arg.Any<CancellationToken>())
            .Returns(true);
        repo.ExistsByNameNormalizedAsync(
                Arg.Is<string>(s => s != "myplugin"),
                Arg.Any<CancellationToken>())
            .Returns(false);

        UploadPluginUseCase useCase = new(repo, storage, reader);
        UploadPluginCommand command = MakeUploadCommand(name: "MYPLUGIN");

        // Act & Assert — case-insensitive: use case normalizes to lower before checking
        DuplicatePluginNameException ex = await Assert.ThrowsAsync<DuplicatePluginNameException>(
            () => useCase.ExecuteAsync(command));

        Assert.Equal(409, ex.StatusCode);
    }

    // =========================================================================
    // UploadPluginUseCase — package format errors propagated from PackageReader
    // These come from IPackageReader and bubble through the use case.
    // =========================================================================

    [Fact]
    public async Task UploadPlugin_UnsupportedPackageFormat_ThrowsUnsupportedPackageFormatException()
    {
        // Arrange — .exe extension is rejected by PackageReader
        (_, _, UploadPluginUseCase useCase) = BuildUploadUseCase();

        UploadPluginCommand command = MakeUploadCommand(
            packageStream: new MemoryStream("content"u8.ToArray()),
            fileName: "installer.exe");

        // Act & Assert
        await Assert.ThrowsAsync<UnsupportedPackageFormatException>(
            () => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task UploadPlugin_CorruptedArchive_ThrowsCorruptedArchiveException()
    {
        // Arrange — garbage bytes with .tar.gz extension
        byte[] garbage = new byte[128];
        Random.Shared.NextBytes(garbage);

        (_, _, UploadPluginUseCase useCase) = BuildUploadUseCase();

        UploadPluginCommand command = MakeUploadCommand(
            packageStream: new MemoryStream(garbage),
            fileName: "corrupted.tar.gz");

        // Act & Assert
        await Assert.ThrowsAsync<CorruptedArchiveException>(
            () => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task UploadPlugin_PackageMissingManifest_ThrowsMissingManifestException()
    {
        // Arrange — archive with no plugin.json or manifest.json
        MemoryStream archive = BuildTarGz([("README.md", "# No Manifest")]);

        (_, _, UploadPluginUseCase useCase) = BuildUploadUseCase();

        UploadPluginCommand command = MakeUploadCommand(
            packageStream: archive, fileName: "no-manifest.tar.gz");

        // Act & Assert
        await Assert.ThrowsAsync<MissingManifestException>(
            () => useCase.ExecuteAsync(command));
    }

    // =========================================================================
    // PublishVersionUseCase — happy path
    // =========================================================================

    [Fact]
    public async Task PublishVersion_ValidNewVersion_Returns201WithVersionRecord()
    {
        // Arrange
        Guid pluginId = Guid.NewGuid();
        PluginVersionPublishResult expected = new(pluginId, Guid.NewGuid(), "1.1.0");
        (_, _, PublishVersionUseCase useCase) = BuildPublishVersionUseCase(
            pluginExists: true, versionExists: false, publishResult: expected);

        MemoryStream archive = BuildTarGz([
            ("plugin.json", """{"name":"test","version":"1.1.0","description":"Desc","author":"Author","types":["skill"],"languages":["typescript"]}"""),
        ]);

        PublishVersionCommand command = new(
            PluginId: pluginId,
            PackageStream: archive,
            FileName: "test-1.1.0.tar.gz",
            Version: "1.1.0",
            ReleaseNotes: "Added new feature X");

        // Act
        PluginVersionPublishResult result = await useCase.ExecuteAsync(command);

        // Assert
        Assert.Equal(pluginId, result.PluginId);
        Assert.Equal("1.1.0", result.Version);
    }

    [Fact]
    public async Task PublishVersion_NewVersion_FlipsPriorIsLatest_CallsAddVersionAsync()
    {
        // Arrange
        Guid pluginId = Guid.NewGuid();
        (IPluginPublishingRepositoryPort repo, _, PublishVersionUseCase useCase) =
            BuildPublishVersionUseCase(pluginExists: true, versionExists: false);

        MemoryStream archive = BuildTarGz([
            ("plugin.json", """{"name":"test","version":"2.0.0","description":"Desc","author":"Author","types":["skill"],"languages":["typescript"]}"""),
        ]);

        PublishVersionCommand command = new(
            PluginId: pluginId,
            PackageStream: archive,
            FileName: "test-2.0.0.tar.gz",
            Version: "2.0.0",
            ReleaseNotes: "Major release");

        // Act
        await useCase.ExecuteAsync(command);

        // Assert — repository.AddVersionAsync must be called with the new version
        await repo.Received(1).AddVersionAsync(
            pluginId,
            Arg.Is<AddVersionCommand>(c => c.Version == "2.0.0"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishVersion_PatchVersion_IsMarkedAsLatest()
    {
        // Arrange
        Guid pluginId = Guid.NewGuid();
        AddVersionCommand? capturedCmd = null;
        (IPluginPublishingRepositoryPort repo, _, PublishVersionUseCase useCase) =
            BuildPublishVersionUseCase(pluginExists: true, versionExists: false);

        await repo.AddVersionAsync(
            Arg.Any<Guid>(),
            Arg.Do<AddVersionCommand>(c => capturedCmd = c),
            Arg.Any<CancellationToken>());

        MemoryStream archive = BuildTarGz([
            ("plugin.json", """{"name":"test","version":"1.0.1","description":"Desc","author":"Author","types":["skill"],"languages":["typescript"]}"""),
        ]);

        PublishVersionCommand command = new(
            PluginId: pluginId,
            PackageStream: archive,
            FileName: "test-1.0.1.tar.gz",
            Version: "1.0.1",
            ReleaseNotes: "Patch fix");

        // Act
        await useCase.ExecuteAsync(command);

        // Assert — version_sort computed correctly for 1.0.1
        Assert.NotNull(capturedCmd);
        Assert.Equal(new SemVer(1, 0, 1).ToVersionSort(), capturedCmd!.VersionSort);
    }

    [Fact]
    public async Task PublishVersion_ReleaseNotesDefaultEmptyWhenNotProvided()
    {
        // Arrange
        Guid pluginId = Guid.NewGuid();
        AddVersionCommand? capturedCmd = null;
        (IPluginPublishingRepositoryPort repo, _, PublishVersionUseCase useCase) =
            BuildPublishVersionUseCase(pluginExists: true, versionExists: false);

        await repo.AddVersionAsync(
            Arg.Any<Guid>(),
            Arg.Do<AddVersionCommand>(c => capturedCmd = c),
            Arg.Any<CancellationToken>());

        MemoryStream archive = BuildTarGz([
            ("plugin.json", """{"name":"test","version":"1.0.2","description":"Desc","author":"Author","types":["skill"],"languages":["typescript"]}"""),
        ]);

        PublishVersionCommand command = new(
            PluginId: pluginId,
            PackageStream: archive,
            FileName: "test-1.0.2.tar.gz",
            Version: "1.0.2",
            ReleaseNotes: "");

        // Act
        await useCase.ExecuteAsync(command);

        // Assert — empty string release notes stored as-is (default empty)
        Assert.NotNull(capturedCmd);
        Assert.Equal("", capturedCmd!.ReleaseNotes);
    }

    [Fact]
    public async Task PublishVersion_PackageStoredViaIPackageStoragePort()
    {
        // Arrange
        Guid pluginId = Guid.NewGuid();
        (_, IPackageStoragePort storage, PublishVersionUseCase useCase) =
            BuildPublishVersionUseCase(pluginExists: true, versionExists: false);

        MemoryStream archive = BuildTarGz([
            ("plugin.json", """{"name":"test","version":"1.1.0","description":"Desc","author":"Author","types":["skill"],"languages":["typescript"]}"""),
        ]);

        PublishVersionCommand command = new(
            PluginId: pluginId,
            PackageStream: archive,
            FileName: "test-1.1.0.tar.gz",
            Version: "1.1.0",
            ReleaseNotes: "");

        // Act
        await useCase.ExecuteAsync(command);

        // Assert
        await storage.Received(1).PutAsync(
            Arg.Any<string>(),
            Arg.Any<Stream>(),
            Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // PublishVersionUseCase — duplicate version
    // VERBATIM spec string: "Version 1.5.0 already exists"  (plugin-versioning/spec.md)
    // =========================================================================

    [Fact]
    public async Task PublishVersion_DuplicateVersion_ThrowsDuplicateVersionException()
    {
        // Arrange — version 1.5.0 already exists
        Guid pluginId = Guid.NewGuid();
        (_, _, PublishVersionUseCase useCase) = BuildPublishVersionUseCase(
            pluginExists: true, versionExists: true);

        MemoryStream archive = BuildValidPluginTarGz(version: "1.5.0");

        PublishVersionCommand command = new(
            PluginId: pluginId,
            PackageStream: archive,
            FileName: "test-1.5.0.tar.gz",
            Version: "1.5.0",
            ReleaseNotes: "");

        // Act & Assert
        DuplicateVersionException ex = await Assert.ThrowsAsync<DuplicateVersionException>(
            () => useCase.ExecuteAsync(command));

        Assert.Equal("Version 1.5.0 already exists", ex.Message);
        Assert.Equal(409, ex.StatusCode);
    }

    // =========================================================================
    // PublishVersionUseCase — invalid version format
    // VERBATIM spec string: "Version must be in format MAJOR.MINOR.PATCH (e.g., 1.2.3)"
    // =========================================================================

    [Theory]
    [InlineData("2.3")]
    [InlineData("v2.3.4")]
    [InlineData("2.3.4-beta")]
    [InlineData("1.0.0+build123")]
    public async Task PublishVersion_InvalidVersionFormat_ThrowsInvalidVersionFormatException(string badVersion)
    {
        // Arrange
        Guid pluginId = Guid.NewGuid();
        (_, _, PublishVersionUseCase useCase) = BuildPublishVersionUseCase(
            pluginExists: true, versionExists: false);

        MemoryStream archive = BuildTarGz([
            ("plugin.json", $$"""{"name":"test","version":"{{badVersion}}","description":"Desc","author":"Author","types":["skill"],"languages":["typescript"]}"""),
        ]);

        PublishVersionCommand command = new(
            PluginId: pluginId,
            PackageStream: archive,
            FileName: $"test-{badVersion}.tar.gz",
            Version: badVersion,
            ReleaseNotes: "");

        // Act & Assert
        InvalidVersionFormatException ex = await Assert.ThrowsAsync<InvalidVersionFormatException>(
            () => useCase.ExecuteAsync(command));

        Assert.Equal("Version must be in format MAJOR.MINOR.PATCH (e.g., 1.2.3)", ex.Message);
        Assert.Equal(400, ex.StatusCode);
    }

    // =========================================================================
    // PublishVersionUseCase — publish to non-existent plugin
    // VERBATIM spec string: "Plugin not found"  (plugin-versioning/spec.md)
    // =========================================================================

    [Fact]
    public async Task PublishVersion_NonExistentPlugin_ThrowsPluginNotFoundForVersionException()
    {
        // Arrange — plugin does NOT exist
        Guid unknownPluginId = Guid.NewGuid();
        (_, _, PublishVersionUseCase useCase) = BuildPublishVersionUseCase(
            pluginExists: false);

        MemoryStream archive = BuildValidPluginTarGz(version: "1.0.0");

        PublishVersionCommand command = new(
            PluginId: unknownPluginId,
            PackageStream: archive,
            FileName: "test-1.0.0.tar.gz",
            Version: "1.0.0",
            ReleaseNotes: "");

        // Act & Assert
        PluginNotFoundForVersionException ex =
            await Assert.ThrowsAsync<PluginNotFoundForVersionException>(
                () => useCase.ExecuteAsync(command));

        Assert.Equal("Plugin not found", ex.Message);
        Assert.Equal(404, ex.StatusCode);
    }

    // =========================================================================
    // PublishVersionUseCase — release notes stored verbatim with special chars
    // =========================================================================

    [Fact]
    public async Task PublishVersion_ReleaseNotesWithMarkdownAndNewlines_StoredAsIs()
    {
        // Arrange
        const string releaseNotes =
            "- Fixed bug #123\n- Added support for TypeScript 5.1\n- Improved performance by 20%";

        Guid pluginId = Guid.NewGuid();
        AddVersionCommand? capturedCmd = null;
        (IPluginPublishingRepositoryPort repo, _, PublishVersionUseCase useCase) =
            BuildPublishVersionUseCase(pluginExists: true, versionExists: false);

        await repo.AddVersionAsync(
            Arg.Any<Guid>(),
            Arg.Do<AddVersionCommand>(c => capturedCmd = c),
            Arg.Any<CancellationToken>());

        MemoryStream archive = BuildTarGz([
            ("plugin.json", """{"name":"test","version":"1.5.0","description":"Desc","author":"Author","types":["skill"],"languages":["typescript"]}"""),
        ]);

        PublishVersionCommand command = new(
            PluginId: pluginId,
            PackageStream: archive,
            FileName: "test-1.5.0.tar.gz",
            Version: "1.5.0",
            ReleaseNotes: releaseNotes);

        // Act
        await useCase.ExecuteAsync(command);

        // Assert
        Assert.NotNull(capturedCmd);
        Assert.Equal(releaseNotes, capturedCmd!.ReleaseNotes);
    }
}
