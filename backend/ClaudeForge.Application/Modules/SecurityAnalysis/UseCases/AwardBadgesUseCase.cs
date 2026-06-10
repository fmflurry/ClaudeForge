using ClaudeForge.Core.Modules.SecurityAnalysis.Ports;

namespace ClaudeForge.Application.Modules.SecurityAnalysis.UseCases;

/// <summary>
/// Use case for checking and awarding badges.
/// Delegates to <see cref="IBadgeServicePort"/>.
/// </summary>
public sealed class AwardBadgesUseCase
{
    private readonly IBadgeServicePort _badgeService;

    public AwardBadgesUseCase(IBadgeServicePort badgeService)
    {
        _badgeService = badgeService ?? throw new ArgumentNullException(nameof(badgeService));
    }

    /// <summary>
    /// Checks all badge criteria against the author's current stats
    /// and awards any newly-earned badges.
    /// </summary>
    public async Task ExecuteAsync(Guid authorId, CancellationToken ct = default)
    {
        // Award badges and discard returned badge names (caller doesn't need them)
        _ = await _badgeService.CheckAndAwardBadgesAsync(authorId, ct);
    }
}
