// SPDX-License-Identifier: MPL-2.0
using System.Collections.ObjectModel;
using System.Text.Json;

namespace Grelecy.Diagnostics;

public static class LocalizedText
{
    private static readonly Lazy<IReadOnlyDictionary<UiLanguage, IReadOnlyDictionary<string, string>>> Catalogs = new(LoadCatalogs);

    /// <summary>Resolve a known key, falling back to English for an unavailable language or translation.</summary>
    public static string Get(string key, UiLanguage language)
    {
        var catalogs = Catalogs.Value;
        var english = catalogs[UiLanguage.English];
        if (!english.TryGetValue(key, out var fallback))
            throw new KeyNotFoundException($"Unknown localization key: {key}");
        return catalogs.TryGetValue(language, out var translated) && translated.TryGetValue(key, out var value)
            && !string.IsNullOrWhiteSpace(value) ? value : fallback;
    }

    /// <summary>Return only this language's embedded entries, without applying fallback.</summary>
    public static IReadOnlyDictionary<string, string> GetTranslations(UiLanguage language) =>
        Catalogs.Value.TryGetValue(language, out var entries) ? entries
            : throw new ArgumentOutOfRangeException(nameof(language), "Unknown application language");

    /// <summary>Report missing, extra or empty entries so tests cannot pass by relying on English fallback.</summary>
    public static IReadOnlyList<string> ValidateCatalogs()
    {
        var errors = new List<string>();
        var english = GetTranslations(UiLanguage.English);
        foreach (var language in LanguageCatalog.All)
        {
            var entries = GetTranslations(language.Language);
            foreach (var key in english.Keys)
            {
                if (!entries.TryGetValue(key, out var value)) errors.Add($"{language.Code}:missing:{key}");
                else if (string.IsNullOrWhiteSpace(value)) errors.Add($"{language.Code}:empty:{key}");
            }
            foreach (var key in entries.Keys)
                if (!english.ContainsKey(key)) errors.Add($"{language.Code}:extra:{key}");
        }
        return errors.AsReadOnly();
    }

    private static IReadOnlyDictionary<UiLanguage, IReadOnlyDictionary<string, string>> LoadCatalogs()
    {
        var catalogs = new Dictionary<UiLanguage, IReadOnlyDictionary<string, string>>();
        var assembly = typeof(LocalizedText).Assembly;
        foreach (var language in LanguageCatalog.All)
        {
            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            using var stream = assembly.GetManifestResourceStream($"Grelecy.Diagnostics.Localization.{language.Code}.json");
            if (stream is not null)
            {
                using var document = JsonDocument.Parse(stream);
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (property.Value.ValueKind != JsonValueKind.String || !entries.TryAdd(property.Name, property.Value.GetString()!))
                        throw new InvalidDataException($"Invalid or duplicate localization entry: {language.Code}:{property.Name}");
                }
            }
            catalogs[language.Language] = new ReadOnlyDictionary<string, string>(entries);
        }
        if (catalogs[UiLanguage.English].Count == 0)
            throw new InvalidDataException("The English localization resource is required");
        return new ReadOnlyDictionary<UiLanguage, IReadOnlyDictionary<string, string>>(catalogs);
    }
}
