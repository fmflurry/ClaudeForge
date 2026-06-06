namespace ClaudeForge.Application.Modules.Telemetry.UseCases;

/// <summary>
/// Command for ingesting a single telemetry event.
/// Contains no PII fields — coarse OS/Arch only.
/// </summary>
public sealed record IngestTelemetryCommand
{
    /// <summary>Event type: 'download' or 'install' (case-sensitive).</summary>
    public required string EventType { get; init; }

    /// <summary>Plugin identifier.</summary>
    public required Guid PluginId { get; init; }

    /// <summary>Semantic version string. Nullable.</summary>
    public string? Version { get; init; }

    /// <summary>SHA-256 hex of a UUID v4 client identifier. Must be exactly 64 lowercase hex characters.</summary>
    public required string AnonClientId { get; init; }

    /// <summary>Coarse OS: 'darwin' | 'linux' | 'windows'. Nullable.</summary>
    public string? ClientOs { get; init; }

    /// <summary>Coarse architecture: 'x64' | 'arm64'. Nullable.</summary>
    public string? ClientArch { get; init; }
}
