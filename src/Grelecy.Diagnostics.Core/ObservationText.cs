// SPDX-License-Identifier: MPL-2.0
using System.Globalization;
using System.Text;

namespace Grelecy.Diagnostics;

public static class ObservationText
{
    public const int MaxSingleLineLength = 256;
    public const int MaxNarrativeLength = 4000;

    public static string SingleLine(string? value) => SafeText.Normalize(value);

    /// <summary>Preserve narrative line breaks while discarding invisible control characters.</summary>
    public static string Narrative(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var result = new StringBuilder();
        foreach (var rune in value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').EnumerateRunes())
        {
            string text;
            if (rune.Value == '\n' || Rune.GetUnicodeCategory(rune) is UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator)
                text = "\n";
            else if (Rune.IsWhiteSpace(rune))
                text = " ";
            else if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control or UnicodeCategory.Format)
                continue;
            else
                text = rune.ToString();
            if (result.Length + text.Length > MaxNarrativeLength) break;
            result.Append(text);
        }
        return result.ToString().Trim();
    }

    internal static string EscapeMarkdown(string value)
    {
        var result = new StringBuilder();
        foreach (var c in value)
        {
            if ((c >= '!' && c <= '/') || (c >= ':' && c <= '@') || (c >= '[' && c <= '`') || (c >= '{' && c <= '~'))
                result.Append('\\');
            result.Append(c);
        }
        return result.ToString();
    }
}
