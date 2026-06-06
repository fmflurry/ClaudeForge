using ClaudeForge.Core.Domain.Plugins;

namespace ClaudeForge.Tests.Unit.Domain;

/// <summary>
/// Unit tests for the SemVer value object (pure domain logic — no DB required).
///
/// Expected production type:
///   namespace ClaudeForge.Core.Domain.Plugins
///   public readonly record struct SemVer
///   {
///       public int Major { get; init; }
///       public int Minor { get; init; }
///       public int Patch { get; init; }
///
///       // Returns major * 1_000_000_000_000L + minor * 1_000_000L + patch
///       public long ToVersionSort();
///
///       // Parses "1.2.3" — throws ArgumentException if malformed
///       public static SemVer Parse(string value);
///
///       // IComparable<SemVer> for ordering
///   }
/// </summary>
public sealed class SemVerTests
{
    // -----------------------------------------------------------------------
    // Parsing — happy path
    // -----------------------------------------------------------------------

    [Fact]
    public void Parse_ValidVersion_ReturnsSemVer()
    {
        SemVer v = SemVer.Parse("1.2.3");

        Assert.Equal(1, v.Major);
        Assert.Equal(2, v.Minor);
        Assert.Equal(3, v.Patch);
    }

    [Theory]
    [InlineData("0.0.0")]
    [InlineData("1.0.0")]
    [InlineData("255.255.255")]
    [InlineData("10.0.1")]
    public void Parse_BoundaryVersions_DoesNotThrow(string input)
    {
        SemVer v = SemVer.Parse(input);
        Assert.NotNull((object?)v);
    }

    // -----------------------------------------------------------------------
    // Parsing — error paths
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("a.b.c")]
    [InlineData("1.2.-1")]
    [InlineData(null!)]
    public void Parse_InvalidInput_ThrowsArgumentException(string? input)
    {
        Assert.Throws<ArgumentException>(() => SemVer.Parse(input!));
    }

    // -----------------------------------------------------------------------
    // ToVersionSort — formula: major*1e12 + minor*1e6 + patch
    // -----------------------------------------------------------------------

    [Fact]
    public void ToVersionSort_ReturnsCorrectFormula()
    {
        SemVer v = SemVer.Parse("1.2.3");
        long expected = 1L * 1_000_000_000_000L + 2L * 1_000_000L + 3L;

        Assert.Equal(expected, v.ToVersionSort());
    }

    [Fact]
    public void ToVersionSort_ZeroVersion_ReturnsZero()
    {
        Assert.Equal(0L, SemVer.Parse("0.0.0").ToVersionSort());
    }

    // -----------------------------------------------------------------------
    // Ordering — 1.10.0 must sort AFTER 1.9.0
    // -----------------------------------------------------------------------

    [Fact]
    public void Compare_MinorVersion_TenGreaterThanNine()
    {
        SemVer v1_9 = SemVer.Parse("1.9.0");
        SemVer v1_10 = SemVer.Parse("1.10.0");

        Assert.True(v1_10.ToVersionSort() > v1_9.ToVersionSort(),
            "1.10.0 must sort after 1.9.0");
    }

    [Fact]
    public void Compare_MajorVersion_TwoGreaterThanOne()
    {
        Assert.True(
            SemVer.Parse("2.0.0").ToVersionSort() > SemVer.Parse("1.99.99").ToVersionSort(),
            "2.0.0 must sort after 1.99.99");
    }

    [Fact]
    public void Compare_PatchVersion_OrderedCorrectly()
    {
        SemVer v100 = SemVer.Parse("1.0.0");
        SemVer v101 = SemVer.Parse("1.0.1");

        Assert.True(v101.ToVersionSort() > v100.ToVersionSort());
    }

    // -----------------------------------------------------------------------
    // Equality (record struct semantics)
    // -----------------------------------------------------------------------

    [Fact]
    public void Equality_SameVersion_Equal()
    {
        Assert.Equal(SemVer.Parse("1.2.3"), SemVer.Parse("1.2.3"));
    }

    [Fact]
    public void Equality_DifferentVersions_NotEqual()
    {
        Assert.NotEqual(SemVer.Parse("1.2.3"), SemVer.Parse("1.2.4"));
    }

    // -----------------------------------------------------------------------
    // ToString — canonical representation
    // -----------------------------------------------------------------------

    [Fact]
    public void ToString_ReturnsOriginalFormat()
    {
        Assert.Equal("1.2.3", SemVer.Parse("1.2.3").ToString());
    }
}
