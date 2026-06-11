namespace ClaudeForge.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the <c>appeals</c> table.
/// Plugin authors can appeal analysis decisions with supporting evidence.
/// </summary>
public sealed class AppealEntity
{
    public Guid Id { get; set; }

    /// <summary>FK → plugins ON DELETE CASCADE.</summary>
    public Guid PluginId { get; set; }

    /// <summary>FK → analysis_results ON DELETE SET NULL.</summary>
    public Guid? AnalysisResultId { get; set; }

    /// <summary>Plugin author who submitted the appeal.</summary>
    public Guid AuthorId { get; set; }

    /// <summary>Reason for the appeal.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Optional evidence submitted by the author to support the appeal.</summary>
    public string? Evidence { get; set; }

    /// <summary>Appeal status: "pending" | "approved" | "rejected".</summary>
    public string Status { get; set; } = "pending";

    /// <summary>Admin who reviewed the appeal.</summary>
    public Guid? ReviewedBy { get; set; }

    /// <summary>When the appeal was reviewed.</summary>
    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>Resolution notes from the admin reviewer.</summary>
    public string? Resolution { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    // Navigation properties
    public AddOnEntity AddOn { get; set; } = null!;
    public AnalysisResultEntity? AnalysisResult { get; set; }
}
