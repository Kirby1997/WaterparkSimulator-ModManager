using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using ModManager.Core;

namespace ModManager.Plugin;

/// <summary>
/// Finds every .cfg under BepInEx/config, works out which ones belong to a running plugin, and
/// stores edits: through the plugin's ConfigEntry when there is one, straight into the file
/// otherwise.
/// </summary>
internal sealed class ConfigStore
{
    private readonly ManualLogSource _log;
    private Dictionary<string, Live> _live = new(StringComparer.OrdinalIgnoreCase);

    public ConfigStore(ManualLogSource log) => _log = log;

    public IReadOnlyList<ModEntry> Mods { get; private set; } = Array.Empty<ModEntry>();

    /// <summary>Re-reads every file; loaded configs whose file was changed outside the game are reloaded first.</summary>
    public void Refresh()
    {
        _live = LiveConfigFiles();
        var sources = Directory.EnumerateFiles(Paths.ConfigPath, "*.cfg", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cfg", StringComparison.OrdinalIgnoreCase))
            .Select(Source)
            .ToList();
        Mods = ModList.Build(sources);
    }

    /// <summary>Checks and stores a value. Returns null when it was saved, otherwise the reason it was not.</summary>
    public string Apply(ModEntry mod, SettingView view, string input)
    {
        var setting = view.Setting;
        var check = ValueRules.Check(setting, input);
        if (!check.Ok) return check.Error;

        var path = Path.GetFullPath(Path.Combine(Paths.ConfigPath, mod.Source.RelativePath));
        try
        {
            _live.TryGetValue(path, out var live);
            var entry = live == null ? null : Entry(live.File, setting);
            if (entry != null) return ApplyToEntry(live.File, entry, check.Raw);

            WriteFile(path, setting, check.Raw);
            // A loaded config keeps keys it has no entry for in memory and would write the old value back on its next save.
            live?.File.Reload();
            return null;
        }
        catch (Exception e)
        {
            _log.LogError($"Could not save {setting.Section}.{setting.Key} in {mod.Source.RelativePath}: {e}");
            return "Could not save: " + e.Message;
        }
        finally
        {
            Refresh();
        }
    }

    private ConfigSource Source(string path)
    {
        var full = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(Paths.ConfigPath, full);
        var isLoader = string.Equals(full, Path.GetFullPath(Paths.BepInExConfigPath), StringComparison.OrdinalIgnoreCase);

        string text;
        try
        {
            text = ReadShared(full);
        }
        catch (Exception e)
        {
            return new ConfigSource(relative, null, e.Message, isLoader ? SourceKind.Loader : SourceKind.NotLoaded);
        }

        if (!_live.TryGetValue(full, out var live)) return new ConfigSource(relative, text, null, isLoader ? SourceKind.Loader : SourceKind.NotLoaded);

        ReloadIfChangedOnDisk(live.File, text, relative);
        var bound = new HashSet<string>();
        var tags = new Dictionary<string, SettingTags>();
        foreach (var entry in live.File.Values)
        {
            var key = ConfigSource.KeyOf(entry.Definition.Section, entry.Definition.Key);
            bound.Add(key);
            tags[key] = Tags.Of(entry);
        }
        return new ConfigSource(relative, text, null, isLoader ? SourceKind.Loader : SourceKind.Loaded,
            live.Owner?.Name, live.Owner?.Version?.ToString(), bound, tags);
    }

    private void ReloadIfChangedOnDisk(ConfigFile file, string text, string relative)
    {
        try
        {
            foreach (var setting in CfgDocument.Parse(text).Settings)
            {
                var entry = Entry(file, setting);
                if (entry == null || entry.GetSerializedValue() == setting.RawValue) continue;
                file.Reload();
                _log.LogInfo($"{relative} was changed outside the game; reloaded it.");
                return;
            }
        }
        catch (Exception e)
        {
            _log.LogWarning($"Could not compare {relative} with its loaded settings: {e.Message}");
        }
    }

    private static ConfigEntryBase Entry(ConfigFile file, CfgSetting setting)
    {
        try
        {
            var definition = new ConfigDefinition(setting.Section, setting.Key);
            return file.ContainsKey(definition) ? file[definition] : null;
        }
        catch (ArgumentException)
        {
            // A key BepInEx itself would refuse to bind; it can only be a file setting.
            return null;
        }
    }

    private static string ApplyToEntry(ConfigFile file, ConfigEntryBase entry, string raw)
    {
        object wanted;
        try
        {
            wanted = TomlTypeConverter.ConvertToValue(raw, entry.SettingType);
        }
        catch (Exception e)
        {
            return "Not a valid value: " + e.Message;
        }

        entry.BoxedValue = wanted;
        if (!file.SaveOnConfigSet) file.Save();
        return Equals(entry.BoxedValue, wanted) ? null : $"The mod changed that value to {entry.GetSerializedValue()}.";
    }

    private static void WriteFile(string path, CfgSetting setting, string raw)
    {
        // Read again rather than trust the copy on screen, in case the file changed since.
        var document = CfgDocument.Parse(ReadShared(path));
        var current = document.Settings.FirstOrDefault(s => s.Section == setting.Section && s.Key == setting.Key)
                      ?? throw new InvalidOperationException($"{setting.Key} is no longer in the file.");

        var temp = path + ".modmanager.tmp";
        File.WriteAllText(temp, document.WithValue(current, raw), new UTF8Encoding(false));
        File.Move(temp, path, overwrite: true);
    }

    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static Dictionary<string, Live> LiveConfigFiles()
    {
        var live = new Dictionary<string, Live>(StringComparer.OrdinalIgnoreCase);
        void Add(ConfigFile file, BepInPlugin owner)
        {
            if (file?.ConfigFilePath != null) live.TryAdd(Path.GetFullPath(file.ConfigFilePath), new Live(file, owner));
        }

        foreach (var info in IL2CPPChainloader.Instance.Plugins.Values)
        {
            if (info.Instance is BasePlugin plugin) Add(plugin.Config, info.Metadata);
        }
        Add(ConfigFile.CoreConfig, null);
        return live;
    }

    // A class, not a record: records need NullableAttribute, which clashes with the interop assemblies' copy.
    private sealed class Live
    {
        public Live(ConfigFile file, BepInPlugin owner)
        {
            File = file;
            Owner = owner;
        }

        public ConfigFile File { get; }
        public BepInPlugin Owner { get; }
    }
}
