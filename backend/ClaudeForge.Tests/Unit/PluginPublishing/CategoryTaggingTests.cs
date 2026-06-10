using System.Formats.Tar;
using System.IO.Compression;
using ClaudeForge.Application.Modules.PluginPublishing.Ports;
using ClaudeForge.Application.Modules.PluginPublishing.UseCases;
using ClaudeForge.Core.Domain.Packaging;
using ClaudeForge.Core.Ports;
using NSubstitute;

namespace ClaudeForge.Tests.Unit.PluginPublishing;

/// <summary>
/// Unit tests for category-tag validation in <see cref="UploadPluginUseCase"/>.
///
/// Verifies:
///   - Multiple use-case tags are all passed to the repository as resolved IDs.
///   - Known tags (present in vocabulary) are accepted and persisted.
///   - Unknown tags are rejected with <see cref="UnknownCategoryTagException"/> that names
///     the invalid value(s).
/// </summary>
public sealed class CategoryTaggingTests
{
    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static readonly IReadOnlyDictionary<string, short> KnownVocabulary =
        new Dictionary<string, short>
        {
            ["type:skill"]           = 1,
            ["type:agent"]           = 2,
            ["language:typescript"]  = 6,
            ["language:python"]      = 7,
            ["use_case:dev-team"]    = 10,
            ["use_case:devops"]      = 13,
            ["use_case:engineering"] = 17,
            ["use_case:product"]     = 18,
            ["use_case:ux-ui"]       = 19,
        };

    private static ICategoryLookupPort MakeLookup(
        IReadOnlyDictionary<string, short>? vocab = null)
    {
        ICategoryLookupPort port = Substitute.For<ICategoryLookupPort>();
        port.GetAllCategoryKeysAsync(Arg.Any<CancellationToken>())
            .Returns(vocab ?? KnownVocabulary);
        return port;
    }

    private static IPluginPublishingRepositoryPort MakeRepo()
    {
        Guid pluginId = Guid.NewGuid();
        IPluginPublishingRepositoryPort repo = Substitute.For<IPluginPublishingRepositoryPort>();
        repo.ExistsByNameNormalizedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        repo.CreatePluginWithInitialVersionAsync(
                Arg.Any<CreatePluginCommand>(), Arg.Any<CancellationToken>())
            .Returns(new PluginPublishResult(pluginId, "1.0.0"));
        return repo;
    }

    private static IPackageStoragePort MakeStorage() =>
        Substitute.For<IPackageStoragePort>();

    /// <summary>
    /// Builds a minimal valid .tar.gz package with a manifest and README.
    /// </summary>
    private static Stream BuildMinimalTarGz(
        string name = "TestPlugin",
        string description = "A test plugin",
        string author = "tester")
    {
        MemoryStream tarBuffer = new();

        using (GZipStream gzip = new(tarBuffer, CompressionLevel.Fastest, leaveOpen: true))
        using (TarWriter tar = new(gzip, TarEntryFormat.Pax, leaveOpen: false))
        {
            string manifestJson = $$$"""
                {
                    "name": "{{{name}}}",
                    "description": "{{{description}}}",
                    "author": "{{{author}}}"
                }
                """;
            byte[] manifestBytes = System.Text.Encoding.UTF8.GetBytes(manifestJson);

            PaxTarEntry manifestEntry = new(TarEntryType.RegularFile, "plugin.json")
            {
                DataStream = new MemoryStream(manifestBytes),
            };
            tar.WriteEntry(manifestEntry);
        }

        tarBuffer.Position = 0;
        return tarBuffer;
    }

    private static UploadPluginUseCase MakeUseCase(
        IPluginPublishingRepositoryPort? repo = null,
        IPackageStoragePort? storage = null,
        ICategoryLookupPort? lookup = null)
    {
        IPackageReader packageReader = new ClaudeForge.Infrastructure.Packaging.PackageReader();
        return new UploadPluginUseCase(
            repo ?? MakeRepo(),
            storage ?? MakeStorage(),
            packageReader,
            new AnonymousCurrentUser(),
            new NoOpMembershipQueryPort(),
            new ClaudeForge.Core.Shared.Authorization.PluginAccessPolicy(),
            lookup ?? MakeLookup());
    }

