namespace ClaudeForge.Core.Identity;

/// <summary>
/// Value object representing an organization member role.
/// </summary>
public sealed record OrgRole
{
    public static readonly OrgRole Owner = new("owner");
    public static readonly OrgRole Admin = new("admin");
    public static readonly OrgRole Member = new("member");

    private static readonly IReadOnlySet<string> ValidValues =
        new HashSet<string>(StringComparer.Ordinal) { "owner", "admin", "member" };

    public string Value { get; }

    private OrgRole(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Parses a string into an <see cref="OrgRole"/>.
    /// Throws <see cref="ArgumentException"/> for unknown values.
    /// </summary>
    public static OrgRole Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("OrgRole value must not be null or whitespace.", nameof(value));

        if (!ValidValues.Contains(value))
            throw new ArgumentException($"'{value}' is not a valid OrgRole. Valid values: owner, admin, member.", nameof(value));

        return value switch
        {
            "owner" => Owner,
            "admin" => Admin,
            "member" => Member,
            _ => throw new ArgumentException($"Unhandled OrgRole value: {value}", nameof(value)),
        };
    }

    public override string ToString() => Value;
}
