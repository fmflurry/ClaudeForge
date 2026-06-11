namespace ClaudeForge.Infrastructure.AddOnSearch;

/// <summary>
/// Internal projection used for raw SQL queries in PostgresSearchAdapter.
/// Column names must match the SQL aliases exactly (snake_case).
/// EF Core maps snake_case column names to PascalCase properties via the convention,
/// but since we use SqlQueryRaw we rely on column name matching.
/// </summary>
internal sealed class SearchRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public long DownloadCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public float RawScore { get; set; }
    public string? LatestVersion { get; set; }
    public string? TypeValues { get; set; }
    public string? LanguageValues { get; set; }
    public string? UseCaseValues { get; set; }
}
