using ClaudeForge.Application.Modules.Telemetry.Ports;
using ClaudeForge.Application.Modules.Telemetry.UseCases;
using ClaudeForge.Core.Shared.Exceptions;
using NSubstitute;

namespace ClaudeForge.Tests.Unit.Telemetry;

/// <summary>
/// Unit tests for Group 8 (task 8.1): IngestTelemetryEventUseCase.
///
/// Uses NSubstitute mocks — no real database.
///
/// Expected production types (coder MUST match these names exactly):
///
///   Namespace: ClaudeForge.Application.Modules.Telemetry.Ports
///     ITelemetryStorePort
///       Task RecordEventAsync(TelemetryEvent ev, CancellationToken ct = default)
///       Task&lt;TelemetrySummaryDto&gt; GetSummaryAsync(Guid pluginId, CancellationToken ct = default)
///       Task&lt;int&gt; PurgeRawEventsOlderThanAsync(int days, CancellationToken ct = default)
///
///     TelemetryEvent (domain record — no PII fields)
///       string EventType; Guid PluginId; string? Version; string AnonClientId;
///       string? ClientOs; string? ClientArch; DateTimeOffset OccurredAt;
///
///     TelemetrySummaryDto
///       Guid PluginId; long TotalDownloads; long TotalInstalls;
///       IReadOnlyList&lt;DailyActivityDto&gt; Last7Days;
///
///     DailyActivityDto
///       DateOnly Date; long Downloads; long Installs;
///
///   Namespace: ClaudeForge.Application.Modules.Telemetry.UseCases
///     IngestTelemetryCommand
///       string EventType; Guid PluginId; string? Version;
///       string AnonClientId; string? ClientOs; string? ClientArch;
///
///     IngestTelemetryEventUseCase(ITelemetryStorePort store)
///       Task ExecuteAsync(IngestTelemetryCommand cmd, CancellationToken ct = default)
///
/// Spec verbatim error strings (design.md §5 + spec.md scenarios):
///   malformed / missing event_type →
///     "Event type is required and must be 'download' or 'install'."
///   malformed / bad anon_client_id →
///     "Anonymous client ID is required and must be a 64-character hex string."
///   malformed / missing plugin_id →
///     "Plugin ID is required."
/// </summary>
public sealed class IngestTelemetryEventUseCaseTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static readonly string ValidAnonClientId = new('a', 64);  // 64-hex 'a' characters

    private static IngestTelemetryCommand ValidDownloadCommand(Guid? pluginId = null) =>
        new()
        {
            EventType = "download",
            PluginId = pluginId ?? Guid.NewGuid(),
            Version = "1.0.0",
            AnonClientId = ValidAnonClientId,
            ClientOs = "linux",
            ClientArch = "x64",
        };

    private static IngestTelemetryCommand ValidInstallCommand(Guid? pluginId = null) =>
        new()
        {
            EventType = "install",
            PluginId = pluginId ?? Guid.NewGuid(),
            Version = "2.1.0",
            AnonClientId = ValidAnonClientId,
            ClientOs = "darwin",
            ClientArch = "arm64",
        };

    // -------------------------------------------------------------------------
    // 8.1 — Valid events: store.RecordEventAsync called once
    // Spec: "Install event recorded without PII" / "Download event recorded with anonymous client ID"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ValidDownloadCommand_CallsStoreRecordEventOnce()
    {
        // Arrange
        ITelemetryStorePort store = Substitute.For<ITelemetryStorePort>();
        IngestTelemetryEventUseCase useCase = new(store);
        IngestTelemetryCommand cmd = ValidDownloadCommand();

        // Act
        await useCase.ExecuteAsync(cmd);

        // Assert — store must be called exactly once with a non-null TelemetryEvent
        await store.Received(1).RecordEventAsync(Arg.Any<TelemetryEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ValidInstallCommand_CallsStoreRecordEventOnce()
    {
        // Arrange
        ITelemetryStorePort store = Substitute.For<ITelemetryStorePort>();
        IngestTelemetryEventUseCase useCase = new(store);
        IngestTelemetryCommand cmd = ValidInstallCommand();

        // Act
        await useCase.ExecuteAsync(cmd);

        // Assert
        await store.Received(1).RecordEventAsync(Arg.Any<TelemetryEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ValidDownloadCommand_MapsEventTypeCorrectly()
    {
        // Arrange
        ITelemetryStorePort store = Substitute.For<ITelemetryStorePort>();
        IngestTelemetryEventUseCase useCase = new(store);
        Guid pluginId = Guid.NewGuid();
        IngestTelemetryCommand cmd = ValidDownloadCommand(pluginId);

        // Act
        await useCase.ExecuteAsync(cmd);

        // Assert — the TelemetryEvent passed to the store has the correct EventType + PluginId
        await store.Received(1).RecordEventAsync(
            Arg.Is<TelemetryEvent>(e =>
                e.EventType == "download" &&
                e.PluginId == pluginId &&
                e.AnonClientId == ValidAnonClientId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ValidCommand_PassesCancellationTokenToStore()
    {
        // Arrange
        ITelemetryStorePort store = Substitute.For<ITelemetryStorePort>();
        IngestTelemetryEventUseCase useCase = new(store);
        using CancellationTokenSource cts = new();
        IngestTelemetryCommand cmd = ValidDownloadCommand();

        // Act
        await useCase.ExecuteAsync(cmd, cts.Token);

        // Assert
        await store.Received(1).RecordEventAsync(
            Arg.Any<TelemetryEvent>(),
            Arg.Is<CancellationToken>(t => t == cts.Token));
    }

    // -------------------------------------------------------------------------
    // 8.1 — Malformed: missing event_type → 400 ProblemDetailsException, store NOT called
    // Spec verbatim: "Event type is required and must be 'download' or 'install'."
    // Spec: "WHEN an event is missing required fields ... THEN the system rejects the event with HTTP 400
    //        AND logs the rejection without storing the incomplete event"
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public async Task ExecuteAsync_MissingEventType_ThrowsProblemDetailsException400AndStoreNotCalled(
        string? eventType)
    {
        // Arrange
        ITelemetryStorePort store = Substitute.For<ITelemetryStorePort>();
        IngestTelemetryEventUseCase useCase = new(store);
        IngestTelemetryCommand cmd = ValidDownloadCommand() with { EventType = eventType! };

        // Act & Assert
        ProblemDetailsException ex = await Assert.ThrowsAsync<ProblemDetailsException>(
            () => useCase.ExecuteAsync(cmd));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("Event type is required and must be 'download' or 'install'.", ex.Message);
        await store.DidNotReceive().RecordEventAsync(Arg.Any<TelemetryEvent>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("view")]
    [InlineData("usage")]
    [InlineData("DOWNLOAD")]   // case-sensitive: must be lowercase per spec
    [InlineData("Install")]
    [InlineData("404-not-found")]
    public async Task ExecuteAsync_InvalidEventType_ThrowsProblemDetailsException400AndStoreNotCalled(
        string eventType)
    {
        // Arrange
        ITelemetryStorePort store = Substitute.For<ITelemetryStorePort>();
        IngestTelemetryEventUseCase useCase = new(store);
        IngestTelemetryCommand cmd = ValidDownloadCommand() with { EventType = eventType };

        // Act & Assert
        ProblemDetailsException ex = await Assert.ThrowsAsync<ProblemDetailsException>(
            () => useCase.ExecuteAsync(cmd));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("Event type is required and must be 'download' or 'install'.", ex.Message);
        await store.DidNotReceive().RecordEventAsync(Arg.Any<TelemetryEvent>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // 8.1 — Malformed: bad anon_client_id → 400 ProblemDetailsException, store NOT called
    // Spec verbatim: "Anonymous client ID is required and must be a 64-character hex string."
    // Design §5: "anon_client_id CHAR(64) — SHA-256 hex of UUID v4 client identifier"
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public async Task ExecuteAsync_MissingAnonClientId_ThrowsProblemDetailsException400AndStoreNotCalled(
        string? anonClientId)
    {
        // Arrange
        ITelemetryStorePort store = Substitute.For<ITelemetryStorePort>();
        IngestTelemetryEventUseCase useCase = new(store);
        IngestTelemetryCommand cmd = ValidDownloadCommand() with { AnonClientId = anonClientId! };

        // Act & Assert
        ProblemDetailsException ex = await Assert.ThrowsAsync<ProblemDetailsException>(
            () => useCase.ExecuteAsync(cmd));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("Anonymous client ID is required and must be a 64-character hex string.", ex.Message);
        await store.DidNotReceive().RecordEventAsync(Arg.Any<TelemetryEvent>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("abc")]                   // too short
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]  // 64 non-hex
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]   // 63 upper-hex
    public async Task ExecuteAsync_InvalidAnonClientIdFormat_ThrowsProblemDetailsException400AndStoreNotCalled(
        string anonClientId)
    {
        // Arrange
        ITelemetryStorePort store = Substitute.For<ITelemetryStorePort>();
        IngestTelemetryEventUseCase useCase = new(store);
        IngestTelemetryCommand cmd = ValidDownloadCommand() with { AnonClientId = anonClientId };

        // Act & Assert
        ProblemDetailsException ex = await Assert.ThrowsAsync<ProblemDetailsException>(
            () => useCase.ExecuteAsync(cmd));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("Anonymous client ID is required and must be a 64-character hex string.", ex.Message);
        await store.DidNotReceive().RecordEventAsync(Arg.Any<TelemetryEvent>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // 8.1 — Malformed: missing plugin_id → 400 ProblemDetailsException, store NOT called
    // Spec verbatim: "Plugin ID is required."
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_EmptyPluginId_ThrowsProblemDetailsException400AndStoreNotCalled()
    {
        // Arrange
        ITelemetryStorePort store = Substitute.For<ITelemetryStorePort>();
        IngestTelemetryEventUseCase useCase = new(store);
        IngestTelemetryCommand cmd = ValidDownloadCommand() with { PluginId = Guid.Empty };

        // Act & Assert
        ProblemDetailsException ex = await Assert.ThrowsAsync<ProblemDetailsException>(
            () => useCase.ExecuteAsync(cmd));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("Plugin ID is required.", ex.Message);
        await store.DidNotReceive().RecordEventAsync(Arg.Any<TelemetryEvent>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // 8.1 — No PII: TelemetryEvent MUST NOT carry user emails / hostnames / IPs
    // Design §5: "Coarse OS/Arch only (darwin|linux|windows, x64|arm64); IP not logged with events"
    // Spec: "no user name, email, or system hostname is captured"
    // Assert that TelemetryEvent type only exposes coarse fields — checked via reflection
    // -------------------------------------------------------------------------

    [Fact]
    public void TelemetryEvent_TypeHasNoPiiFields()
    {
        // Arrange: list of property names that would indicate PII storage
        string[] piiFieldNames =
        [
            "Email", "UserName", "Username", "UserId", "IpAddress",
            "Hostname", "Ip", "UserAgent", "DeviceId", "MachineId",
            "SystemHostname", "FullName", "FirstName", "LastName",
        ];

        // Act
        System.Reflection.PropertyInfo[] properties =
            typeof(TelemetryEvent).GetProperties(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        string[] propertyNames = properties.Select(p => p.Name).ToArray();

        // Assert — none of the PII field names appear on TelemetryEvent
        foreach (string piiField in piiFieldNames)
        {
            Assert.DoesNotContain(piiField, propertyNames,
                StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void TelemetryEvent_HasRequiredCoarseFields()
    {
        // Assert that TelemetryEvent has the expected coarse fields (not PII)
        System.Reflection.PropertyInfo[] properties =
            typeof(TelemetryEvent).GetProperties(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        string[] propertyNames = properties.Select(p => p.Name).ToArray();

        Assert.Contains("EventType", propertyNames);
        Assert.Contains("PluginId", propertyNames);
        Assert.Contains("AnonClientId", propertyNames);
        Assert.Contains("ClientOs", propertyNames);      // coarse: darwin|linux|windows
        Assert.Contains("ClientArch", propertyNames);    // coarse: x64|arm64
    }

    [Fact]
    public void IngestTelemetryCommand_HasNoPiiFields()
    {
        // Assert that the command DTO also does not expose PII field names
        string[] piiFieldNames =
        [
            "Email", "UserName", "Username", "UserId", "IpAddress",
            "Hostname", "Ip", "UserAgent", "DeviceId", "MachineId",
        ];

        System.Reflection.PropertyInfo[] properties =
            typeof(IngestTelemetryCommand).GetProperties(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        string[] propertyNames = properties.Select(p => p.Name).ToArray();

        foreach (string piiField in piiFieldNames)
        {
            Assert.DoesNotContain(piiField, propertyNames, StringComparer.OrdinalIgnoreCase);
        }
    }

    // -------------------------------------------------------------------------
    // 8.1 — Coarse OS/Arch: only canonical values are accepted
    // Design §5: "Coarse: 'darwin' | 'linux' | 'windows'", "'x64' | 'arm64'"
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("darwin", "x64")]
    [InlineData("linux", "arm64")]
    [InlineData("windows", "x64")]
    [InlineData(null, null)]           // OS/arch optional (nullable fields)
    public async Task ExecuteAsync_ValidCoarseOsArch_Succeeds(
        string? clientOs, string? clientArch)
    {
        // Arrange
        ITelemetryStorePort store = Substitute.For<ITelemetryStorePort>();
        IngestTelemetryEventUseCase useCase = new(store);
        IngestTelemetryCommand cmd = ValidDownloadCommand() with
        {
            ClientOs = clientOs,
            ClientArch = clientArch,
        };

        // Act — should not throw
        await useCase.ExecuteAsync(cmd);

        // Assert
        await store.Received(1).RecordEventAsync(
            Arg.Is<TelemetryEvent>(e =>
                e.ClientOs == clientOs &&
                e.ClientArch == clientArch),
            Arg.Any<CancellationToken>());
    }
}
