using AppDeck.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace AppDeck.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettingsService _settingsService;
    private readonly StartupService _startupService;
    private bool _loadingStartupState;

    [ObservableProperty]
    public partial bool SilentPackageOperations { get; set; }

    [ObservableProperty]
    public partial bool CheckUpdatesOnStartup { get; set; }

    [ObservableProperty]
    public partial bool StartWithWindows { get; set; }

    [ObservableProperty]
    public partial bool CanChangeStartWithWindows { get; set; }

    [ObservableProperty]
    public partial string StartupStatus { get; set; } = string.Empty;

    public string VersionText
    {
        get
        {
            var version = Package.Current.Id.Version;
            return $"Version {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
    }

    public SettingsViewModel(AppSettingsService settingsService, StartupService startupService)
    {
        _settingsService = settingsService;
        _startupService = startupService;

        SilentPackageOperations = _settingsService.SilentPackageOperations;
        CheckUpdatesOnStartup = _settingsService.CheckUpdatesOnStartup;

        _ = LoadStartupStateAsync();
    }

    private async Task LoadStartupStateAsync()
    {
        _loadingStartupState = true;

        var state = await _startupService.GetStateAsync();

        StartWithWindows = state == StartupTaskState.Enabled;
        CanChangeStartWithWindows = state is StartupTaskState.Enabled or StartupTaskState.Disabled;
        StartupStatus = GetStartupStatus(state);

        _loadingStartupState = false;
    }

    partial void OnSilentPackageOperationsChanged(bool value)
    {
        _settingsService.SilentPackageOperations = value;
    }

    partial void OnCheckUpdatesOnStartupChanged(bool value)
    {
        _settingsService.CheckUpdatesOnStartup = value;
    }

    async partial void OnStartWithWindowsChanged(bool value)
    {
        if (_loadingStartupState)
            return;

        _loadingStartupState = true;

        var state = await _startupService.SetEnabledAsync(value);

        StartWithWindows = state == StartupTaskState.Enabled;
        CanChangeStartWithWindows = state is StartupTaskState.Enabled or StartupTaskState.Disabled;
        StartupStatus = GetStartupStatus(state);

        _loadingStartupState = false;
    }

    private static string GetStartupStatus(StartupTaskState state)
    {
        return state switch
        {
            StartupTaskState.Enabled => "AppDeck will start automatically when you sign in to Windows.",
            StartupTaskState.Disabled => "AppDeck will not start automatically with Windows.",
            StartupTaskState.DisabledByUser => "Startup has been disabled in Windows Settings. Enable AppDeck there to allow this option.",
            StartupTaskState.DisabledByPolicy => "Startup is disabled by a Windows policy.",
            _ => "Windows startup status is unavailable."
        };
    }
}