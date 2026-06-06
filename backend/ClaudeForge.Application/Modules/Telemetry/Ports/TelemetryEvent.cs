namespace ClaudeForge.Application.Modules.Telemetry.Ports;

/// <summary>
/// Domain record representing a single telemetry event (no PII fields).
/// Coarse OS/Arch only: 'darwin' | 'linux' | 'windows', 'x64' | 'arm64'.
/// </summary>
public sealed record TelemetryEvent
{
    /// <summary>Event type: 'download' or 'install'.</summary>
    public required string EventType { get; init; }

    /// <summary>Plugin identifier.</summary>
    public required Guid PluginId { get; init; }

    /// <summary>Semantic version string. Nullable.</summary>
    public string? Version { get; init; }

    /// <summary>SHA-256 hex of a UUID v4 client identifier. 64 hex characters. Never PII.</summary>
    public required string AnonClientId { get; init; }

    /// <summary>Coarse OS: 'darwin' | 'linux' | 'windows'. Nullable.</summary>
    public string? ClientOs { get; init; }

    /// <summary>Coarse architecture: 'x64' | 'arm64'. Nullable.</summary>
    public string? ClientArch { get; init; }

    /// <summary>When the event occurred (UTC).</summary>
    public required DateTimeOffset OccurredAt { get; init; }
}
