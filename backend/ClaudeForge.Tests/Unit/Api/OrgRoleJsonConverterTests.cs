using System.Text;
using System.Text.Json;
using ClaudeForge.Api.Infrastructure.Serialization;
using ClaudeForge.Core.Identity;

namespace ClaudeForge.Tests.Unit.Api;

/// <summary>
/// Unit tests for <see cref="OrgRoleJsonConverter"/>:
///   - Read: each valid role value (owner, admin, member)
///   - Read: null/whitespace → JsonException
///   - Read: unknown value → ArgumentException / JsonException
///   - Write: each valid role serializes to its string value
/// </summary>
public sealed class OrgRoleJsonConverterTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static JsonSerializerOptions OptionsWithConverter()
    {
        JsonSerializerOptions opts = new();
        opts.Converters.Add(new OrgRoleJsonConverter());
        return opts;
    }

    private static OrgRole Deserialize(string json) =>
        JsonSerializer.Deserialize<OrgRole>(json, OptionsWithConverter())!;

    private static string Serialize(OrgRole role) =>
        JsonSerializer.Serialize(role, OptionsWithConverter());

    // -----------------------------------------------------------------------
    // Read — happy path
    // -----------------------------------------------------------------------

    [Fact]
    public void Read_Owner_ReturnsOwnerRole()
    {
        OrgRole role = Deserialize("\"owner\"");

        Assert.Equal(OrgRole.Owner, role);
        Assert.Equal("owner", role.Value);
    }

    [Fact]
    public void Read_Admin_ReturnsAdminRole()
    {
        OrgRole role = Deserialize("\"admin\"");

        Assert.Equal(OrgRole.Admin, role);
        Assert.Equal("admin", role.Value);
    }

    [Fact]
    public void Read_Member_ReturnsMemberRole()
    {
        OrgRole role = Deserialize("\"member\"");

        Assert.Equal(OrgRole.Member, role);
        Assert.Equal("member", role.Value);
    }

    // -----------------------------------------------------------------------
    // Read — invalid input
    // -----------------------------------------------------------------------

    [Fact]
    public void Read_NullToken_ReturnsNullWithoutHittingConverter()
    {
        // JSON null for a reference type is handled by JsonSerializer before the converter's
        // Read method is invoked, so it returns null rather than throwing.
        // This documents the actual behavior so coverage of the null/whitespace branch
        // is achieved via the empty-string and whitespace tests below.
        OrgRole? result = JsonSerializer.Deserialize<OrgRole?>("null", OptionsWithConverter());
        Assert.Null(result);
    }

    [Fact]
    public void Read_WhitespaceString_ThrowsJsonException()
    {
        Assert.Throws<JsonException>(() => Deserialize("\"   \""));
    }

    [Fact]
    public void Read_EmptyString_ThrowsJsonException()
    {
        Assert.Throws<JsonException>(() => Deserialize("\"\""));
    }

    [Fact]
    public void Read_UnknownRole_ThrowsException()
    {
        // "superadmin" is not a valid OrgRole — should propagate as some exception
        Assert.ThrowsAny<Exception>(() => Deserialize("\"superadmin\""));
    }

    [Fact]
    public void Read_CaseMismatch_ThrowsException()
    {
        // "Owner" (capital O) is not recognised — OrgRole.Parse is case-sensitive
        Assert.ThrowsAny<Exception>(() => Deserialize("\"Owner\""));
    }

    // -----------------------------------------------------------------------
    // Write — each role serializes to its string value
    // -----------------------------------------------------------------------

    [Fact]
    public void Write_Owner_SerializesToOwnerString()
    {
        string json = Serialize(OrgRole.Owner);

        Assert.Equal("\"owner\"", json);
    }

    [Fact]
    public void Write_Admin_SerializesToAdminString()
    {
        string json = Serialize(OrgRole.Admin);

        Assert.Equal("\"admin\"", json);
    }

    [Fact]
    public void Write_Member_SerializesToMemberString()
    {
        string json = Serialize(OrgRole.Member);

        Assert.Equal("\"member\"", json);
    }

    // -----------------------------------------------------------------------
    // Round-trip
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("owner")]
    [InlineData("admin")]
    [InlineData("member")]
    public void RoundTrip_SerializeDeserialize_ReturnsSameRole(string roleName)
    {
        OrgRole original = OrgRole.Parse(roleName);
        string json = Serialize(original);
        OrgRole deserialized = Deserialize(json);

        Assert.Equal(original, deserialized);
    }

    // -----------------------------------------------------------------------
    // Write via Utf8JsonWriter directly (tests converter Write branch)
    // -----------------------------------------------------------------------

    [Fact]
    public void Write_DirectWriter_ProducesCorrectJsonBytes()
    {
        OrgRoleJsonConverter converter = new();
        using MemoryStream ms = new();
        using Utf8JsonWriter writer = new(ms);

        converter.Write(writer, OrgRole.Admin, new JsonSerializerOptions());
        writer.Flush();

        string result = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Equal("\"admin\"", result);
    }
}
