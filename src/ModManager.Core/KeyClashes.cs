using System.Text.Json;

namespace ModManager.Core;

/// <summary>One place a key is bound: a mod setting or one of the game's controls.</summary>
public sealed record KeyBinding(string Id, string Label, IReadOnlySet<string> Keys, bool FromGame);

public static class KeyClashes
{
    public static string IdOf(ModEntry mod, SettingView view) =>
        mod.Source.RelativePath + "\n" + view.Setting.Section + "\n" + view.Setting.Key;

    /// <summary>The bound key settings of every mod.</summary>
    public static IEnumerable<KeyBinding> FromMods(IEnumerable<ModEntry> mods)
    {
        foreach (var mod in mods)
        {
            foreach (var view in mod.Sections.SelectMany(s => s.Settings))
            {
                if (view.Control != ControlKind.Key) continue;
                var keys = KeyNames.Parse(view.Setting.TypeName, view.Setting.RawValue);
                if (keys == null) continue;
                yield return new KeyBinding(IdOf(mod, view), $"{mod.Title}: {view.Label}",
                    new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase), false);
            }
        }
    }

    /// <summary>
    /// For each mod binding that shares its exact keys with another binding, the labels of the
    /// others. The game binding one key to several controls is its own business and not reported.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Find(IEnumerable<KeyBinding> bindings)
    {
        var all = bindings.ToList();
        var clashes = new Dictionary<string, IReadOnlyList<string>>();
        foreach (var binding in all.Where(b => !b.FromGame))
        {
            var others = all
                .Where(other => other.Id != binding.Id && other.Keys.SetEquals(binding.Keys))
                .Select(other => other.Label)
                .ToList();
            if (others.Count > 0) clashes[binding.Id] = others;
        }
        return clashes;
    }
}

/// <summary>The game's keyboard controls, read from its input action assets as JSON.</summary>
public static class GameBindings
{
    /// <param name="assetJsons">Each asset as written by InputActionAsset.ToJson().</param>
    /// <param name="overridesJson">The player's rebinds as written by SaveBindingOverridesAsJson().</param>
    public static IReadOnlyList<KeyBinding> FromJson(IEnumerable<string> assetJsons, string? overridesJson)
    {
        var overrides = Overrides(overridesJson);
        var bindings = new List<KeyBinding>();
        foreach (var json in assetJsons)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                if (!document.RootElement.TryGetProperty("maps", out var maps)) continue;
                foreach (var map in maps.EnumerateArray())
                {
                    var mapName = Text(map, "name");
                    if (!map.TryGetProperty("bindings", out var mapBindings)) continue;
                    foreach (var binding in mapBindings.EnumerateArray())
                    {
                        if (Flag(binding, "isComposite")) continue;
                        var id = Text(binding, "id");
                        var path = overrides.TryGetValue(id, out var overridden) ? overridden : Text(binding, "path");
                        var key = KeyNames.FromControlPath(path);
                        if (key == null) continue;

                        var part = Flag(binding, "isPartOfComposite") && Text(binding, "name").Length > 0 ? $" ({Text(binding, "name")})" : "";
                        bindings.Add(new KeyBinding("game\n" + id, $"Game: {mapName}/{Text(binding, "action")}{part}",
                            new HashSet<string>(new[] { key }, StringComparer.OrdinalIgnoreCase), true));
                    }
                }
            }
            catch (JsonException)
            {
                // Without the game's controls, clashes between mods are still found.
            }
        }
        return bindings;
    }

    private static Dictionary<string, string> Overrides(string? json)
    {
        var overrides = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(json)) return overrides;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("bindings", out var bindings)) return overrides;
            foreach (var binding in bindings.EnumerateArray())
            {
                var id = Text(binding, "id");
                if (id.Length > 0) overrides[id] = Text(binding, "path");
            }
        }
        catch (JsonException)
        {
        }
        return overrides;
    }

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    private static bool Flag(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
