namespace ClaudeForge.Core.Domain.Plugins;

/// <summary>
/// Semantic version value object (major.minor.patch).
/// Immutable, comparable, and parseable from string form.
/// </summary>
public readonly record struct SemVer : IComparable<SemVer>
{
    public int Major { get; init; }
    public int Minor { get; init; }
    public int Patch { get; init; }

    public SemVer(int major, int minor, int patch)
    {
        if (major < 0) throw new ArgumentException("Major must be non-negative.", nameof(major));
        if (minor < 0) throw new ArgumentException("Minor must be non-negative.", nameof(minor));
        if (patch < 0) throw new ArgumentException("Patch must be non-negative.", nameof(patch));

        Major = major;
        Minor = minor;
        Patch = patch;
    }

    /// <summary>
    /// Parses a semantic version string of the form "major.minor.patch".
    /// Throws <see cref="ArgumentException"/> on null, empty, malformed, or negative components.
    /// </summary>
    public static SemVer Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Version string must not be null or empty.", nameof(value));

        string[] parts = value.Split('.');
        if (parts.Length != 3)
            throw new ArgumentException($"Version '{value}' is not in the format 'major.minor.patch'.", nameof(value));

        if (!int.TryParse(parts[0], out int major) ||
            !int.TryParse(parts[1], out int minor) ||
            !int.TryParse(parts[2], out int patch))
        {
            throw new ArgumentException($"Version '{value}' contains non-integer components.", nameof(value));
        }

        // Constructor validates non-negative
        return new SemVer(major, minor, patch);
    }

    /// <summary>
    /// Returns a monotonically increasing sort key: major * 1_000_000_000_000 + minor * 1_000_000 + patch.
    /// Ensures that 1.10.0 sorts after 1.9.0 (lexicographic comparison would fail).
    /// </summary>
    public long ToVersionSort() =>
        (long)Major * 1_000_000_000_000L + (long)Minor * 1_000_000L + Patch;

    /// <inheritdoc />
    public int CompareTo(SemVer other) => ToVersionSort().CompareTo(other.ToVersionSort());

    /// <inheritdoc />
    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}
