using CommunityToolkit.Mvvm.ComponentModel;
using PCGet.Services;

namespace PCGet.Models;

public partial class PackageInfo : ObservableObject
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string InstalledVersion { get; init; }
    public required string AvailableVersion { get; init; }
    public required string Source { get; init; }

    public bool IsPCGet => PCGetIdentity.IsPCGetPackage(Id);

    [ObservableProperty]
    public partial PackageUpdateState UpdateState { get; set; }

    [ObservableProperty]
    public partial string UpdateStatus { get; set; } = LocalizationService.GetString("Status_Ready");

    [ObservableProperty]
    public partial double UpdateProgress { get; set; }

    [ObservableProperty]
    public partial bool IsUpdateIndeterminate { get; set; }

    [ObservableProperty]
    public partial bool IsUninstalling { get; set; }

    [ObservableProperty]
    public partial string UninstallStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double UninstallProgress { get; set; }

    [ObservableProperty]
    public partial bool IsUninstallIndeterminate { get; set; }

    public bool IsUpdating =>
        UpdateState == PackageUpdateState.Queued ||
        UpdateState == PackageUpdateState.Updating;

    public bool IsUpdateEnabled =>
        UpdateState == PackageUpdateState.Ready ||
        UpdateState == PackageUpdateState.Failed;

    public string UpdateButtonText => UpdateState switch
    {
        PackageUpdateState.Queued => LocalizationService.GetString("Status_Queued"),
        PackageUpdateState.Updating => LocalizationService.GetString("Status_Updating"),
        PackageUpdateState.Failed => LocalizationService.GetString("Common_Retry"),
        _ => LocalizationService.GetString("Common_Update")
    };

    public bool IsUninstallEnabled => !IsUninstalling;
    public string UninstallButtonText => IsUninstalling ? LocalizationService.GetString("Status_Uninstalling") : LocalizationService.GetString("Common_Uninstall");

    partial void OnUpdateStateChanged(PackageUpdateState value)
    {
        OnPropertyChanged(nameof(IsUpdating));
        OnPropertyChanged(nameof(IsUpdateEnabled));
        OnPropertyChanged(nameof(UpdateButtonText));
    }

    partial void OnIsUninstallingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsUninstallEnabled));
        OnPropertyChanged(nameof(UninstallButtonText));
    }
}

public enum PackageUpdateState
{
    Ready,
    Queued,
    Updating,
    Failed
}
