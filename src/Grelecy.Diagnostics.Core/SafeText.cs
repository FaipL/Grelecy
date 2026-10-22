// SPDX-License-Identifier: MPL-2.0
using System.Globalization;
using System.Text;

namespace Grelecy.Diagnostics;

public static class SafeText
{
    /// <summary>Keep short, single-line values; discard invisible directional controls.</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var result = new StringBuilder();
        var pendingSpace = false;
        foreach (var rune in value.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
            {
                pendingSpace = result.Length > 0;
                continue;
            }
            if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control or UnicodeCategory.Format
                or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator)
                continue;
            var required = rune.Utf16SequenceLength + (pendingSpace ? 1 : 0);
            if (result.Length + required > 256) break;
            if (pendingSpace) result.Append(' ');
            result.Append(rune.ToString());
            pendingSpace = false;
        }
        return result.ToString();
    }
}
