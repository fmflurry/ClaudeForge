namespace ClaudeForge.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the <c>telemetry_events</c> raw-events table.
/// Internal-only — never exposed through the API.
/// PK is <c>bigserial</c> (long / bigint auto-increment).
/// </summary>
public sealed class TelemetryEventEntity
{
    public long Id { get; set; }

    /// <summary>
    /// Event type: <c>'download'</c> or <c>'install'</c>.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Nullable FK to plugins. Set to NULL on plugin deletion (ON DELETE SET NULL).
    /// </summary>
    public Guid? PluginId { get; set; }

    /// <summary>
    /// Semantic version string. Nullable.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// SHA-256 of a random UUID (64 hex characters). Never PII.
    /// Stored as <c>char(64)</c> in the database.
    /// </summary>
    public string? AnonClientId { get; set; }

    /// <summary>
    /// Coarse OS: <c>'darwin'</c>, <c>'linux'</c>, or <c>'windows'</c>.
    /// </summary>
    public string? ClientOs { get; set; }

    /// <summary>
    /// Coarse architecture: <c>'x64'</c> or <c>'arm64'</c>.
    /// </summary>
    public string? ClientArch { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}
