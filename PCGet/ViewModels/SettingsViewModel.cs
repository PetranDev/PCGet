using PCGet.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace PCGet.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettingsService _settingsService;
    private readonly StartupService _startupService;
    private bool _loadingStartupState;

    public UpdateIntervalOption[] BackgroundUpdateIntervals { get; } =
    [
        new(15, "Every 15 minutes"),
        new(30, "Every 30 minutes"),
        new(60, "Every hour"),
        new(120, "Every 2 hours"),
        new(240, "Every 4 hours"),
        new(480, "Every 8 hours"),
        new(720, "Every 12 hours"),
        new(1440, "Every day")
    ];

    [ObservableProperty]
    public partial bool SilentPackageOperations { get; set; }

    [ObservableProperty]
    public partial bool CheckUpdatesOnStartup { get; set; }

    [ObservableProperty]
    public partial bool BackgroundUpdateChecks { get; set; }

    [ObservableProperty]
    public partial UpdateIntervalOption SelectedBackgroundUpdateInterval { get; set; } = null!;

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
        BackgroundUpdateChecks = _settingsService.BackgroundUpdateChecks;

        SelectedBackgroundUpdateInterval =
            BackgroundUpdateIntervals.FirstOrDefault(option => option.Minutes == _settingsService.BackgroundUpdateIntervalMinutes) ??
            BackgroundUpdateIntervals.First(option => option.Minutes == 60);

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

    partial void OnBackgroundUpdateChecksChanged(bool value)
    {
        _settingsService.BackgroundUpdateChecks = value;
        App.CurrentApp?.ApplyBackgroundUpdateSettings();
    }

    partial void OnSelectedBackgroundUpdateIntervalChanged(UpdateIntervalOption value)
    {
        if (value is null)
            return;

        _settingsService.BackgroundUpdateIntervalMinutes = value.Minutes;
        App.CurrentApp?.ApplyBackgroundUpdateSettings();
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
            StartupTaskState.Enabled => "PCGet will start automatically when you sign in to Windows.",
            StartupTaskState.Disabled => "PCGet will not start automatically with Windows.",
            StartupTaskState.DisabledByUser => "Startup has been disabled in Windows Settings. Enable PCGet there to allow this option.",
            StartupTaskState.DisabledByPolicy => "Startup is disabled by a Windows policy.",
            _ => "Windows startup status is unavailable."
        };
    }
}

public sealed record UpdateIntervalOption(int Minutes, string DisplayName)
{
    public override string ToString() => DisplayName;
}