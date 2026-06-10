namespace ClaudeForge.Core.Modules.SecurityAnalysis.Ports;

/// <summary>Karma operation return value: current points, level, and badge list.</summary>
public sealed record KarmaSummary(
    int KarmaPoints,
    int Level,
    IReadOnlyList<string> Badges);

/// <summary>Single karma history entry.</summary>
public sealed record KarmaEventDto(
    string EventType,
    int Points,
    string? Description,
    DateTimeOffset CreatedAt);

/// <summary>
/// Port for karma (reputation points) operations.
/// Implemented by an infrastructure adapter using EF Core.
/// </summary>
public interface IKarmaServicePort
{
    /// <summary>
    /// Adds karma points for an author.
    /// Enforces minimum karma >=0.
    /// Calls badge check after updating.
    /// </summary>
    Task AddKarmaAsync(Guid authorId, int points, string eventType, string description, CancellationToken ct = default);

    /// <summary>Returns the current karma summary for the author.</summary>
    Task<KarmaSummary> GetKarmaAsync(Guid authorId, CancellationToken ct = default);

    /// <summary>Returns karma history ordered by created_at DESC.</summary>
    Task<IReadOnlyList<KarmaEventDto>> GetKarmaHistoryAsync(Guid authorId, CancellationToken ct = default);
}
