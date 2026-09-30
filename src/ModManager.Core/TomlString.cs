using System.Text;
using System.Text.RegularExpressions;

namespace ModManager.Core;

/// <summary>
/// BepInEx's escaping for string settings, copied from its TomlTypeConverter so that a value
/// written into a file reads back exactly as BepInEx would read it. Its quirk is kept: a
/// backslash is written as is, not doubled.
/// </summary>
public static class TomlString
{
    // BepInEx does not unescape values that look like Windows paths.
    private static readonly Regex WindowsPath = new(@"^""?\w:\\(?!\\)(?!.+\\\\)");

    public static string Escape(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var builder = new StringBuilder(text.Length + 2);
        foreach (var c in text)
        {
            builder.Append(c switch
            {
                '\0' => "\\0",
                '\a' => "\\a",
                '\b' => "\\b",
                '\t' => "\\t",
                '\n' => "\\n",
                '\v' => "\\v",
                '\f' => "\\f",
                '\r' => "\\r",
                '\'' => "\\'",
                '"' => "\\\"",
                _ => c.ToString(),
            });
        }
        return builder.ToString();
    }

    public static string Unescape(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var builder = new StringBuilder(text.Length);
        var start = 0;
        while (start < text.Length)
        {
            var slash = text.IndexOf('\\', start);
            if (slash < 0 || slash == text.Length - 1) slash = text.Length;
            builder.Append(text, start, slash - start);
            if (slash >= text.Length) break;

            var next = text[slash + 1];
            switch (next)
            {
                case '0': builder.Append('\0'); break;
                case 'a': builder.Append('\a'); break;
                case 'b': builder.Append('\b'); break;
                case 't': builder.Append('\t'); break;
                case 'n': builder.Append('\n'); break;
                case 'v': builder.Append('\v'); break;
                case 'f': builder.Append('\f'); break;
                case 'r': builder.Append('\r'); break;
                case '\'': builder.Append('\''); break;
                case '"': builder.Append('"'); break;
                case '\\': builder.Append('\\'); break;
                default: builder.Append('\\').Append(next); break;
            }
            start = slash + 2;
        }
        return builder.ToString();
    }

    /// <summary>The string BepInEx reads from a raw value in a .cfg file.</summary>
    public static string Read(string raw) => WindowsPath.IsMatch(raw) ? raw : Unescape(raw);
}
