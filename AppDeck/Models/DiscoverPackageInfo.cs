using CommunityToolkit.Mvvm.ComponentModel;

namespace AppDeck.Models;

public partial class DiscoverPackageInfo : ObservableObject
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string Source { get; init; }

    [ObservableProperty]
    public partial bool IsInstalled { get; set; }

    [ObservableProperty]
    public partial bool IsInstalling { get; set; }

    [ObservableProperty]
    public partial string InstallStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double InstallProgress { get; set; }

    public bool IsInstallEnabled =>
        !IsInstalled &&
        !IsInstalling;

    public string InstallButtonText
    {
        get
        {
            if (IsInstalled)
                return "Installed";

            if (IsInstalling)
                return "Installing...";

            return "Install";
        }
    }

    partial void OnIsInstalledChanged(
        bool value)
    {
        OnPropertyChanged(nameof(IsInstallEnabled));
        OnPropertyChanged(nameof(InstallButtonText));
    }

    partial void OnIsInstallingChanged(
        bool value)
    {
        OnPropertyChanged(nameof(IsInstallEnabled));
        OnPropertyChanged(nameof(InstallButtonText));
    }
}