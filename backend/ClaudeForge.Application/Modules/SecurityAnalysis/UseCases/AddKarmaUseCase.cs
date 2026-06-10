using ClaudeForge.Core.Modules.SecurityAnalysis.Ports;

namespace ClaudeForge.Application.Modules.SecurityAnalysis.UseCases;

/// <summary>
/// Use case for adding karma points to an author.
/// Delegates to <see cref="IKarmaServicePort"/>.
/// </summary>
public sealed class AddKarmaUseCase
{
    private readonly IKarmaServicePort _karmaService;

    public AddKarmaUseCase(IKarmaServicePort karmaService)
    {
        _karmaService = karmaService ?? throw new ArgumentNullException(nameof(karmaService));
    }

    /// <summary>
    /// Adds karma points for an author. Enforces minimum karma >= 0.
    /// Automatically triggers badge checking after the update.
    /// </summary>
    public async Task ExecuteAsync(Guid authorId, int points, string eventType, string description, CancellationToken ct = default)
    {
        await _karmaService.AddKarmaAsync(authorId, points, eventType, description, ct);
    }
}
