using System.Globalization;

namespace ModManager.Core;

/// <summary>Input checked against a setting: the raw text to store, or why it cannot be stored.</summary>
public readonly record struct ValueCheck(string? Raw, string? Error)
{
    public bool Ok => Error == null;

    public static ValueCheck Valid(string raw) => new(raw, null);

    public static ValueCheck Invalid(string error) => new(null, error);
}

/// <summary>
/// Turns what the player typed or picked into the text BepInEx stores, using the type name and
/// limits BepInEx wrote into the file.
/// </summary>
public static class ValueRules
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private static readonly Dictionary<string, (decimal Min, decimal Max)> Integers = new()
    {
        ["Byte"] = (byte.MinValue, byte.MaxValue),
        ["SByte"] = (sbyte.MinValue, sbyte.MaxValue),
        ["Int16"] = (short.MinValue, short.MaxValue),
        ["UInt16"] = (ushort.MinValue, ushort.MaxValue),
        ["Int32"] = (int.MinValue, int.MaxValue),
        ["UInt32"] = (uint.MinValue, uint.MaxValue),
        ["Int64"] = (long.MinValue, long.MaxValue),
        ["UInt64"] = (ulong.MinValue, ulong.MaxValue),
    };

    private static readonly HashSet<string> Fractions = new() { "Single", "Double", "Decimal" };

    public static bool IsInteger(string? typeName) => typeName != null && Integers.ContainsKey(typeName);

    public static bool IsNumeric(string? typeName) => IsInteger(typeName) || (typeName != null && Fractions.Contains(typeName));

    /// <summary>The value as the player should see it.</summary>
    public static string Display(CfgSetting setting) => Readable(setting, setting.RawValue);

    public static ValueCheck Check(CfgSetting setting, string input)
    {
        var isString = setting.TypeName == "String";
        if (isString && setting.AcceptableValues.Count == 0) return ValueCheck.Valid(TomlString.Escape(input));

        var text = input.Trim();
        if (text.Contains('\n') || text.Contains('\r')) return ValueCheck.Invalid("Must fit on one line.");

        if (setting.TypeName == "Boolean")
        {
            return bool.TryParse(text, out var flag) ? ValueCheck.Valid(flag ? "true" : "false") : ValueCheck.Invalid("Must be true or false.");
        }
        if (setting.AcceptableValues.Count > 0)
        {
            if (setting.IsFlags) return CheckFlags(setting, text);
            var match = Match(setting, text);
            if (match == null) return ValueCheck.Invalid($"Must be one of: {Allowed(setting)}.");
            return ValueCheck.Valid(isString ? TomlString.Escape(match) : match);
        }
        if (setting.TypeName != null && Integers.TryGetValue(setting.TypeName, out var bounds)) return CheckInteger(setting, text, bounds);
        if (setting.TypeName != null && Fractions.Contains(setting.TypeName)) return CheckFraction(setting, text);
        return ValueCheck.Valid(text);
    }

    /// <summary>The allowed value <paramref name="step"/> places from the current one, wrapping round.</summary>
    public static string Cycle(CfgSetting setting, int step)
    {
        var values = setting.AcceptableValues;
        if (values.Count == 0) return Display(setting);

        var current = Display(setting).Trim();
        var index = -1;
        for (var i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], current, StringComparison.OrdinalIgnoreCase)) index = i;
        }
        if (index < 0) return values[0];
        return values[((index + step) % values.Count + values.Count) % values.Count];
    }

    public static double? ParseNumber(CfgSetting setting) =>
        double.TryParse(setting.RawValue, NumberStyles.Float, Invariant, out var value) ? value : null;

    /// <summary>Text for a slider position, clamped and rounded to a step that suits the range.</summary>
    public static string SliderText(CfgSetting setting, double position)
    {
        if (setting.Range is not { } range) return FormatNumber(position);

        var value = Math.Clamp(position, range.Min, range.Max);
        if (IsInteger(setting.TypeName)) return Math.Round(value).ToString(Invariant);

        var span = range.Max - range.Min;
        var decimals = span <= 0 ? 2 : Math.Clamp(2 - (int)Math.Floor(Math.Log10(span)), 0, 6);
        return Math.Round(value, decimals).ToString(Invariant);
    }

    public static string FormatNumber(double value) => value.ToString("0.######", Invariant);

    /// <summary>The description plus default, range and flag hints, for the line under a control.</summary>
    public static string Details(CfgSetting setting)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(setting.Description)) parts.Add(setting.Description.Trim());

        var facts = new List<string>();
        if (setting.DefaultValue != null)
        {
            var shown = Readable(setting, setting.DefaultValue);
            facts.Add("Default: " + (shown.Length == 0 ? "(empty)" : shown));
        }
        if (setting.Range is { } range) facts.Add($"Range: {FormatNumber(range.Min)} to {FormatNumber(range.Max)}");
        if (setting.IsFlags) facts.Add("Several allowed, separated by commas: " + Allowed(setting));
        if (facts.Count > 0) parts.Add(string.Join(". ", facts) + ".");

        return string.Join("\n", parts);
    }

    private static string Readable(CfgSetting setting, string raw) => setting.TypeName == "String" ? TomlString.Read(raw) : raw;

    private static string? Match(CfgSetting setting, string text) =>
        setting.AcceptableValues.FirstOrDefault(v => string.Equals(v, text, StringComparison.OrdinalIgnoreCase));

    private static string Allowed(CfgSetting setting) => string.Join(", ", setting.AcceptableValues);

    private static ValueCheck CheckFlags(CfgSetting setting, string text)
    {
        var picked = new List<string>();
        foreach (var part in text.Split(',').Select(p => p.Trim()).Where(p => p.Length > 0))
        {
            var match = Match(setting, part);
            if (match == null) return ValueCheck.Invalid($"'{part}' is not one of: {Allowed(setting)}.");
            if (!picked.Contains(match)) picked.Add(match);
        }
        return picked.Count == 0
            ? ValueCheck.Invalid($"Pick at least one of: {Allowed(setting)}.")
            : ValueCheck.Valid(string.Join(", ", picked));
    }

    private static ValueCheck CheckInteger(CfgSetting setting, string text, (decimal Min, decimal Max) bounds)
    {
        if (!decimal.TryParse(text, NumberStyles.AllowLeadingSign, Invariant, out var number)) return ValueCheck.Invalid("Must be a whole number.");
        if (number < bounds.Min || number > bounds.Max)
        {
            return ValueCheck.Invalid($"Must be a whole number from {bounds.Min.ToString(Invariant)} to {bounds.Max.ToString(Invariant)}.");
        }
        return InRange(setting, (double)number, number.ToString(Invariant));
    }

    private static ValueCheck CheckFraction(CfgSetting setting, string text)
    {
        // Players with a comma decimal separator type 1,5.
        var normalised = text.Contains('.') ? text : text.Replace(',', '.');

        if (setting.TypeName == "Decimal")
        {
            return decimal.TryParse(normalised, NumberStyles.Float, Invariant, out var exact)
                ? InRange(setting, (double)exact, exact.ToString(Invariant))
                : ValueCheck.Invalid("Must be a number.");
        }

        if (!double.TryParse(normalised, NumberStyles.Float, Invariant, out var value)) return ValueCheck.Invalid("Must be a number.");
        if (setting.TypeName == "Single")
        {
            var single = (float)value;
            if (!float.IsFinite(single)) return ValueCheck.Invalid("Must be a number.");
            return InRange(setting, single, single.ToString(Invariant));
        }
        if (!double.IsFinite(value)) return ValueCheck.Invalid("Must be a number.");
        return InRange(setting, value, value.ToString("R", Invariant));
    }

    private static ValueCheck InRange(CfgSetting setting, double value, string raw) =>
        setting.Range is { } range && (value < range.Min || value > range.Max)
            ? ValueCheck.Invalid($"Must be from {FormatNumber(range.Min)} to {FormatNumber(range.Max)}.")
            : ValueCheck.Valid(raw);
}
