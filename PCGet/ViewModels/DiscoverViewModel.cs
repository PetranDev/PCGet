using PCGet.Models;
using PCGet.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace PCGet.ViewModels;

public partial class DiscoverViewModel : ObservableObject
{
    private static string AllSources => LocalizationService.GetString("Filter_AllSources");

    private readonly IWinGetService _winGetService;
    private readonly ElevatedOperationService _elevatedOperationService;
    private readonly List<DiscoverPackageInfo> _searchResults = [];

    public ObservableCollection<DiscoverPackageInfo> Packages { get; } = [];
    public ObservableCollection<string> Sources { get; } = [AllSources];
    public string[] SortOptions { get; } = [LocalizationService.GetString("Sort_Relevance"), LocalizationService.GetString("Sort_NameAZ"), LocalizationService.GetString("Sort_NameZA"), LocalizationService.GetString("Sort_PublisherAZ")];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsSearching { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string? LastInstallError { get; set; }

    [ObservableProperty]
    public partial bool LastInstallRequiresElevation { get; set; }

    [ObservableProperty]
    public partial DiscoverPackageInfo? SelectedPackage { get; set; }

    [ObservableProperty]
    public partial string SelectedSource { get; set; } = AllSources;

    [ObservableProperty]
    public partial string SelectedSort { get; set; } = LocalizationService.GetString("Sort_Relevance");

    public bool HasSelectedPackage => SelectedPackage is not null;
    public bool HasSearchResults => _searchResults.Count > 0;
    public bool CanRefresh => !IsSearching && !string.IsNullOrWhiteSpace(SearchText) && _searchResults.Count > 0;

    public string StatusText
    {
        get
        {
            if (IsSearching)
                return LocalizationService.GetString("Status_Searching");

            if (string.IsNullOrWhiteSpace(SearchText))
                return LocalizationService.GetString("Discover_SearchPrompt");

            if (_searchResults.Count == 0)
                return LocalizationService.GetString("Discover_NoApplicationsFound");

            if (Packages.Count != _searchResults.Count)
                return Packages.Count == 1
                    ? LocalizationService.Format("Discover_OneOfApplicationsShown", _searchResults.Count)
                    : LocalizationService.Format("Discover_ApplicationsShown", Packages.Count, _searchResults.Count);

            return Packages.Count switch
            {
                0 => LocalizationService.GetString("Discover_NoApplicationsFound"),
                1 => LocalizationService.GetString("Discover_OneApplicationFound"),
                _ => LocalizationService.Format("Discover_ApplicationsFound", Packages.Count)
            };
        }
    }

    public DiscoverViewModel(IWinGetService winGetService, ElevatedOperationService elevatedOperationService)
    {
        _winGetService = winGetService;
        _elevatedOperationService = elevatedOperationService;
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        await ExecuteSearchAsync(false);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (!CanRefresh)
            return;

        await ExecuteSearchAsync(true);
    }

    private async Task ExecuteSearchAsync(bool preserveSelection)
    {
        if (IsSearching)
            return;

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            _searchResults.Clear();
            Packages.Clear();
            Sources.Clear();
            Sources.Add(AllSources);
            SelectedSource = AllSources;
            SelectedPackage = null;

            OnPropertyChanged(nameof(HasSearchResults));
            OnPropertyChanged(nameof(CanRefresh));
            OnPropertyChanged(nameof(StatusText));
            return;
        }

        var selectedPackageId = preserveSelection ? SelectedPackage?.Id : null;
        var selectedSource = preserveSelection ? SelectedSource : AllSources;

        try
        {
            IsSearching = true;
            ErrorMessage = null;

            if (!preserveSelection)
                SelectedPackage = null;

            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(CanRefresh));

            var query = SearchText.Trim();
            var packages = await Task.Run(async () => await _winGetService.SearchPackagesAsync(query));

            _searchResults.Clear();
            _searchResults.AddRange(packages);

            RebuildSources(selectedSource);
            ApplyView(selectedPackageId);

            OnPropertyChanged(nameof(HasSearchResults));
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsSearching = false;
            OnPropertyChanged(nameof(CanRefresh));
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public async Task<bool> InstallAsync(DiscoverPackageInfo package, bool showError = true)
    {
        if (package.IsInstalled || package.IsInstalling)
            return false;

        try
        {
            ErrorMessage = null;
            LastInstallError = null;
            LastInstallRequiresElevation = false;

            package.IsInstalling = true;
            package.InstallStatus = LocalizationService.GetString("Status_Preparing");
            package.InstallProgress = 0;
            package.IsInstallIndeterminate = false;

            var progress = new Progress<PackageInstallProgress>(value =>
            {
                package.InstallStatus = value.Status;
                package.InstallProgress = value.Percent;
            });

            await Task.Run(async () => await _winGetService.InstallPackageAsync(package.Id, progress));

            if (!await VerifyInstallAsync(package, showError))
                return false;

            CompleteInstall(package);
            return true;
        }
        catch (Exception ex)
        {
            FailInstall(package, $"{package.Name}: {ex.Message}", showError, true);
            return false;
        }
        finally
        {
            package.IsInstallIndeterminate = false;
            package.IsInstalling = false;
        }
    }

    public async Task<bool> InstallAsAdministratorAsync(DiscoverPackageInfo package)
    {
        if (package.IsInstalled || package.IsInstalling)
            return false;

        try
        {
            ErrorMessage = null;
            LastInstallError = null;
            LastInstallRequiresElevation = false;

            package.IsInstalling = true;
            package.InstallStatus = LocalizationService.GetString("Status_InstallingAsAdministrator");
            package.InstallProgress = 0;
            package.IsInstallIndeterminate = true;

            await _elevatedOperationService.InstallPackageAsync(package.Id);

            if (!await VerifyInstallAsync(package, true))
                return false;

            CompleteInstall(package);
            return true;
        }
        catch (Exception ex)
        {
            FailInstall(package, $"{package.Name}: {ex.Message}", true, false);
            return false;
        }
        finally
        {
            package.IsInstallIndeterminate = false;
            package.IsInstalling = false;
        }
    }

    private async Task<bool> VerifyInstallAsync(DiscoverPackageInfo package, bool showError)
    {
        package.InstallStatus = LocalizationService.GetString("Status_VerifyingInstallation");
        package.InstallProgress = 0;
        package.IsInstallIndeterminate = true;

        var installedPackages = await Task.Run(async () => await _winGetService.GetInstalledPackagesAsync());
        var isInstalled = installedPackages.Any(candidate =>
            string.Equals(candidate.Id, package.Id, StringComparison.OrdinalIgnoreCase));

        if (isInstalled)
            return true;

        var message = LocalizationService.Format("Error_InstallVerification", package.Name);

        package.IsInstallIndeterminate = false;
        package.InstallStatus = LocalizationService.GetString("Status_NotInstalled");
        package.InstallProgress = 0;

        LastInstallError = message;
        LastInstallRequiresElevation = false;

        if (showError)
            ErrorMessage = message;

        return false;
    }

    private void CompleteInstall(DiscoverPackageInfo package)
    {
        package.IsInstallIndeterminate = false;
        package.InstallStatus = LocalizationService.GetString("Status_Installed");
        package.InstallProgress = 100;
        package.IsInstalled = true;
    }

    private void FailInstall(DiscoverPackageInfo package, string errorMessage, bool showError, bool classifyElevation)
    {
        package.IsInstallIndeterminate = false;
        package.InstallStatus = LocalizationService.GetString("Status_Failed");
        package.InstallProgress = 0;

        LastInstallError = errorMessage;
        LastInstallRequiresElevation = classifyElevation && PackageOperationErrorClassifier.RequiresElevation(errorMessage);

        if (showError)
            ErrorMessage = errorMessage;
    }

    private void RebuildSources(string? preferredSource = null)
    {
        var previousSource = preferredSource ?? SelectedSource;

        Sources.Clear();
        Sources.Add(AllSources);

        foreach (var source in _searchResults
            .Select(package => package.Source)
            .Where(source => !string.IsNullOrWhiteSpace(source))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(source => source, StringComparer.OrdinalIgnoreCase))
        {
            Sources.Add(source);
        }

        SelectedSource = Sources.Any(source => string.Equals(source, previousSource, StringComparison.OrdinalIgnoreCase))
            ? previousSource
            : AllSources;
    }

    private void ApplyView(string? preferredPackageId = null)
    {
        var selectedPackageId = preferredPackageId ?? SelectedPackage?.Id;

        IEnumerable<DiscoverPackageInfo> packages = _searchResults;

        if (!string.Equals(SelectedSource, AllSources, StringComparison.OrdinalIgnoreCase))
        {
            packages = packages.Where(package =>
                string.Equals(package.Source, SelectedSource, StringComparison.OrdinalIgnoreCase));
        }

        packages = SelectedSort switch
        {
            var sort when sort == LocalizationService.GetString("Sort_NameAZ") => packages.OrderBy(package => package.Name, StringComparer.OrdinalIgnoreCase),
            var sort when sort == LocalizationService.GetString("Sort_NameZA") => packages.OrderByDescending(package => package.Name, StringComparer.OrdinalIgnoreCase),
            var sort when sort == LocalizationService.GetString("Sort_PublisherAZ") => packages
                .OrderBy(package => package.Publisher, StringComparer.OrdinalIgnoreCase)
                .ThenBy(package => package.Name, StringComparer.OrdinalIgnoreCase),
            _ => packages
        };

        Packages.Clear();

        foreach (var package in packages)
            Packages.Add(package);

        SelectedPackage = Packages.FirstOrDefault(package =>
            string.Equals(package.Id, selectedPackageId, StringComparison.OrdinalIgnoreCase));

        if (SelectedPackage is null && Packages.Count > 0)
            SelectedPackage = Packages[0];

        OnPropertyChanged(nameof(StatusText));
    }

    partial void OnSelectedSourceChanged(string value)
    {
        ApplyView();
    }

    partial void OnSelectedSortChanged(string value)
    {
        ApplyView();
    }

    partial void OnSelectedPackageChanged(DiscoverPackageInfo? value)
    {
        OnPropertyChanged(nameof(HasSelectedPackage));
    }

    partial void OnIsSearchingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRefresh));
    }

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(CanRefresh));
    }
}
