using CommunityToolkit.Mvvm.ComponentModel;

namespace AppDeck.Models;

public partial class PackageInfo : ObservableObject
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string InstalledVersion { get; init; }
    public required string AvailableVersion { get; init; }
    public required string Source { get; init; }

    public bool IsAppDeck => AppDeckIdentity.IsAppDeckPackage(Id);

    [ObservableProperty]
    public partial PackageUpdateState UpdateState { get; set; }

    [ObservableProperty]
    public partial string UpdateStatus { get; set; } = "Ready";

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
        PackageUpdateState.Queued => "Queued",
        PackageUpdateState.Updating => "Updating...",
        PackageUpdateState.Failed => "Retry",
        _ => "Update"
    };

    public bool IsUninstallEnabled => !IsUninstalling;
    public string UninstallButtonText => IsUninstalling ? "Uninstalling..." : "Uninstall";

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