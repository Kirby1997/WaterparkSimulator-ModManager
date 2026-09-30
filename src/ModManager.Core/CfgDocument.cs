using System.Globalization;
using System.Text.RegularExpressions;

namespace ModManager.Core;

public readonly record struct NumberRange(double Min, double Max);

/// <summary>One key in a .cfg file, with the metadata BepInEx writes in the comments above it.</summary>
public sealed record CfgSetting(
    string Section,
    string Key,
    string RawValue,
    int Line,
    string Description,
    string? TypeName,
    string? DefaultValue,
    IReadOnlyList<string> AcceptableValues,
    NumberRange? Range,
    bool IsFlags);

/// <summary>
/// A BepInEx .cfg file, read the way ConfigFile.Reload reads it. Changes are written back one
/// value line at a time, so comments, order and line endings stay as they were.
/// </summary>
public sealed class CfgDocument
{
    private static readonly Regex CreatedBy = new(@"^## Settings file was created by plugin (.+) v(\S+)$");
    private static readonly Regex RangeLine = new(@"^# Acceptable value range: From (.+) to (.+)$");
    private const string GuidPrefix = "## Plugin GUID:";
    private const string TypePrefix = "# Setting type:";
    private const string DefaultPrefix = "# Default value:";
    private const string ValuesPrefix = "# Acceptable values:";
    private const string FlagsPrefix = "# Multiple values can be set at the same time";

    private readonly string[] _lines;

    private CfgDocument(string[] lines, IReadOnlyList<CfgSetting> settings)
    {
        _lines = lines;
        Settings = settings;
    }

    public string? PluginName { get; private init; }
    public string? PluginVersion { get; private init; }
    public string? PluginGuid { get; private init; }
    public IReadOnlyList<CfgSetting> Settings { get; }

    /// <summary>The file's text with one setting's value replaced and every other line untouched.</summary>
    public string WithValue(CfgSetting setting, string rawValue)
    {
        if (rawValue.Contains('\n') || rawValue.Contains('\r'))
            throw new ArgumentException("A value must fit on one line.", nameof(rawValue));

        var line = setting.Line < _lines.Length ? _lines[setting.Line] : "";
        var equals = line.IndexOf('=');
        if (equals < 0 || line[..equals].Trim() != setting.Key)
            throw new InvalidOperationException($"Line {setting.Line + 1} does not hold {setting.Key}.");

        var lines = (string[])_lines.Clone();
        var ending = line.EndsWith('\r') ? "\r" : "";
        lines[setting.Line] = line[..equals].TrimEnd() + " = " + rawValue + ending;
        return string.Join('\n', lines);
    }

    public static CfgDocument Parse(string text)
    {
        var lines = text.Split('\n');
        var settings = new List<CfgSetting>();
        string? name = null, version = null, guid = null;
        var section = "";
        var inHeader = true;
        var pending = new Pending();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0) continue;

            if (line.StartsWith('#'))
            {
                if (inHeader && CreatedBy.Match(line) is { Success: true } created)
                {
                    name = created.Groups[1].Value;
                    version = created.Groups[2].Value;
                }
                else if (inHeader && line.StartsWith(GuidPrefix))
                {
                    guid = line[GuidPrefix.Length..].Trim();
                }
                else
                {
                    pending.Read(line);
                }
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1];
                inHeader = false;
                pending = new Pending();
                continue;
            }

            // BepInEx skips lines without '=' as well.
            var parts = line.Split('=', 2);
            if (parts.Length != 2) continue;
            settings.Add(pending.ToSetting(section, parts[0].Trim(), parts[1].Trim(), i));
            pending = new Pending();
        }

        return new CfgDocument(lines, settings) { PluginName = name, PluginVersion = version, PluginGuid = guid };
    }

    /// <summary>
    /// A number as BepInEx wrote it. Ranges are written with the game's culture, so a comma
    /// decimal separator is possible.
    /// </summary>
    internal static double? ParseNumber(string text)
    {
        text = text.Trim();
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return value;
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)) return value;
        return null;
    }

    /// <summary>The comments seen since the last key.</summary>
    private sealed class Pending
    {
        private readonly List<string> _description = new();
        private string? _type;
        private string? _default;
        private IReadOnlyList<string> _values = Array.Empty<string>();
        private NumberRange? _range;
        private bool _flags;

        public void Read(string line)
        {
            if (line.StartsWith("##"))
            {
                _description.Add(line[2..].Trim());
            }
            else if (line.StartsWith(TypePrefix))
            {
                _type = line[TypePrefix.Length..].Trim();
            }
            else if (line.StartsWith(DefaultPrefix))
            {
                _default = line[DefaultPrefix.Length..].Trim();
            }
            else if (line.StartsWith(ValuesPrefix))
            {
                _values = line[ValuesPrefix.Length..].Split(',').Select(v => v.Trim()).Where(v => v.Length > 0).ToArray();
            }
            else if (line.StartsWith(FlagsPrefix))
            {
                _flags = true;
            }
            else if (RangeLine.Match(line) is { Success: true } range
                     && ParseNumber(range.Groups[1].Value) is { } min
                     && ParseNumber(range.Groups[2].Value) is { } max)
            {
                _range = new NumberRange(min, max);
            }
        }

        public CfgSetting ToSetting(string section, string key, string value, int line) =>
            new(section, key, value, line, string.Join("\n", _description).Trim(), _type, _default, _values, _range, _flags);
    }
}
