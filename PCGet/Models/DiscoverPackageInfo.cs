using CommunityToolkit.Mvvm.ComponentModel;
using PCGet.Services;

namespace PCGet.Models;

public partial class DiscoverPackageInfo : ObservableObject
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string Source { get; init; }

    public string Publisher { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ShortDescription { get; init; } = string.Empty;
    public string License { get; init; } = string.Empty;
    public string Locale { get; init; } = string.Empty;
    public string Copyright { get; init; } = string.Empty;
    public string ReleaseNotes { get; init; } = string.Empty;
    public string InstallationNotes { get; init; } = string.Empty;
    public string PackageUrl { get; init; } = string.Empty;
    public string PublisherUrl { get; init; } = string.Empty;
    public string PublisherSupportUrl { get; init; } = string.Empty;
    public string LicenseUrl { get; init; } = string.Empty;
    public string PrivacyUrl { get; init; } = string.Empty;
    public string CopyrightUrl { get; init; } = string.Empty;
    public string ReleaseNotesUrl { get; init; } = string.Empty;
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

    [ObservableProperty]
    public partial bool IsInstallIndeterminate { get; set; }

    public bool IsInstallEnabled => !IsInstalled && !IsInstalling;

    public string InstallButtonText
    {
        get
        {
            if (IsInstalled)
                return LocalizationService.GetString("Status_Installed");

            if (IsInstalling)
                return LocalizationService.GetString("Status_Installing");

            return LocalizationService.GetString("Common_Install");
        }
    }

    public string DisplayDescription => !string.IsNullOrWhiteSpace(Description) ? Description : ShortDescription;

    public bool HasPublisher => !string.IsNullOrWhiteSpace(Publisher);
    public bool HasAuthor => !string.IsNullOrWhiteSpace(Author);
    public bool HasDescription => !string.IsNullOrWhiteSpace(DisplayDescription);
    public bool HasLicense => !string.IsNullOrWhiteSpace(License);
    public bool HasLocale => !string.IsNullOrWhiteSpace(Locale);
    public bool HasCopyright => !string.IsNullOrWhiteSpace(Copyright);
    public bool HasReleaseNotes => !string.IsNullOrWhiteSpace(ReleaseNotes);
    public bool HasInstallationNotes => !string.IsNullOrWhiteSpace(InstallationNotes);
    public bool HasTags => !string.IsNullOrWhiteSpace(Tags);
    public bool HasIcon => !string.IsNullOrWhiteSpace(IconUrl);
    public bool HasNoIcon => !HasIcon;
    public bool HasPackageUrl => !string.IsNullOrWhiteSpace(PackageUrl);
    public bool HasPublisherUrl => !string.IsNullOrWhiteSpace(PublisherUrl);
    public bool HasPublisherSupportUrl => !string.IsNullOrWhiteSpace(PublisherSupportUrl);
    public bool HasLicenseUrl => !string.IsNullOrWhiteSpace(LicenseUrl);
    public bool HasPrivacyUrl => !string.IsNullOrWhiteSpace(PrivacyUrl);
    public bool HasCopyrightUrl => !string.IsNullOrWhiteSpace(CopyrightUrl);
    public bool HasReleaseNotesUrl => !string.IsNullOrWhiteSpace(ReleaseNotesUrl);
    public bool HasLinks => HasPackageUrl || HasPublisherUrl || HasPublisherSupportUrl || HasLicenseUrl || HasPrivacyUrl || HasCopyrightUrl || HasReleaseNotesUrl;

    partial void OnIsInstalledChanged(bool value)
    {
        OnPropertyChanged(nameof(IsInstallEnabled));
        OnPropertyChanged(nameof(InstallButtonText));
    }

    partial void OnIsInstallingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsInstallEnabled));
        OnPropertyChanged(nameof(InstallButtonText));
    }
}
