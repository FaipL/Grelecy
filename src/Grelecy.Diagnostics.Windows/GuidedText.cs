// SPDX-License-Identifier: MPL-2.0
namespace Grelecy.Diagnostics.Windows;

internal static class GuidedText
{
    public static string Get(string key, UiLanguage language) => LocalizedText.Get("guided." + key, language);
}
