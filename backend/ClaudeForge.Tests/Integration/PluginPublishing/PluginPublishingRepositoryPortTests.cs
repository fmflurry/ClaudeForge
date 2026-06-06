using ClaudeForge.Application.Modules.PluginPublishing.Ports;
using ClaudeForge.Core.Domain.Plugins;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using ClaudeForge.Infrastructure.PluginPublishing;
using ClaudeForge.Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Tests.Integration.PluginPublishing;

/// <summary>
/// Integration tests for Group 5: IPluginPublishingRepositoryPort adapter.
///
/// Runs against a REAL PostgreSQL 16 container via Testcontainers.
/// Docker must be running on the test host.
///
/// Expected production types (coder MUST match these names exactly):
///
///   ClaudeForge.Infrastructure.PluginPublishing.PluginPublishingRepositoryAdapter
///     PluginPublishingRepositoryAdapter(MarketplaceDbContext context)
///     implements IPluginPublishingRepositoryPort
///
///   ClaudeForge.Application.Modules.PluginPublishing.Ports.IPluginPublishingRepositoryPort
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
///     Task&lt;(IReadOnlyList&lt;VersionHistoryDto&gt; Items, int TotalCount)&gt; GetVersionHistoryAsync(
///         Guid pluginId, PaginationRequest pagination, CancellationToken ct = default)
///     Task&lt;VersionDetailDto?&gt; GetVersionAsync(
///         Guid pluginId, string version, CancellationToken ct = default)
///
///   VersionHistoryDto (record in Ports ns)
///     Guid Id, string Version, long VersionSort, bool IsLatest,
///     DateTimeOffset ReleasedAt, string ReleaseNotes, long DownloadCount
///
///   VersionDetailDto (record in Ports ns)
///     Guid Id, Guid PluginId, string Version, bool IsLatest,
///     DateTimeOffset ReleasedAt, string ReleaseNotes,
///     long DownloadCount, long SizeBytes, string Sha256, string PackageFormat
/// </summary>
[Collection(PostgresFixture.CollectionName)]
public sealed class PluginPublishingRepositoryPortTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public PluginPublishingRepositoryPortTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // -------------------------------------------------------------------------
    // Per-test isolation: truncate all marketplace tables before each test.
    // -------------------------------------------------------------------------

    public async Task InitializeAsync()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        await ctx.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE TABLE
                telemetry_aggregates,
                telemetry_events,
                plugin_categories,
                plugin_versions,
                plugins,
                categories
            RESTART IDENTITY CASCADE
            """);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // -------------------------------------------------------------------------
    // Helper: build a CreatePluginCommand
    // -------------------------------------------------------------------------

    private static CreatePluginCommand MakeCreateCommand(
        string name = "TestPlugin",
        string version = "1.0.0",
        string releaseNotes = "Initial release",
        string? readmeText = null)
    {
        SemVer semver = SemVer.Parse(version);
        Guid tempId = Guid.NewGuid(); // placeholder for key generation
        return new CreatePluginCommand(
            Name: name,
            NameNormalized: name.ToLowerInvariant(),
            Slug: name.ToLowerInvariant().Replace(" ", "-"),
            Description: $"Description for {name}",
            Author: "Test Author",
            Version: version,
            VersionSort: semver.ToVersionSort(),
            PackageKey: $"plugins/{tempId}/{version}/package.tar.gz",
            PackageFormat: "tar.gz",
            SizeBytes: 1024,
            Sha256: new string('a', 64),
            ReleaseNotes: releaseNotes,
            ReadmeText: readmeText);
    }

    private static AddVersionCommand MakeAddVersionCommand(
        Guid pluginId,
        string version = "1.1.0",
        string releaseNotes = "New version",
        string? readmeText = null)
    {
        SemVer semver = SemVer.Parse(version);
        return new AddVersionCommand(
            Version: version,
            VersionSort: semver.ToVersionSort(),
            PackageKey: $"plugins/{pluginId}/{version}/package.tar.gz",
            PackageFormat: "tar.gz",
            SizeBytes: 2048,
            Sha256: new string('b', 64),
            ReleaseNotes: releaseNotes,
            ReadmeText: readmeText);
    }

    // -------------------------------------------------------------------------
    // CreatePluginWithInitialVersionAsync — persists plugin + version correctly
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreatePluginWithInitialVersionAsync_ValidCommand_PersistsPluginAndVersion()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo = new PluginPublishingRepositoryAdapter(ctx);
        CreatePluginCommand command = MakeCreateCommand(name: "NewPlugin", version: "1.0.0");

        // Act
        PluginPublishResult result = await repo.CreatePluginWithInitialVersionAsync(command);

        // Assert
        Assert.NotEqual(Guid.Empty, result.PluginId);
        Assert.Equal("1.0.0", result.Version);

        // Verify persisted in DB
        await using MarketplaceDbContext verifyCtx = _fixture.CreateContext();
        PluginEntity? plugin = await verifyCtx.Plugins
            .Include(p => p.Versions)
            .FirstOrDefaultAsync(p => p.Id == result.PluginId);

        Assert.NotNull(plugin);
        Assert.Equal("NewPlugin", plugin.Name);
        Assert.Equal("newplugin", plugin.NameNormalized);
        Assert.Single(plugin.Versions);

        PluginVersionEntity version = plugin.Versions.First();
        Assert.Equal("1.0.0", version.Version);
        Assert.True(version.IsLatest);
        Assert.Equal(new SemVer(1, 0, 0).ToVersionSort(), version.VersionSort);
    }

    [Fact]
    public async Task CreatePluginWithInitialVersionAsync_InitialVersionIsLatestTrue()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo = new PluginPublishingRepositoryAdapter(ctx);
        CreatePluginCommand command = MakeCreateCommand(name: "IsLatestPlugin", version: "1.0.0");

        // Act
        PluginPublishResult result = await repo.CreatePluginWithInitialVersionAsync(command);

        // Assert
        await using MarketplaceDbContext verifyCtx = _fixture.CreateContext();
        PluginVersionEntity? version = await verifyCtx.PluginVersions
            .FirstOrDefaultAsync(v => v.PluginId == result.PluginId);

        Assert.NotNull(version);
        Assert.True(version.IsLatest);
    }

    [Fact]
    public async Task CreatePluginWithInitialVersionAsync_WithReadme_PersistedToReadmeText()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo = new PluginPublishingRepositoryAdapter(ctx);
        CreatePluginCommand command = MakeCreateCommand(
            name: "ReadmePlugin",
            version: "1.0.0",
            readmeText: "# My Plugin README");

        // Act
        PluginPublishResult result = await repo.CreatePluginWithInitialVersionAsync(command);

        // Assert
        await using MarketplaceDbContext verifyCtx = _fixture.CreateContext();
        PluginVersionEntity? version = await verifyCtx.PluginVersions
            .FirstOrDefaultAsync(v => v.PluginId == result.PluginId);

        Assert.NotNull(version);
        Assert.Equal("# My Plugin README", version.ReadmeText);
    }

    [Fact]
    public async Task CreatePluginWithInitialVersionAsync_NameNormalizedAndSlugPersisted()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo = new PluginPublishingRepositoryAdapter(ctx);
        CreatePluginCommand command = MakeCreateCommand(name: "MyPlugin") with
        {
            NameNormalized = "myplugin",
            Slug = "myplugin",
        };

        // Act
        PluginPublishResult result = await repo.CreatePluginWithInitialVersionAsync(command);

        // Assert
        await using MarketplaceDbContext verifyCtx = _fixture.CreateContext();
        PluginEntity? plugin = await verifyCtx.Plugins.FindAsync(result.PluginId);

        Assert.NotNull(plugin);
        Assert.Equal("myplugin", plugin.NameNormalized);
        Assert.Equal("myplugin", plugin.Slug);
    }

    // -------------------------------------------------------------------------
    // AddVersionAsync — flips single is_latest invariant atomically
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AddVersionAsync_NewVersion_FlipsPriorIsLatestToFalse()
    {
        // Arrange — create plugin with v1.0.0 as latest
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo = new PluginPublishingRepositoryAdapter(ctx);
        CreatePluginCommand createCmd = MakeCreateCommand(name: "FlipPlugin", version: "1.0.0");
        PluginPublishResult created = await repo.CreatePluginWithInitialVersionAsync(createCmd);

        // Act — add v1.1.0
        await using MarketplaceDbContext ctx2 = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo2 = new PluginPublishingRepositoryAdapter(ctx2);
        AddVersionCommand addCmd = MakeAddVersionCommand(created.PluginId, version: "1.1.0");
        await repo2.AddVersionAsync(created.PluginId, addCmd);

        // Assert — only 1.1.0 must have isLatest=true
        await using MarketplaceDbContext verifyCtx = _fixture.CreateContext();
        List<PluginVersionEntity> versions = await verifyCtx.PluginVersions
            .Where(v => v.PluginId == created.PluginId)
            .ToListAsync();

        Assert.Equal(2, versions.Count);

        PluginVersionEntity? v100 = versions.FirstOrDefault(v => v.Version == "1.0.0");
        PluginVersionEntity? v110 = versions.FirstOrDefault(v => v.Version == "1.1.0");

        Assert.NotNull(v100);
        Assert.NotNull(v110);
        Assert.False(v100.IsLatest);
        Assert.True(v110.IsLatest);
    }

    [Fact]
    public async Task AddVersionAsync_MultipleVersions_OnlyOneIsLatestAtAllTimes()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo = new PluginPublishingRepositoryAdapter(ctx);
        CreatePluginCommand createCmd = MakeCreateCommand(name: "MultiVersionPlugin", version: "1.0.0");
        PluginPublishResult created = await repo.CreatePluginWithInitialVersionAsync(createCmd);

        // Act — publish 1.1.0, then 2.0.0
        await using MarketplaceDbContext ctx2 = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo2 = new PluginPublishingRepositoryAdapter(ctx2);
        await repo2.AddVersionAsync(created.PluginId,
            MakeAddVersionCommand(created.PluginId, version: "1.1.0"));

        await using MarketplaceDbContext ctx3 = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo3 = new PluginPublishingRepositoryAdapter(ctx3);
        await repo3.AddVersionAsync(created.PluginId,
            MakeAddVersionCommand(created.PluginId, version: "2.0.0"));

        // Assert — only 2.0.0 has isLatest=true
        await using MarketplaceDbContext verifyCtx = _fixture.CreateContext();
        List<PluginVersionEntity> versions = await verifyCtx.PluginVersions
            .Where(v => v.PluginId == created.PluginId)
            .ToListAsync();

        Assert.Equal(3, versions.Count);
        int latestCount = versions.Count(v => v.IsLatest);
        Assert.Equal(1, latestCount);

        PluginVersionEntity? latest = versions.FirstOrDefault(v => v.IsLatest);
        Assert.NotNull(latest);
        Assert.Equal("2.0.0", latest.Version);
    }

    // -------------------------------------------------------------------------
    // Duplicate (plugin_id, version) must be rejected (DB constraint)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AddVersionAsync_DuplicateVersion_ThrowsAtDatabaseLevel()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo = new PluginPublishingRepositoryAdapter(ctx);
        CreatePluginCommand createCmd = MakeCreateCommand(name: "DupVersionPlugin", version: "1.0.0");
        PluginPublishResult created = await repo.CreatePluginWithInitialVersionAsync(createCmd);

        // Act — attempt to add the same version again
        await using MarketplaceDbContext ctx2 = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo2 = new PluginPublishingRepositoryAdapter(ctx2);
        AddVersionCommand dupCmd = MakeAddVersionCommand(created.PluginId, version: "1.0.0");

        // Assert — DB UNIQUE(plugin_id, version) constraint fires
        await Assert.ThrowsAnyAsync<Exception>(
            () => repo2.AddVersionAsync(created.PluginId, dupCmd));
    }

    // -------------------------------------------------------------------------
    // ExistsByNameNormalizedAsync — case-insensitive duplicate detection
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExistsByNameNormalizedAsync_ExistingName_ReturnsTrue()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo = new PluginPublishingRepositoryAdapter(ctx);
        await repo.CreatePluginWithInitialVersionAsync(
            MakeCreateCommand(name: "ExistsPlugin"));

        // Act
        await using MarketplaceDbContext ctx2 = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo2 = new PluginPublishingRepositoryAdapter(ctx2);
        bool exists = await repo2.ExistsByNameNormalizedAsync("existsplugin");

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsByNameNormalizedAsync_NonExistentName_ReturnsFalse()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo = new PluginPublishingRepositoryAdapter(ctx);

        // Act
        bool exists = await repo.ExistsByNameNormalizedAsync("nonexistentplugin");

        // Assert
        Assert.False(exists);
    }

    // -------------------------------------------------------------------------
    // PluginExistsAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PluginExistsAsync_ExistingId_ReturnsTrue()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo = new PluginPublishingRepositoryAdapter(ctx);
        PluginPublishResult created = await repo.CreatePluginWithInitialVersionAsync(
            MakeCreateCommand(name: "PluginExistsPlugin"));

        // Act
        await using MarketplaceDbContext ctx2 = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo2 = new PluginPublishingRepositoryAdapter(ctx2);
        bool exists = await repo2.PluginExistsAsync(created.PluginId);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task PluginExistsAsync_UnknownId_ReturnsFalse()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo = new PluginPublishingRepositoryAdapter(ctx);

        // Act
        bool exists = await repo.PluginExistsAsync(Guid.NewGuid());

        // Assert
        Assert.False(exists);
    }

    // -------------------------------------------------------------------------
    // VersionExistsAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task VersionExistsAsync_ExistingVersion_ReturnsTrue()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo = new PluginPublishingRepositoryAdapter(ctx);
        PluginPublishResult created = await repo.CreatePluginWithInitialVersionAsync(
            MakeCreateCommand(name: "VersionCheckPlugin", version: "1.0.0"));

        // Act
        await using MarketplaceDbContext ctx2 = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo2 = new PluginPublishingRepositoryAdapter(ctx2);
        bool exists = await repo2.VersionExistsAsync(created.PluginId, "1.0.0");

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task VersionExistsAsync_NonExistentVersion_ReturnsFalse()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo = new PluginPublishingRepositoryAdapter(ctx);
        PluginPublishResult created = await repo.CreatePluginWithInitialVersionAsync(
            MakeCreateCommand(name: "NoVersionPlugin", version: "1.0.0"));

        // Act
        await using MarketplaceDbContext ctx2 = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo2 = new PluginPublishingRepositoryAdapter(ctx2);
        bool exists = await repo2.VersionExistsAsync(created.PluginId, "9.9.9");

        // Assert
        Assert.False(exists);
    }

    // -------------------------------------------------------------------------
    // GetVersionHistoryAsync — paginated, semver descending
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetVersionHistoryAsync_WithMultipleVersions_ReturnsSemVerDesc()
    {
        // Arrange — create plugin then add versions out of order
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo = new PluginPublishingRepositoryAdapter(ctx);
        PluginPublishResult created = await repo.CreatePluginWithInitialVersionAsync(
            MakeCreateCommand(name: "HistoryPlugin", version: "1.0.0"));

        await using MarketplaceDbContext ctx2 = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo2 = new PluginPublishingRepositoryAdapter(ctx2);
        await repo2.AddVersionAsync(created.PluginId,
            MakeAddVersionCommand(created.PluginId, version: "2.0.0"));

        await using MarketplaceDbContext ctx3 = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo3 = new PluginPublishingRepositoryAdapter(ctx3);
        await repo3.AddVersionAsync(created.PluginId,
            MakeAddVersionCommand(created.PluginId, version: "1.5.0"));

        // Act — get full history
        await using MarketplaceDbContext ctx4 = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo4 = new PluginPublishingRepositoryAdapter(ctx4);
        (IReadOnlyList<VersionHistoryDto> items, int totalCount) =
            await repo4.GetVersionHistoryAsync(
                created.PluginId,
                new ClaudeForge.Core.Shared.Model.PaginationRequest { Page = 1, Limit = 20 });

        // Assert — semver descending: 2.0.0, 1.5.0, 1.0.0
        Assert.Equal(3, totalCount);
        Assert.Equal(3, items.Count);
        Assert.Equal("2.0.0", items[0].Version);
        Assert.Equal("1.5.0", items[1].Version);
        Assert.Equal("1.0.0", items[2].Version);
    }

    [Fact]
    public async Task GetVersionHistoryAsync_OnlyLatestVersionHasIsLatestTrue()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo = new PluginPublishingRepositoryAdapter(ctx);
        PluginPublishResult created = await repo.CreatePluginWithInitialVersionAsync(
            MakeCreateCommand(name: "LatestFlagPlugin", version: "1.0.0"));

        await using MarketplaceDbContext ctx2 = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo2 = new PluginPublishingRepositoryAdapter(ctx2);
        await repo2.AddVersionAsync(created.PluginId,
            MakeAddVersionCommand(created.PluginId, version: "2.0.0"));

        // Act
        await using MarketplaceDbContext ctx3 = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo3 = new PluginPublishingRepositoryAdapter(ctx3);
        (IReadOnlyList<VersionHistoryDto> items, _) =
            await repo3.GetVersionHistoryAsync(
                created.PluginId,
                new ClaudeForge.Core.Shared.Model.PaginationRequest());

        // Assert
        Assert.Equal(1, items.Count(v => v.IsLatest));
        VersionHistoryDto? latestItem = items.FirstOrDefault(v => v.IsLatest);
        Assert.NotNull(latestItem);
        Assert.Equal("2.0.0", latestItem.Version);
    }

    [Fact]
    public async Task GetVersionHistoryAsync_IncludesDownloadCountPerVersion()
    {
        // Arrange — manipulate download counts directly via EF
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo = new PluginPublishingRepositoryAdapter(ctx);
        PluginPublishResult created = await repo.CreatePluginWithInitialVersionAsync(
            MakeCreateCommand(name: "DownloadCountPlugin", version: "1.0.0"));

        await using MarketplaceDbContext ctx2 = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo2 = new PluginPublishingRepositoryAdapter(ctx2);
        await repo2.AddVersionAsync(created.PluginId,
            MakeAddVersionCommand(created.PluginId, version: "2.0.0"));

        // Manually update download counts
        await using MarketplaceDbContext updateCtx = _fixture.CreateContext();
        PluginVersionEntity? v100 = await updateCtx.PluginVersions
            .FirstOrDefaultAsync(v => v.PluginId == created.PluginId && v.Version == "1.0.0");
        PluginVersionEntity? v200 = await updateCtx.PluginVersions
            .FirstOrDefaultAsync(v => v.PluginId == created.PluginId && v.Version == "2.0.0");

        Assert.NotNull(v100);
        Assert.NotNull(v200);
        v100.DownloadCount = 5;
        v200.DownloadCount = 10;
        await updateCtx.SaveChangesAsync();

        // Act
        await using MarketplaceDbContext readCtx = _fixture.CreateContext();
        IPluginPublishingRepositoryPort readRepo = new PluginPublishingRepositoryAdapter(readCtx);
        (IReadOnlyList<VersionHistoryDto> items, _) =
            await readRepo.GetVersionHistoryAsync(
                created.PluginId,
                new ClaudeForge.Core.Shared.Model.PaginationRequest());

        // Assert — semver desc order: 2.0.0 first with 10 downloads, 1.0.0 with 5
        Assert.Equal(2, items.Count);
        Assert.Equal("2.0.0", items[0].Version);
        Assert.Equal(10L, items[0].DownloadCount);
        Assert.Equal("1.0.0", items[1].Version);
        Assert.Equal(5L, items[1].DownloadCount);
    }

    [Fact]
    public async Task GetVersionHistoryAsync_Pagination_ReturnsPaginatedSubset()
    {
        // Arrange — create 5 versions
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo = new PluginPublishingRepositoryAdapter(ctx);
        PluginPublishResult created = await repo.CreatePluginWithInitialVersionAsync(
            MakeCreateCommand(name: "PaginationPlugin", version: "1.0.0"));

        foreach (string ver in new[] { "1.1.0", "1.2.0", "1.3.0", "1.4.0" })
        {
            await using MarketplaceDbContext addCtx = _fixture.CreateContext();
            IPluginPublishingRepositoryPort addRepo = new PluginPublishingRepositoryAdapter(addCtx);
            await addRepo.AddVersionAsync(created.PluginId,
                MakeAddVersionCommand(created.PluginId, version: ver));
        }

        // Act — page 2, limit 2
        await using MarketplaceDbContext readCtx = _fixture.CreateContext();
        IPluginPublishingRepositoryPort readRepo = new PluginPublishingRepositoryAdapter(readCtx);
        (IReadOnlyList<VersionHistoryDto> items, int totalCount) =
            await readRepo.GetVersionHistoryAsync(
                created.PluginId,
                new ClaudeForge.Core.Shared.Model.PaginationRequest { Page = 2, Limit = 2 });

        // Assert
        Assert.Equal(5, totalCount);
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task GetVersionHistoryAsync_PageBeyondRange_ReturnsEmptyList()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo = new PluginPublishingRepositoryAdapter(ctx);
        PluginPublishResult created = await repo.CreatePluginWithInitialVersionAsync(
            MakeCreateCommand(name: "BeyondRangePlugin", version: "1.0.0"));

        // Act — page 100, limit 10, only 1 version exists
        await using MarketplaceDbContext readCtx = _fixture.CreateContext();
        IPluginPublishingRepositoryPort readRepo = new PluginPublishingRepositoryAdapter(readCtx);
        (IReadOnlyList<VersionHistoryDto> items, int totalCount) =
            await readRepo.GetVersionHistoryAsync(
                created.PluginId,
                new ClaudeForge.Core.Shared.Model.PaginationRequest { Page = 100, Limit = 10 });

        // Assert
        Assert.Equal(1, totalCount);
        Assert.Empty(items);
    }

    [Fact]
    public async Task GetVersionHistoryAsync_DefaultPagination_UsesPage1Limit20()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo = new PluginPublishingRepositoryAdapter(ctx);
        PluginPublishResult created = await repo.CreatePluginWithInitialVersionAsync(
            MakeCreateCommand(name: "DefaultPagPlugin", version: "1.0.0"));

        // Act — no explicit page/limit (default PaginationRequest)
        await using MarketplaceDbContext readCtx = _fixture.CreateContext();
        IPluginPublishingRepositoryPort readRepo = new PluginPublishingRepositoryAdapter(readCtx);
        (IReadOnlyList<VersionHistoryDto> items, int totalCount) =
            await readRepo.GetVersionHistoryAsync(
                created.PluginId,
                new ClaudeForge.Core.Shared.Model.PaginationRequest());

        // Assert — defaults: page=1, limit=20
        Assert.Equal(1, totalCount);
        Assert.Single(items);
    }

    // -------------------------------------------------------------------------
    // GetVersionAsync — single version detail
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetVersionAsync_ExistingVersion_ReturnsVersionDetail()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo = new PluginPublishingRepositoryAdapter(ctx);
        CreatePluginCommand createCmd = MakeCreateCommand(
            name: "VersionDetailPlugin",
            version: "1.2.3",
            releaseNotes: "Special release");
        PluginPublishResult created = await repo.CreatePluginWithInitialVersionAsync(createCmd);

        // Act
        await using MarketplaceDbContext readCtx = _fixture.CreateContext();
        IPluginPublishingRepositoryPort readRepo = new PluginPublishingRepositoryAdapter(readCtx);
        VersionDetailDto? detail = await readRepo.GetVersionAsync(created.PluginId, "1.2.3");

        // Assert
        Assert.NotNull(detail);
        Assert.Equal("1.2.3", detail.Version);
        Assert.Equal(created.PluginId, detail.PluginId);
        Assert.True(detail.IsLatest);
        Assert.Equal("Special release", detail.ReleaseNotes);
        Assert.Equal("tar.gz", detail.PackageFormat);
    }

    [Fact]
    public async Task GetVersionAsync_NonExistentVersion_ReturnsNull()
    {
        // Arrange
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        IPluginPublishingRepositoryPort repo = new PluginPublishingRepositoryAdapter(ctx);
        PluginPublishResult created = await repo.CreatePluginWithInitialVersionAsync(
            MakeCreateCommand(name: "NullVersionPlugin", version: "1.0.0"));

        // Act
        await using MarketplaceDbContext readCtx = _fixture.CreateContext();
        IPluginPublishingRepositoryPort readRepo = new PluginPublishingRepositoryAdapter(readCtx);
        VersionDetailDto? detail = await readRepo.GetVersionAsync(created.PluginId, "9.9.9");

        // Assert
        Assert.Null(detail);
    }
}
