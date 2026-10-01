using System.Reflection;
using BepInEx.Configuration;
using ModSettings.Core;

namespace ModSettings.Plugin;

/// <summary>
/// Reads the ConfigurationManagerAttributes a mod may attach to a setting. Each mod ships its own
/// copy of that class, so it is matched by name and read through reflection.
/// </summary>
internal static class Tags
{
    public static SettingTags Of(ConfigEntryBase entry)
    {
        var tags = entry.Description?.Tags;
        if (tags == null) return SettingTags.None;

        foreach (var tag in tags)
        {
            if (tag == null || tag.GetType().Name != "ConfigurationManagerAttributes") continue;
            return new SettingTags(
                Hidden: Member(tag, "Browsable") is false,
                ReadOnly: Member(tag, "ReadOnly") is true,
                Order: Member(tag, "Order") as int?,
                DisplayName: Member(tag, "DispName") as string);
        }
        return SettingTags.None;
    }

    private static object Member(object target, string name)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var type = target.GetType();
        return type.GetField(name, flags)?.GetValue(target) ?? type.GetProperty(name, flags)?.GetValue(target);
    }
}
