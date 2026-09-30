namespace ModManager.Tests;

internal static class Settings
{
    public static CfgSetting Make(string? type, string raw = "", IReadOnlyList<string>? values = null, NumberRange? range = null,
        bool flags = false, string? defaultValue = null, string description = "") =>
        new("General", "Key", raw, 1, description, type, defaultValue, values ?? Array.Empty<string>(), range, flags);
}
