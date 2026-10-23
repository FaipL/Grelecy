// SPDX-License-Identifier: MPL-2.0
using System.Globalization;

namespace Grelecy.Diagnostics;

public sealed record LanguageInfo(UiLanguage Language, string Code, string NativeName, bool IsRightToLeft);

public static class LanguageCatalog
{
    public static IReadOnlyList<LanguageInfo> All { get; } = Array.AsReadOnly<LanguageInfo>(
    [
        new(UiLanguage.French, "fr", "Français", false),
        new(UiLanguage.English, "en", "English", false),
        new(UiLanguage.Italian, "it", "Italiano", false),
        new(UiLanguage.Spanish, "es", "Español", false),
        new(UiLanguage.German, "de", "Deutsch", false),
        new(UiLanguage.Dutch, "nl", "Nederlands", false),
        new(UiLanguage.Portuguese, "pt", "Português", false),
        new(UiLanguage.Chinese, "zh", "简体中文", false),
        new(UiLanguage.Korean, "ko", "한국어", false),
        new(UiLanguage.Japanese, "ja", "日本語", false),
        new(UiLanguage.Arabic, "ar", "العربية", true),
        new(UiLanguage.Polish, "pl", "Polski", false),
        new(UiLanguage.Swedish, "sv", "Svenska", false),
        new(UiLanguage.Norwegian, "nb", "Norsk bokmål", false)
    ]);

    public static LanguageInfo Get(UiLanguage language) =>
        All.FirstOrDefault(item => item.Language == language) ?? All[1];

    public static UiLanguage FromCulture(CultureInfo? culture)
    {
        var code = culture?.TwoLetterISOLanguageName;
        if (code is "no" or "nn") code = "nb";
        return All.FirstOrDefault(item => string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase))?.Language
            ?? UiLanguage.English;
    }
}
