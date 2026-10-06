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
    private const string LanguageManifestPath = "Localization\\languages.txt";
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
        var languageTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var manifestPath = Path.Combine(AppContext.BaseDirectory, LanguageManifestPath);

        if (File.Exists(manifestPath))
        {
            foreach (var line in File.ReadAllLines(manifestPath))
            {
                var languageTag = line.Trim().TrimEnd('\\', '/');

                if (!string.IsNullOrWhiteSpace(languageTag))
                    languageTags.Add(languageTag);
            }
        }

        languageTags.Add(DefaultLanguage);

        return languageTags
            .Select(CreateLocalizationOption)
            .Where(option => option is not null)
            .Cast<LocalizationOption>()
            .OrderBy(option => option.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static LocalizationOption? CreateLocalizationOption(string languageTag)
    {
        try
        {
            var culture = CultureInfo.GetCultureInfo(languageTag);
            return new LocalizationOption(culture.Name, culture.NativeName);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}

public sealed record LocalizationOption(string LanguageTag, string DisplayName)
{
    public override string ToString() => DisplayName;
}