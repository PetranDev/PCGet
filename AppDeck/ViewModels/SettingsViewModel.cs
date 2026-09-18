using AppDeck.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Windows.ApplicationModel;

namespace AppDeck.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettingsService _settingsService;

    [ObservableProperty]
    public partial bool SilentPackageOperations { get; set; }

    [ObservableProperty]
    public partial bool CheckUpdatesOnStartup { get; set; }

    public string VersionText
    {
        get
        {
            var version = Package.Current.Id.Version;
            return $"Version {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
    }

    public SettingsViewModel(AppSettingsService settingsService)
    {
        _settingsService = settingsService;

        SilentPackageOperations = _settingsService.SilentPackageOperations;
        CheckUpdatesOnStartup = _settingsService.CheckUpdatesOnStartup;
    }

    partial void OnSilentPackageOperationsChanged(bool value)
    {
        _settingsService.SilentPackageOperations = value;
    }

    partial void OnCheckUpdatesOnStartupChanged(bool value)
    {
        _settingsService.CheckUpdatesOnStartup = value;
    }
}