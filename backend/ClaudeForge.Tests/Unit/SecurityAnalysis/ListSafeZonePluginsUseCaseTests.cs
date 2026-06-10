using ClaudeForge.Application.Modules.SecurityAnalysis.UseCases;
using ClaudeForge.Core.Modules.Organizations.Ports;
using NSubstitute;

namespace ClaudeForge.Tests.Unit.SecurityAnalysis;

/// <summary>
/// Unit tests for ListSafeZonePluginsUseCase.
///
/// Tests org isolation, global plugin visibility, blocked globals,
/// and pending approval filtering.
///
/// Uses NSubstitute mock for ISafeZoneStorePort — no real database.
/// </summary>
public sealed class ListSafeZonePluginsUseCaseTests
{
    private readonly ISafeZoneStorePort _store = Substitute.For<ISafeZoneStorePort>();
    private readonly ListSafeZonePluginsUseCase _useCase;

    private static readonly Guid OrgA = Guid.NewGuid();
    private static readonly Guid OrgB = Guid.NewGuid();
    private static readonly Guid Plugin1 = Guid.NewGuid();
    private static readonly Guid Plugin2 = Guid.NewGuid();
    private static readonly Guid Plugin3 = Guid.NewGuid();

    public ListSafeZonePluginsUseCaseTests()
    {
        _useCase = new ListSafeZonePluginsUseCase(_store);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static SafeZonePluginDetailDto MakePlugin(Guid pluginId, string name, string label = "")
    {
        return new SafeZonePluginDetailDto(
            Id: Guid.NewGuid(),
            PluginId: pluginId,
            Name: name,
            Slug: name.ToLowerInvariant(),
            PluginVersion: "1.0.0",
            SecurityScore: 95m,
            SecurityStatus: "passed",
            ApprovedBy: Guid.NewGuid(),
            ApprovedAt: DateTimeOffset.UtcNow,
            Label: label);
    }

    // ── T.3: Org A cannot see Org B's safe zone plugins ─────────────────────

    [Fact]
    public async Task ExecuteAsync_OrgA_DoesNotIncludeOrgBPlugins()
    {
        // Arrange
        _store.ListSafeZonePluginsAsync(OrgA, Arg.Any<CancellationToken>())
            .Returns([MakePlugin(Plugin1, "Plugin-A1")]);

        _store.ListSafeZonePluginsAsync(OrgB, Arg.Any<CancellationToken>())
            .Returns([MakePlugin(Plugin2, "Plugin-B1")]);

        _store.ListGlobalSafeZonePluginsAsync(Arg.Any<CancellationToken>())
            .Returns([]);

        _store.ListBlockedGlobalPluginsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        IReadOnlyList<SafeZonePluginDetailDto> result = await _useCase.ExecuteAsync(OrgA);

        // Assert
        Assert.Single(result);
        Assert.Equal(Plugin1, result[0].PluginId);
        Assert.DoesNotContain(result, p => p.PluginId == Plugin2);
    }

    // ── Global plugins appear for all orgs ───────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_GlobalPlugins_AppearForAllOrgs()
    {
        // Arrange
        _store.ListSafeZonePluginsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([]);

        // Global plugin visible to all orgs
        _store.ListGlobalSafeZonePluginsAsync(Arg.Any<CancellationToken>())
            .Returns([MakePlugin(Plugin1, "Global-Plugin")]);

        _store.ListBlockedGlobalPluginsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        IReadOnlyList<SafeZonePluginDetailDto> resultA = await _useCase.ExecuteAsync(OrgA);
        IReadOnlyList<SafeZonePluginDetailDto> resultB = await _useCase.ExecuteAsync(OrgB);

        // Assert — both orgs see the global plugin
        Assert.Contains(resultA, p => p.PluginId == Plugin1);
        Assert.Contains(resultB, p => p.PluginId == Plugin1);

        // Global plugins get label "GLOBAL"
        SafeZonePluginDetailDto globalPlugin = resultA.First(p => p.PluginId == Plugin1);
        Assert.Equal("GLOBAL", globalPlugin.Label);
    }

    // ── Org-specific + global merged ─────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_OrgAndGlobalPlugins_Merged()
    {
        // Arrange
        _store.ListSafeZonePluginsAsync(OrgA, Arg.Any<CancellationToken>())
            .Returns([MakePlugin(Plugin1, "Org-Plugin")]);

        _store.ListGlobalSafeZonePluginsAsync(Arg.Any<CancellationToken>())
            .Returns([MakePlugin(Plugin2, "Global-Plugin")]);

        _store.ListBlockedGlobalPluginsAsync(OrgA, Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        IReadOnlyList<SafeZonePluginDetailDto> result = await _useCase.ExecuteAsync(OrgA);

        // Assert — both present
        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.PluginId == Plugin1);
        Assert.Contains(result, p => p.PluginId == Plugin2);
    }

    // ── Blocked globals excluded ─────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_BlockedGlobalPlugin_Excluded()
    {
        // Arrange
        _store.ListSafeZonePluginsAsync(OrgA, Arg.Any<CancellationToken>())
            .Returns([]);

        _store.ListGlobalSafeZonePluginsAsync(Arg.Any<CancellationToken>())
            .Returns([
                MakePlugin(Plugin1, "Global-1"),
                MakePlugin(Plugin2, "Global-2"),
            ]);

        // OrgA blocks Plugin2
        _store.ListBlockedGlobalPluginsAsync(OrgA, Arg.Any<CancellationToken>())
            .Returns([Plugin2]);

        // Act
        IReadOnlyList<SafeZonePluginDetailDto> result = await _useCase.ExecuteAsync(OrgA);

        // Assert — Plugin2 excluded
        Assert.Single(result);
        Assert.Equal(Plugin1, result[0].PluginId);
        Assert.DoesNotContain(result, p => p.PluginId == Plugin2);
    }

    [Fact]
    public async Task ExecuteAsync_OnlySpecificOrgBlocked_OtherOrgSeesPlugin()
    {
        // Arrange
        _store.ListSafeZonePluginsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([]);

        _store.ListGlobalSafeZonePluginsAsync(Arg.Any<CancellationToken>())
            .Returns([MakePlugin(Plugin1, "Global-1")]);

        // OrgA blocks Plugin1, OrgB does not
        _store.ListBlockedGlobalPluginsAsync(OrgA, Arg.Any<CancellationToken>())
            .Returns([Plugin1]);
        _store.ListBlockedGlobalPluginsAsync(OrgB, Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        IReadOnlyList<SafeZonePluginDetailDto> resultA = await _useCase.ExecuteAsync(OrgA);
        IReadOnlyList<SafeZonePluginDetailDto> resultB = await _useCase.ExecuteAsync(OrgB);

        // Assert
        Assert.Empty(resultA);
        Assert.Single(resultB);
        Assert.Contains(resultB, p => p.PluginId == Plugin1);
    }

    // ── Duplicate across org and global ──────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_PluginInBothOrgAndGlobal_NoDuplicate()
    {
        // Arrange — same plugin approved for org AND global
        _store.ListSafeZonePluginsAsync(OrgA, Arg.Any<CancellationToken>())
            .Returns([MakePlugin(Plugin1, "Org-Plugin")]);

        _store.ListGlobalSafeZonePluginsAsync(Arg.Any<CancellationToken>())
            .Returns([MakePlugin(Plugin1, "Global-Plugin")]);

        _store.ListBlockedGlobalPluginsAsync(OrgA, Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        IReadOnlyList<SafeZonePluginDetailDto> result = await _useCase.ExecuteAsync(OrgA);

        // Assert — no duplicate; org-specific takes precedence
        Assert.Single(result);
        Assert.Equal(Plugin1, result[0].PluginId);
        // Org-specific plugin keeps its original label (not "GLOBAL")
        Assert.NotEqual("GLOBAL", result[0].Label);
    }

    // ── Empty results ────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_NoPluginsForOrg_ReturnsEmpty()
    {
        _store.ListSafeZonePluginsAsync(OrgA, Arg.Any<CancellationToken>())
            .Returns([]);
        _store.ListGlobalSafeZonePluginsAsync(Arg.Any<CancellationToken>())
            .Returns([]);
        _store.ListBlockedGlobalPluginsAsync(OrgA, Arg.Any<CancellationToken>())
            .Returns([]);

        IReadOnlyList<SafeZonePluginDetailDto> result = await _useCase.ExecuteAsync(OrgA);

        Assert.Empty(result);
    }
}
