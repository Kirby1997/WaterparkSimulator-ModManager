namespace ModSettings.Core;

/// <summary>
/// Key names across the three ways a setting can hold a key: Unity's Input System <c>Key</c>,
/// the legacy <c>KeyCode</c>, and a BepInEx-style <c>KeyboardShortcut</c> ("F1 + LeftControl").
/// Keys are compared by their Input System name, which is also what the game's controls use.
/// </summary>
public static class KeyNames
{
    public const string Unbound = "None";

    private static readonly string[] KeyTypes = { "Key", "KeyCode", "KeyboardShortcut" };

    private static readonly string[] Modifiers =
    {
        "LeftShift", "RightShift", "LeftCtrl", "RightCtrl", "LeftAlt", "RightAlt", "AltGr", "LeftMeta", "RightMeta",
    };

    // KeyCode names that differ from the Input System's; the first pair for a Key is used when converting back.
    private static readonly (string KeyCode, string Key)[] Renames = BuildRenames();

    private static readonly Dictionary<string, string> KeyCodeToKey =
        Renames.ToDictionary(r => r.KeyCode, r => r.Key, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> KeyToKeyCode = Renames
        .GroupBy(r => r.Key, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.First().KeyCode, StringComparer.OrdinalIgnoreCase);

    public static bool IsKeyType(string? typeName) => typeName != null && KeyTypes.Contains(typeName);

    public static bool IsModifier(string key) => Modifiers.Contains(key, StringComparer.OrdinalIgnoreCase);

    /// <summary>The keys a stored value binds, by Input System name and sorted; null when it binds nothing.</summary>
    public static IReadOnlyList<string>? Parse(string? typeName, string raw)
    {
        if (!IsKeyType(typeName)) return null;

        var parts = typeName == "KeyboardShortcut"
            ? raw.Split(new[] { ' ', '+', ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
            : new[] { raw.Trim() };
        var keys = parts
            .Where(p => p.Length > 0 && !string.Equals(p, Unbound, StringComparison.OrdinalIgnoreCase))
            .Select(ToKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return keys.Count == 0 ? null : keys;
    }

    /// <summary>The key a game control path such as "&lt;Keyboard&gt;/leftCtrl" points at; null for anything else.</summary>
    public static string? FromControlPath(string path)
    {
        const string prefix = "<Keyboard>/";
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;

        var control = path[prefix.Length..];
        // "#(a)" means whatever key types that character on the current layout.
        if (control.Length == 0 || control.StartsWith('#') || control.Contains('/') || control == "anyKey") return null;
        if (control.Length == 1 && char.IsDigit(control[0])) return "Digit" + control;
        return control switch
        {
            "ctrl" => "LeftCtrl",
            "shift" => "LeftShift",
            "alt" => "LeftAlt",
            _ => char.ToUpperInvariant(control[0]) + control[1..],
        };
    }

    /// <summary>The value to store for a key the player pressed, with any modifiers they held.</summary>
    public static ValueCheck Capture(CfgSetting setting, string key, IReadOnlyList<string> heldModifiers)
    {
        var raw = setting.TypeName switch
        {
            "KeyCode" => ToKeyCode(key),
            "KeyboardShortcut" => string.Join(" + ", new[] { ToKeyCode(key) }.Concat(heldModifiers
                .Where(m => !string.Equals(m, key, StringComparison.OrdinalIgnoreCase))
                .Select(ToKeyCode))),
            _ => key,
        };

        if (setting.AcceptableValues.Count > 0)
        {
            var match = setting.AcceptableValues.FirstOrDefault(v => string.Equals(v, raw, StringComparison.OrdinalIgnoreCase));
            if (match == null) return ValueCheck.Invalid($"{key} cannot be used for this setting.");
            raw = match;
        }
        return ValueCheck.Valid(raw);
    }

    private static string ToKey(string name) => KeyCodeToKey.TryGetValue(name, out var key) ? key : name;

    private static string ToKeyCode(string key) => KeyToKeyCode.TryGetValue(key, out var keyCode) ? keyCode : key;

    private static (string, string)[] BuildRenames()
    {
        var renames = new List<(string, string)>
        {
            ("Return", "Enter"),
            ("LeftControl", "LeftCtrl"),
            ("RightControl", "RightCtrl"),
            ("LeftWindows", "LeftMeta"),
            ("RightWindows", "RightMeta"),
            ("LeftCommand", "LeftMeta"),
            ("RightCommand", "RightMeta"),
            ("LeftApple", "LeftMeta"),
            ("RightApple", "RightMeta"),
            ("Print", "PrintScreen"),
            ("Menu", "ContextMenu"),
            ("KeypadDivide", "NumpadDivide"),
            ("KeypadMultiply", "NumpadMultiply"),
            ("KeypadMinus", "NumpadMinus"),
            ("KeypadPlus", "NumpadPlus"),
            ("KeypadPeriod", "NumpadPeriod"),
            ("KeypadEquals", "NumpadEquals"),
            ("KeypadEnter", "NumpadEnter"),
        };
        for (var digit = 0; digit <= 9; digit++)
        {
            renames.Add(("Alpha" + digit, "Digit" + digit));
            renames.Add(("Keypad" + digit, "Numpad" + digit));
        }
        return renames.ToArray();
    }
}