    // ─── Tests: multiple use-case tags persisted ───────────────────────────────

    [Fact]
    public async Task Upload_MultipleUseCaseTags_AllResolvedIdsPassedToRepository()
    {
        // Arrange
        IPluginPublishingRepositoryPort repo = MakeRepo();
        ICategoryLookupPort lookup = MakeLookup();
        UploadPluginUseCase useCase = MakeUseCase(repo: repo, lookup: lookup);

        using Stream pkg = BuildMinimalTarGz();
        UploadPluginCommand command = new(
            PackageStream: pkg,
            FileName: "plugin.tar.gz",
            Name: "Multi Tag Plugin",
            Description: "desc",
            Author: "author",
            InitialVersion: "1.0.0",
            ReleaseNotes: "",
            UseCaseTags: ["dev-team", "devops", "engineering"]);

        // Act
        await useCase.ExecuteAsync(command);

        // Assert: repository received a command with all three use-case IDs
        await repo.Received(1).CreatePluginWithInitialVersionAsync(
            Arg.Is<CreatePluginCommand>(cmd =>
                cmd.ResolvedCategoryIds != null &&
                cmd.ResolvedCategoryIds.Contains((short)10) &&  // dev-team
                cmd.ResolvedCategoryIds.Contains((short)13) &&  // devops
                cmd.ResolvedCategoryIds.Contains((short)17)),   // engineering
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_MultipleUseCaseTags_AllTagsRetained_NotJustFirst()
    {
        // Arrange
        IPluginPublishingRepositoryPort repo = MakeRepo();
        ICategoryLookupPort lookup = MakeLookup();
        UploadPluginUseCase useCase = MakeUseCase(repo: repo, lookup: lookup);

        using Stream pkg = BuildMinimalTarGz();
        UploadPluginCommand command = new(
            PackageStream: pkg,
            FileName: "plugin.tar.gz",
            Name: "Two Tag Plugin",
            Description: "desc",
            Author: "author",
            InitialVersion: "1.0.0",
            ReleaseNotes: "",
            UseCaseTags: ["product", "ux-ui"]);

        // Act
        await useCase.ExecuteAsync(command);

        // Assert: exactly 2 IDs resolved (product=18, ux-ui=19)
        await repo.Received(1).CreatePluginWithInitialVersionAsync(
            Arg.Is<CreatePluginCommand>(cmd =>
                cmd.ResolvedCategoryIds != null &&
                cmd.ResolvedCategoryIds.Count == 2 &&
                cmd.ResolvedCategoryIds.Contains((short)18) &&
                cmd.ResolvedCategoryIds.Contains((short)19)),
            Arg.Any<CancellationToken>());
    }

    // ─── Tests: known tags accepted ───────────────────────────────────────────

    [Fact]
    public async Task Upload_KnownTagsInAllDimensions_Succeeds()
    {
        // Arrange
        IPluginPublishingRepositoryPort repo = MakeRepo();
        UploadPluginUseCase useCase = MakeUseCase(repo: repo);

        using Stream pkg = BuildMinimalTarGz();
        UploadPluginCommand command = new(
            PackageStream: pkg,
            FileName: "plugin.tar.gz",
            Name: "Known Tags Plugin",
            Description: "desc",
            Author: "author",
            InitialVersion: "1.0.0",
            ReleaseNotes: "",
            Types: ["skill"],
            Languages: ["typescript"],
            UseCaseTags: ["dev-team"]);

        // Act — should not throw
        PluginPublishResult result = await useCase.ExecuteAsync(command);

        // Assert
        Assert.NotNull(result);
        await repo.Received(1).CreatePluginWithInitialVersionAsync(
            Arg.Is<CreatePluginCommand>(cmd =>
                cmd.ResolvedCategoryIds != null &&
                cmd.ResolvedCategoryIds.Contains((short)1) &&   // skill
                cmd.ResolvedCategoryIds.Contains((short)6) &&   // typescript
                cmd.ResolvedCategoryIds.Contains((short)10)),   // dev-team
            Arg.Any<CancellationToken>());
    }

    // ─── Tests: unknown tag rejected with validation error ────────────────────

    [Fact]
    public async Task Upload_UnknownUseCaseTag_ThrowsUnknownCategoryTagException()
    {
        // Arrange
        UploadPluginUseCase useCase = MakeUseCase();

        using Stream pkg = BuildMinimalTarGz();
        UploadPluginCommand command = new(
            PackageStream: pkg,
            FileName: "plugin.tar.gz",
            Name: "Bad Tags Plugin",
            Description: "desc",
            Author: "author",
            InitialVersion: "1.0.0",
            ReleaseNotes: "",
            UseCaseTags: ["nonexistent-tag"]);

        // Act & Assert
        UnknownCategoryTagException ex = await Assert.ThrowsAsync<UnknownCategoryTagException>(
            () => useCase.ExecuteAsync(command));

        Assert.Contains("nonexistent-tag", ex.Message);
        Assert.Contains("use_case", ex.Message);
    }

    [Fact]
    public async Task Upload_UnknownTypeTag_ThrowsUnknownCategoryTagExceptionNamingInvalidValue()
    {
        // Arrange
        UploadPluginUseCase useCase = MakeUseCase();

        using Stream pkg = BuildMinimalTarGz();
        UploadPluginCommand command = new(
            PackageStream: pkg,
            FileName: "plugin.tar.gz",
            Name: "Bad Type Plugin",
            Description: "desc",
            Author: "author",
            InitialVersion: "1.0.0",
            ReleaseNotes: "",
            Types: ["widget"]);

        // Act & Assert
        UnknownCategoryTagException ex = await Assert.ThrowsAsync<UnknownCategoryTagException>(
            () => useCase.ExecuteAsync(command));

        // The exception message must name the invalid value
        Assert.Contains("widget", ex.Message);
        Assert.Contains("type", ex.Message);
    }

    [Fact]
    public async Task Upload_MultipleInvalidTags_ExceptionNamesAll()
    {
        // Arrange
        UploadPluginUseCase useCase = MakeUseCase();

        using Stream pkg = BuildMinimalTarGz();
        UploadPluginCommand command = new(
            PackageStream: pkg,
            FileName: "plugin.tar.gz",
            Name: "Multi Bad Plugin",
            Description: "desc",
            Author: "author",
            InitialVersion: "1.0.0",
            ReleaseNotes: "",
            UseCaseTags: ["bad-one", "bad-two"]);

        // Act & Assert
        UnknownCategoryTagException ex = await Assert.ThrowsAsync<UnknownCategoryTagException>(
            () => useCase.ExecuteAsync(command));

        Assert.Contains("bad-one", ex.Message);
        Assert.Contains("bad-two", ex.Message);
    }
}

// ─── Minimal ICurrentUser / IOrgMembershipQueryPort stubs for test constructor ─

file sealed class AnonymousCurrentUser : ClaudeForge.Core.Shared.Authorization.ICurrentUser
{
    public bool IsAuthenticated => false;
    public Guid? UserId => null;
    public string? Email => null;
}

file sealed class NoOpMembershipQueryPort : ClaudeForge.Core.Shared.Authorization.IOrgMembershipQueryPort
{
    public Task<Guid[]> GetOrgIdsForUserAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult(Array.Empty<Guid>());

    public Task<bool> IsMemberAsync(Guid userId, Guid orgId, string? minRole = null, CancellationToken ct = default)
        => Task.FromResult(false);

    public void InvalidateUser(Guid userId) { }
}
