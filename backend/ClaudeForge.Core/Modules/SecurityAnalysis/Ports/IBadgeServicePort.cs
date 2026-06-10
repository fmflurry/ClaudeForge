namespace ClaudeForge.Core.Modules.SecurityAnalysis.Ports;

/// <summary>DTO for a badge earned by a specific author.</summary>
public sealed record AuthorBadgeDto(
    Guid BadgeId,
    string Name,
    string Slug,
    string Description,
    string? IconUrl,
    DateTimeOffset AwardedAt);

/// <summary>DTO for a badge definition (available to earn).</summary>
public sealed record BadgeDefinitionDto(
    Guid Id,
    string Name,
    string Slug,
    string Description,
    string? IconUrl,
    string Requirements,
    int Tier,
    DateTimeOffset CreatedAt);

/// <summary>
/// Port for badge operations.
/// Implemented by an infrastructure adapter using EF Core.
/// </summary>
public interface IBadgeServicePort
{
    /// <summary>
    /// Checks all badge definitions against the author's current stats,
    /// and awards any newly-earned badges. Called after every karma event.
    /// Returns the names of badges that were newly awarded in this call.
    /// </summary>
    Task<IReadOnlyList<string>> CheckAndAwardBadgesAsync(Guid authorId, CancellationToken ct = default);

    /// <summary>Returns all badges earned by the author (joined with badge definitions).</summary>
    Task<IReadOnlyList<AuthorBadgeDto>> GetAuthorBadgesAsync(Guid authorId, CancellationToken ct = default);

    /// <summary>Returns all available badge definitions.</summary>
    Task<IReadOnlyList<BadgeDefinitionDto>> GetAllBadgesAsync(CancellationToken ct = default);
}
