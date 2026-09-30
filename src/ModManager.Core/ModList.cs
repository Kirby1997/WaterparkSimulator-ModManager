namespace ModManager.Core;

/// <summary>ConfigurationManager-style hints a mod can attach to a setting.</summary>
public sealed record SettingTags(bool Hidden = false, bool ReadOnly = false, int? Order = null, string? DisplayName = null)
{
    public static SettingTags None { get; } = new();
}

public enum SourceKind
{
    /// <summary>Owned by a plugin running in this game session.</summary>
    Loaded,
    /// <summary>No running plugin owns it; edits go into the file.</summary>
    NotLoaded,
    /// <summary>BepInEx.cfg; the loader reads it only at start-up.</summary>
    Loader,
}

/// <summary>One .cfg file as found on disk, plus what the running game knows about it.</summary>
public sealed record ConfigSource(
    string RelativePath,
    string? Text,
    string? ReadError,
    SourceKind Kind,
    string? LoadedName = null,
    string? LoadedVersion = null,
    IReadOnlySet<string>? BoundKeys = null,
    IReadOnlyDictionary<string, SettingTags>? Tags = null)
{
    public static string KeyOf(string section, string key) => section + "\n" + key;
}

public sealed record SettingView(CfgSetting Setting, string Label, ControlKind Control, bool ReadOnly, bool AppliesLive);

public sealed record SectionView(string Name, IReadOnlyList<SettingView> Settings);

public sealed record ModEntry(string Title, string Version, SourceKind Kind, ConfigSource Source, string? Error, IReadOnlyList<SectionView> Sections)
{
    public string Status => Kind switch
    {
        SourceKind.Loaded => "loaded",
        SourceKind.Loader => "mod loader",
        _ => "not loaded",
    };
}

public static class ModList
{
    public static IReadOnlyList<ModEntry> Build(IEnumerable<ConfigSource> sources) =>
        sources.Select(Entry)
            .OrderBy(m => m.Kind switch { SourceKind.Loaded => 0, SourceKind.NotLoaded => 1, _ => 2 })
            .ThenBy(m => m.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static ModEntry Entry(ConfigSource source)
    {
        var fileName = Path.GetFileNameWithoutExtension(source.RelativePath);
        if (source.Text == null)
        {
            return new ModEntry(source.LoadedName ?? fileName, source.LoadedVersion ?? "", source.Kind, source,
                source.ReadError ?? "Could not read the file.", Array.Empty<SectionView>());
        }

        var document = CfgDocument.Parse(source.Text);
        var sections = document.Settings
            .Select((setting, index) => (Setting: setting, Index: index, Tags: TagsOf(source, setting)))
            .Where(s => !s.Tags.Hidden)
            .GroupBy(s => s.Setting.Section)
            .Select(group => new SectionView(group.Key, group
                .OrderByDescending(s => s.Tags.Order ?? 0)
                .ThenBy(s => s.Index)
                .Select(s => View(source, s.Setting, s.Tags))
                .ToList()))
            .ToList();

        return new ModEntry(source.LoadedName ?? document.PluginName ?? fileName, source.LoadedVersion ?? document.PluginVersion ?? "",
            source.Kind, source, null, sections);
    }

    private static SettingTags TagsOf(ConfigSource source, CfgSetting setting) =>
        source.Tags != null && source.Tags.TryGetValue(ConfigSource.KeyOf(setting.Section, setting.Key), out var tags) ? tags : SettingTags.None;

    private static SettingView View(ConfigSource source, CfgSetting setting, SettingTags tags)
    {
        var live = source.Kind == SourceKind.Loaded && source.BoundKeys?.Contains(ConfigSource.KeyOf(setting.Section, setting.Key)) == true;
        return new SettingView(setting, tags.DisplayName ?? setting.Key, Controls.For(setting), tags.ReadOnly, live);
    }
}
