using CommunityToolkit.Mvvm.ComponentModel;

namespace AppDeck.Models;

public partial class DiscoverPackageInfo : ObservableObject
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string Source { get; init; }

    public string Publisher { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ShortDescription { get; init; } = string.Empty;
    public string License { get; init; } = string.Empty;
    public string PackageUrl { get; init; } = string.Empty;
    public string PublisherUrl { get; init; } = string.Empty;
    public string LicenseUrl { get; init; } = string.Empty;
    public string Tags { get; init; } = string.Empty;
    public string IconUrl { get; init; } = string.Empty;

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

    public string DisplayDescription =>
        !string.IsNullOrWhiteSpace(Description)
            ? Description
            : ShortDescription;

    public bool HasPublisher =>
        !string.IsNullOrWhiteSpace(Publisher);

    public bool HasDescription =>
        !string.IsNullOrWhiteSpace(DisplayDescription);

    public bool HasLicense =>
        !string.IsNullOrWhiteSpace(License);

    public bool HasTags =>
        !string.IsNullOrWhiteSpace(Tags);

    public bool HasIcon =>
        !string.IsNullOrWhiteSpace(IconUrl);

    public bool HasNoIcon =>
        !HasIcon;

    public bool HasPackageUrl =>
        !string.IsNullOrWhiteSpace(PackageUrl);

    public bool HasPublisherUrl =>
        !string.IsNullOrWhiteSpace(PublisherUrl);

    public bool HasLicenseUrl =>
        !string.IsNullOrWhiteSpace(LicenseUrl);

    public bool HasLinks =>
        HasPackageUrl ||
        HasPublisherUrl ||
        HasLicenseUrl;

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