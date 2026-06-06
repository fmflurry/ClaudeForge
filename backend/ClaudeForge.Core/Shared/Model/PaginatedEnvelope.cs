namespace ClaudeForge.Core.Shared.Model;

/// <summary>
/// Standard paginated response envelope used by all list endpoints.
/// </summary>
public sealed record PaginatedEnvelope<T>
{
    public required IReadOnlyList<T> Data { get; init; }
    public required int TotalCount { get; init; }
    public required int Page { get; init; }
    public required int Limit { get; init; }
    public int TotalPages => Limit > 0 ? (int)Math.Ceiling((double)TotalCount / Limit) : 0;
}
