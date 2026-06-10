namespace ClaudeForge.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the <c>config_change_log</c> table.
/// Audit trail of analysis configuration changes.
/// </summary>
public sealed class ConfigChangeLogEntity
{
    public Guid Id { get; set; }

    /// <summary>User who made the change.</summary>
    public Guid ChangedBy { get; set; }

    /// <summary>JSONB snapshot of the config before the change.</summary>
    public string PreviousConfig { get; set; } = "{}";

    /// <summary>JSONB snapshot of the config after the change.</summary>
    public string NewConfig { get; set; } = "{}";

    /// <summary>Description of what changed.</summary>
    public string ChangeDescription { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
