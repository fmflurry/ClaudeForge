namespace ClaudeForge.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the <c>user_notification_preferences</c> table.
/// Stores per-user notification preferences.
/// </summary>
public sealed class UserNotificationPreferencesEntity
{
    /// <summary>FK → users. One row per user.</summary>
    public Guid UserId { get; set; }

    /// <summary>Whether the user wants email alerts. Default true.</summary>
    public bool EmailAlerts { get; set; } = true;

    /// <summary>Whether the user wants in-app alerts. Default true.</summary>
    public bool InAppAlerts { get; set; } = true;

    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public UserEntity User { get; set; } = null!;
}
