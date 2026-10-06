using Microsoft.Windows.ApplicationModel.Resources;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ApplicationLanguages = Microsoft.Windows.Globalization.ApplicationLanguages;

namespace PCGet.Services;

public static class LocalizationService
{
    private const string DefaultLanguage = "en-US";
    private static ResourceLoader? _resourceLoader;

    public static string CurrentLanguage => ApplicationLanguages.PrimaryLanguageOverride.Length > 0
        ? ApplicationLanguages.PrimaryLanguageOverride
        : ApplicationLanguages.Languages.FirstOrDefault() ?? DefaultLanguage;

    public static IReadOnlyList<LocalizationOption> AvailableLanguages { get; } = DiscoverLanguages();

    public static void ApplyLanguage(string? language)
    {
        if (!string.IsNullOrWhiteSpace(language) &&
            AvailableLanguages.Any(option => string.Equals(option.LanguageTag, language, StringComparison.OrdinalIgnoreCase)))
        {
            ApplicationLanguages.PrimaryLanguageOverride = language;
        }

        _resourceLoader = new ResourceLoader();
    }

    public static string GetString(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        try
        {
            _resourceLoader ??= new ResourceLoader();

            var value = _resourceLoader.GetString(key);
            return string.IsNullOrEmpty(value) ? key : value;
        }
        catch
        {
            return key;
        }
    }

    public static string Format(string key, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, GetString(key), args);
    }

    private static IReadOnlyList<LocalizationOption> DiscoverLanguages()
    {
        var languages = new List<LocalizationOption>();
        var localizationPath = Path.Combine(AppContext.BaseDirectory, "Localization");

        if (Directory.Exists(localizationPath))
        {
            foreach (var directory in Directory.GetDirectories(localizationPath))
            {
                var languageTag = Path.GetFileName(directory);

                if (string.IsNullOrWhiteSpace(languageTag))
                    continue;

                try
                {
                    var culture = CultureInfo.GetCultureInfo(languageTag);
                    languages.Add(new LocalizationOption(languageTag, culture.NativeName));
                }
                catch (CultureNotFoundException)
                {
                }
            }
        }

        if (!languages.Any(option => string.Equals(option.LanguageTag, DefaultLanguage, StringComparison.OrdinalIgnoreCase)))
            languages.Add(new LocalizationOption(DefaultLanguage, CultureInfo.GetCultureInfo(DefaultLanguage).NativeName));

        return languages
            .OrderBy(option => option.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }
}

public sealed record LocalizationOption(string LanguageTag, string DisplayName)
{
    public override string ToString() => DisplayName;
}