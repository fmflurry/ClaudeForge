namespace ClaudeForge.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the <c>notifications</c> table.
/// Stores in-app notifications for users.
/// </summary>
public sealed class NotificationEntity
{
    public Guid Id { get; set; }

    /// <summary>FK → users. Recipient of the notification.</summary>
    public Guid UserId { get; set; }

    /// <summary>Notification type: "appeal_resolved" | "analysis_complete" | "new_badge" | "system"</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Short notification title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Notification body message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Whether the notification has been read.</summary>
    public bool IsRead { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    // Navigation properties
    public UserEntity User { get; set; } = null!;
}
