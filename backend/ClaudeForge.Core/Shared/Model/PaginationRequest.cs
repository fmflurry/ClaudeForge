namespace ClaudeForge.Core.Shared.Model;

/// <summary>
/// Common pagination parameters with defaults and validation bounds.
/// </summary>
public sealed record PaginationRequest
{
    public int Page { get; init; } = 1;
    public int Limit { get; init; } = 20;

    public static PaginationRequest Default => new();

    public bool IsValid(out string? error)
    {
        if (Page < 1)
        {
            error = "Page must be greater than or equal to 1.";
            return false;
        }

        if (Limit is < 1 or > 100)
        {
            error = "Limit must be between 1 and 100.";
            return false;
        }

        error = null;
        return true;
    }
}
