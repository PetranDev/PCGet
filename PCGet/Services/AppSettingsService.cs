using Windows.Storage;

namespace PCGet.Services;

public sealed class AppSettingsService
{
    private const string SilentPackageOperationsKey = "SilentPackageOperations";
    private const string CheckUpdatesOnStartupKey = "CheckUpdatesOnStartup";
    private const string BackgroundUpdateChecksKey = "BackgroundUpdateChecks";
    private const string IncludeUnknownVersionsKey = "IncludeUnknownVersions";
    private const string BackgroundUpdateIntervalMinutesKey = "BackgroundUpdateIntervalMinutes";
    private const string InstallAdditionalArgumentsKey = "InstallAdditionalArguments";
    private const string UpdateAdditionalArgumentsKey = "UpdateAdditionalArguments";
    private const string UninstallAdditionalArgumentsKey = "UninstallAdditionalArguments";
    private const string LanguageKey = "Language";

    private readonly ApplicationDataContainer? _localSettings;

    public AppSettingsService()
    {
        // ApplicationData requires package identity; the Inno Setup build uses a JSON file instead.
        if (AppIdentity.IsPackaged)
            _localSettings = ApplicationData.Current.LocalSettings;
    }

    public bool SilentPackageOperations
    {
        get => GetBoolean(SilentPackageOperationsKey, true);
        set => SetValue(SilentPackageOperationsKey, value);
    }

    public bool CheckUpdatesOnStartup
    {
        get => GetBoolean(CheckUpdatesOnStartupKey, true);
        set => SetValue(CheckUpdatesOnStartupKey, value);
    }

    public bool BackgroundUpdateChecks
    {
        get => GetBoolean(BackgroundUpdateChecksKey, false);
        set => SetValue(BackgroundUpdateChecksKey, value);
    }

    public bool IncludeUnknownVersions
    {
        get => GetBoolean(IncludeUnknownVersionsKey, false);
        set => SetValue(IncludeUnknownVersionsKey, value);
    }

    public int BackgroundUpdateIntervalMinutes
    {
        get => GetInt32(BackgroundUpdateIntervalMinutesKey, 60);
        set => SetValue(BackgroundUpdateIntervalMinutesKey, value);
    }

    public string InstallAdditionalArguments
    {
        get => GetString(InstallAdditionalArgumentsKey);
        set => SetValue(InstallAdditionalArgumentsKey, value);
    }

    public string UpdateAdditionalArguments
    {
        get => GetString(UpdateAdditionalArgumentsKey);
        set => SetValue(UpdateAdditionalArgumentsKey, value);
    }

    public string UninstallAdditionalArguments
    {
        get => GetString(UninstallAdditionalArgumentsKey);
        set => SetValue(UninstallAdditionalArgumentsKey, value);
    }

    public string Language
    {
        get => GetString(LanguageKey);
        set => SetValue(LanguageKey, value);
    }

    private bool GetBoolean(string key, bool defaultValue)
    {
        if (TryGetValue(key, out var value) && value is bool booleanValue)
            return booleanValue;

        return defaultValue;
    }

    private int GetInt32(string key, int defaultValue)
    {
        if (TryGetValue(key, out var value) && value is int intValue)
            return intValue;

        return defaultValue;
    }

    private string GetString(string key)
    {
        if (TryGetValue(key, out var value) && value is string stringValue)
            return stringValue;

        return string.Empty;
    }

    private bool TryGetValue(string key, out object? value)
    {
        if (_localSettings is not null)
            return _localSettings.Values.TryGetValue(key, out value);

        return JsonSettingsStore.Instance.TryGetValue(key, out value);
    }

    private void SetValue(string key, object value)
    {
        if (_localSettings is not null)
            _localSettings.Values[key] = value;
        else
            JsonSettingsStore.Instance.SetValue(key, value);
    }
}
