// SPDX-License-Identifier: MPL-2.0
namespace Grelecy.Diagnostics.Windows;

internal static class UiText
{
    public static string Get(string key, UiLanguage language) => LocalizedText.Get("ui." + key, language);
}
