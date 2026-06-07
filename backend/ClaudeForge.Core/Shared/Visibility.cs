namespace ClaudeForge.Core.Shared;

/// <summary>
/// Value object representing plugin visibility.
/// </summary>
public sealed record Visibility
{
    public static readonly Visibility Public = new("public");
    public static readonly Visibility Private = new("private");

    private static readonly IReadOnlySet<string> ValidValues =
        new HashSet<string>(StringComparer.Ordinal) { "public", "private" };

    public string Value { get; }

    private Visibility(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Parses a string into a <see cref="Visibility"/> value object.
    /// Throws <see cref="ArgumentException"/> for unknown values.
    /// </summary>
    public static Visibility Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Visibility value must not be null or whitespace.", nameof(value));

        if (!ValidValues.Contains(value))
            throw new ArgumentException($"'{value}' is not a valid Visibility. Valid values: public, private.", nameof(value));

        return value switch
        {
            "public" => Public,
            "private" => Private,
            _ => throw new ArgumentException($"Unhandled Visibility value: {value}", nameof(value)),
        };
    }

    public override string ToString() => Value;
}
