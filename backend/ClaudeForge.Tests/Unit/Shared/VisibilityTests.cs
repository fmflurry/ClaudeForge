using ClaudeForge.Core.Shared;

namespace ClaudeForge.Tests.Unit.Shared;

/// <summary>
/// Unit tests for the Visibility value object (0% coverage before this file).
/// Mirrors the OrgRole test pattern.
/// </summary>
public sealed class VisibilityTests
{
    // -----------------------------------------------------------------------
    // Parse — happy path: singletons
    // -----------------------------------------------------------------------

    [Fact]
    public void Parse_Public_ReturnsSingletonPublic()
    {
        Visibility result = Visibility.Parse("public");

        Assert.Same(Visibility.Public, result);
    }

    [Fact]
    public void Parse_Private_ReturnsSingletonPrivate()
    {
        Visibility result = Visibility.Parse("private");

        Assert.Same(Visibility.Private, result);
    }

    [Fact]
    public void Parse_Public_ValueIsPublic()
    {
        Visibility result = Visibility.Parse("public");

        Assert.Equal("public", result.Value);
    }

    [Fact]
    public void Parse_Private_ValueIsPrivate()
    {
        Visibility result = Visibility.Parse("private");

        Assert.Equal("private", result.Value);
    }

    // -----------------------------------------------------------------------
    // Parse — invalid input → ArgumentException
    // -----------------------------------------------------------------------

    [Fact]
    public void Parse_Null_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Visibility.Parse(null!));
    }

    [Fact]
    public void Parse_WhitespaceOnly_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Visibility.Parse("   "));
    }

    [Fact]
    public void Parse_EmptyString_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Visibility.Parse(string.Empty));
    }

    [Fact]
    public void Parse_UnknownValue_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Visibility.Parse("restricted"));
    }

    [Fact]
    public void Parse_CaseMismatch_ThrowsArgumentException()
    {
        // "Public" (capital P) is not in the valid set — value-object is case-sensitive.
        Assert.Throws<ArgumentException>(() => Visibility.Parse("Public"));
    }

    [Fact]
    public void Parse_UnknownValue_ExceptionMessageContainsValue()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => Visibility.Parse("internal"));

        Assert.Contains("internal", ex.Message);
    }

    // -----------------------------------------------------------------------
    // ToString
    // -----------------------------------------------------------------------

    [Fact]
    public void ToString_Public_ReturnsPublicString()
    {
        Assert.Equal("public", Visibility.Public.ToString());
    }

    [Fact]
    public void ToString_Private_ReturnsPrivateString()
    {
        Assert.Equal("private", Visibility.Private.ToString());
    }

    // -----------------------------------------------------------------------
    // Record equality (sealed record behaviour)
    // -----------------------------------------------------------------------

    [Fact]
    public void Public_StaticField_EqualsItself()
    {
        Assert.Equal(Visibility.Public, Visibility.Public);
    }

    [Fact]
    public void Public_And_Private_AreNotEqual()
    {
        Assert.NotEqual(Visibility.Public, Visibility.Private);
    }

    [Fact]
    public void ParsePublic_SameReference_AsStaticField()
    {
        // Parse returns the singleton — reference equality must hold.
        object result = Visibility.Parse("public");
        Assert.Same(Visibility.Public, result);
    }

    [Fact]
    public void ParsePrivate_SameReference_AsStaticField()
    {
        object result = Visibility.Parse("private");
        Assert.Same(Visibility.Private, result);
    }
}
