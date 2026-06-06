namespace ClaudeForge.Application.Modules.Docs.UseCases;

/// <summary>
/// Input model for the SearchDocsUseCase.
/// Defaults: Page=1, Limit=20 (capped at 20).
/// </summary>
public sealed class SearchDocsQuery
{
    public string? Search { get; init; }
    public int Page { get; init; } = 1;
    public int Limit { get; init; } = 20;
}
