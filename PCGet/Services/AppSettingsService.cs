using Windows.Storage;

namespace PCGet.Services;

public sealed class AppSettingsService
{
    private const string SilentPackageOperationsKey = "SilentPackageOperations";
    private const string CheckUpdatesOnStartupKey = "CheckUpdatesOnStartup";
    private const string BackgroundUpdateChecksKey = "BackgroundUpdateChecks";
    private const string BackgroundUpdateIntervalMinutesKey = "BackgroundUpdateIntervalMinutes";
    private const string InstallAdditionalArgumentsKey = "InstallAdditionalArguments";
    private const string UpdateAdditionalArgumentsKey = "UpdateAdditionalArguments";
    private const string UninstallAdditionalArgumentsKey = "UninstallAdditionalArguments";

    private readonly ApplicationDataContainer _localSettings;

    public AppSettingsService()
    {
        _localSettings = ApplicationData.Current.LocalSettings;
    }

    public bool SilentPackageOperations
    {
        get => GetBoolean(SilentPackageOperationsKey, true);
        set => _localSettings.Values[SilentPackageOperationsKey] = value;
    }

    public bool CheckUpdatesOnStartup
    {
        get => GetBoolean(CheckUpdatesOnStartupKey, true);
        set => _localSettings.Values[CheckUpdatesOnStartupKey] = value;
    }

    public bool BackgroundUpdateChecks
    {
        get => GetBoolean(BackgroundUpdateChecksKey, false);
        set => _localSettings.Values[BackgroundUpdateChecksKey] = value;
    }

    public int BackgroundUpdateIntervalMinutes
    {
        get => GetInt32(BackgroundUpdateIntervalMinutesKey, 60);
        set => _localSettings.Values[BackgroundUpdateIntervalMinutesKey] = value;
    }

    public string InstallAdditionalArguments
    {
        get => GetString(InstallAdditionalArgumentsKey);
        set => _localSettings.Values[InstallAdditionalArgumentsKey] = value;
    }

    public string UpdateAdditionalArguments
    {
        get => GetString(UpdateAdditionalArgumentsKey);
        set => _localSettings.Values[UpdateAdditionalArgumentsKey] = value;
    }

    public string UninstallAdditionalArguments
    {
        get => GetString(UninstallAdditionalArgumentsKey);
        set => _localSettings.Values[UninstallAdditionalArgumentsKey] = value;
    }

    private bool GetBoolean(string key, bool defaultValue)
    {
        if (_localSettings.Values.TryGetValue(key, out var value) && value is bool booleanValue)
            return booleanValue;

        return defaultValue;
    }

    private int GetInt32(string key, int defaultValue)
    {
        if (_localSettings.Values.TryGetValue(key, out var value) && value is int intValue)
            return intValue;

        return defaultValue;
    }

    private string GetString(string key)
    {
        if (_localSettings.Values.TryGetValue(key, out var value) && value is string stringValue)
            return stringValue;

        return string.Empty;
    }
}