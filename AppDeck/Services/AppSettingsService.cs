using Windows.Storage;

namespace AppDeck.Services;

public sealed class AppSettingsService
{
    private const string SilentPackageOperationsKey = "SilentPackageOperations";
    private const string CheckUpdatesOnStartupKey = "CheckUpdatesOnStartup";

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

    private bool GetBoolean(string key, bool defaultValue)
    {
        if (_localSettings.Values.TryGetValue(key, out var value) && value is bool booleanValue)
            return booleanValue;

        return defaultValue;
    }
}