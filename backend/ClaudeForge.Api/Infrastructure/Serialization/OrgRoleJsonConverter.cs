using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudeForge.Core.Identity;

namespace ClaudeForge.Api.Infrastructure.Serialization;

/// <summary>
/// JSON converter for <see cref="OrgRole"/> that serializes the value object
/// as its string value (e.g. "owner", "admin", "member") rather than as an object.
/// </summary>
public sealed class OrgRoleJsonConverter : JsonConverter<OrgRole>
{
    public override OrgRole Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
            throw new JsonException("OrgRole value cannot be null or whitespace.");

        return OrgRole.Parse(value);
    }

    public override void Write(Utf8JsonWriter writer, OrgRole value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
