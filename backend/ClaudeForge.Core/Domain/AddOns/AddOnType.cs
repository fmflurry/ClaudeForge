namespace ClaudeForge.Core.Domain.AddOns;

/// <summary>
/// The five recognised add-on sub-type values.
/// The "Plugin" member is kept so that the existing "plugin" token in
/// <c>PluginManifestValidator.ValidTypes</c> can be expressed as an enum value
/// without renaming the external wire token.
/// </summary>
public enum AddOnType
{
    Skill,
    Hook,
    Agent,
    Command,
    Plugin,
}
