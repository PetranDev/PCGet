using PCGet.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Microsoft.Windows.Globalization;

namespace PCGet.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettingsService _settingsService;
    private readonly StartupService _startupService;
    private bool _loadingStartupState;
    private bool _loadingLanguage;

    public UpdateIntervalOption[] BackgroundUpdateIntervals { get; } =
    [
        new(15, LocalizationService.GetString("Interval_15Minutes")),
        new(30, LocalizationService.GetString("Interval_30Minutes")),
        new(60, LocalizationService.GetString("Interval_1Hour")),
        new(120, LocalizationService.GetString("Interval_2Hours")),
        new(240, LocalizationService.GetString("Interval_4Hours")),
        new(480, LocalizationService.GetString("Interval_8Hours")),
        new(720, LocalizationService.GetString("Interval_12Hours")),
        new(1440, LocalizationService.GetString("Interval_1Day"))
    ];

    public LocalizationOption[] Languages { get; }

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

    [ObservableProperty]
    public partial string InstallAdditionalArguments { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string UpdateAdditionalArguments { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string UninstallAdditionalArguments { get; set; } = string.Empty;

    [ObservableProperty]
    public partial LocalizationOption SelectedLanguage { get; set; } = null!;

    [ObservableProperty]
    public partial string LanguageRestartStatus { get; set; } = string.Empty;

    public string VersionText
    {
        get
        {
            var version = Package.Current.Id.Version;
            return LocalizationService.Format("Settings_Version", version.Major, version.Minor, version.Build, version.Revision);
        }
    }

    public SettingsViewModel(AppSettingsService settingsService, StartupService startupService)
    {
        _settingsService = settingsService;
        _startupService = startupService;

        SilentPackageOperations = _settingsService.SilentPackageOperations;
        CheckUpdatesOnStartup = _settingsService.CheckUpdatesOnStartup;
        BackgroundUpdateChecks = _settingsService.BackgroundUpdateChecks;
        InstallAdditionalArguments = _settingsService.InstallAdditionalArguments;
        UpdateAdditionalArguments = _settingsService.UpdateAdditionalArguments;
        UninstallAdditionalArguments = _settingsService.UninstallAdditionalArguments;

        SelectedBackgroundUpdateInterval = BackgroundUpdateIntervals.FirstOrDefault(option => option.Minutes == _settingsService.BackgroundUpdateIntervalMinutes) ?? BackgroundUpdateIntervals.First(option => option.Minutes == 60);

        Languages = LocalizationService.AvailableLanguages.ToArray();
        _loadingLanguage = true;
        var currentLanguage = string.IsNullOrWhiteSpace(_settingsService.Language) ? ApplicationLanguages.Languages.FirstOrDefault() : _settingsService.Language;
        SelectedLanguage = Languages.FirstOrDefault(language => string.Equals(language.LanguageTag, currentLanguage, StringComparison.OrdinalIgnoreCase)) ?? Languages.First();
        _loadingLanguage = false;

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

    partial void OnSilentPackageOperationsChanged(bool value) => _settingsService.SilentPackageOperations = value;
    partial void OnCheckUpdatesOnStartupChanged(bool value) => _settingsService.CheckUpdatesOnStartup = value;

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

    partial void OnInstallAdditionalArgumentsChanged(string value) => _settingsService.InstallAdditionalArguments = value ?? string.Empty;
    partial void OnUpdateAdditionalArgumentsChanged(string value) => _settingsService.UpdateAdditionalArguments = value ?? string.Empty;
    partial void OnUninstallAdditionalArgumentsChanged(string value) => _settingsService.UninstallAdditionalArguments = value ?? string.Empty;

    partial void OnSelectedLanguageChanged(LocalizationOption value)
    {
        if (_loadingLanguage || value is null)
            return;
        _settingsService.Language = value.LanguageTag;
        LocalizationService.ApplyLanguage(value.LanguageTag);
        LanguageRestartStatus = LocalizationService.GetString("Settings_LanguageRestartRequired");
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
            StartupTaskState.Enabled => LocalizationService.GetString("Startup_Enabled"),
            StartupTaskState.Disabled => LocalizationService.GetString("Startup_Disabled"),
            StartupTaskState.DisabledByUser => LocalizationService.GetString("Startup_DisabledByUser"),
            StartupTaskState.DisabledByPolicy => LocalizationService.GetString("Startup_DisabledByPolicy"),
            _ => LocalizationService.GetString("Startup_Unavailable")
        };
    }
}

public sealed record UpdateIntervalOption(int Minutes, string DisplayName)
{
    public override string ToString() => DisplayName;
}
