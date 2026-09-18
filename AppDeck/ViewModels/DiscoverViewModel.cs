using AppDeck.Models;
using AppDeck.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace AppDeck.ViewModels;

public partial class DiscoverViewModel : ObservableObject
{
    private const string AllSources = "All sources";

    private readonly IWinGetService _winGetService;
    private readonly ElevatedOperationService _elevatedOperationService;
    private readonly List<DiscoverPackageInfo> _searchResults = [];

    public ObservableCollection<DiscoverPackageInfo> Packages { get; } = [];
    public ObservableCollection<string> Sources { get; } = [AllSources];
    public string[] SortOptions { get; } = ["Relevance", "Name A–Z", "Name Z–A", "Publisher A–Z"];

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
    public partial string SelectedSort { get; set; } = "Relevance";

    public bool HasSelectedPackage => SelectedPackage is not null;
    public bool HasSearchResults => _searchResults.Count > 0;

    public string StatusText
    {
        get
        {
            if (IsSearching)
                return "Searching...";

            if (string.IsNullOrWhiteSpace(SearchText))
                return "Search WinGet for applications.";

            if (_searchResults.Count == 0)
                return "No applications found.";

            if (Packages.Count != _searchResults.Count)
                return Packages.Count == 1
                    ? $"1 of {_searchResults.Count} applications shown"
                    : $"{Packages.Count} of {_searchResults.Count} applications shown";

            return Packages.Count switch
            {
                0 => "No applications found.",
                1 => "1 application found",
                _ => $"{Packages.Count} applications found"
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
            OnPropertyChanged(nameof(StatusText));
            return;
        }

        try
        {
            IsSearching = true;
            ErrorMessage = null;
            SelectedPackage = null;

            OnPropertyChanged(nameof(StatusText));

            var query = SearchText.Trim();
            var packages = await Task.Run(async () => await _winGetService.SearchPackagesAsync(query));

            _searchResults.Clear();
            _searchResults.AddRange(packages);

            RebuildSources();
            ApplyView();

            if (Packages.Count > 0)
                SelectedPackage = Packages[0];

            OnPropertyChanged(nameof(HasSearchResults));
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsSearching = false;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public async Task<bool> InstallAsync(DiscoverPackageInfo package, bool showError = true)
    {
        if (package.IsInstalled || package.IsInstalling)
            return false;

        var succeeded = false;

        try
        {
            ErrorMessage = null;
            LastInstallError = null;
            LastInstallRequiresElevation = false;

            package.IsInstalling = true;
            package.InstallStatus = "Preparing...";
            package.InstallProgress = 0;
            package.IsInstallIndeterminate = false;

            var progress = new Progress<PackageInstallProgress>(value =>
            {
                package.InstallStatus = value.Status;
                package.InstallProgress = value.Percent;
            });

            await Task.Run(async () => await _winGetService.InstallPackageAsync(package.Id, progress));

            package.InstallStatus = "Installed";
            package.InstallProgress = 100;
            package.IsInstalled = true;

            succeeded = true;
        }
        catch (Exception ex)
        {
            package.InstallStatus = "Failed";
            package.InstallProgress = 0;

            LastInstallError = $"{package.Name}: {ex.Message}";
            LastInstallRequiresElevation = PackageOperationErrorClassifier.RequiresElevation(ex.Message);

            if (showError)
                ErrorMessage = LastInstallError;
        }
        finally
        {
            package.IsInstallIndeterminate = false;
            package.IsInstalling = false;
        }

        return succeeded;
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
            package.InstallStatus = "Installing as administrator...";
            package.InstallProgress = 0;
            package.IsInstallIndeterminate = true;

            await _elevatedOperationService.InstallPackageAsync(package.Id);

            package.IsInstallIndeterminate = false;
            package.InstallStatus = "Installed";
            package.InstallProgress = 100;
            package.IsInstalled = true;

            return true;
        }
        catch (Exception ex)
        {
            package.IsInstallIndeterminate = false;
            package.InstallStatus = "Failed";
            package.InstallProgress = 0;

            LastInstallError = $"{package.Name}: {ex.Message}";
            ErrorMessage = LastInstallError;

            return false;
        }
        finally
        {
            package.IsInstallIndeterminate = false;
            package.IsInstalling = false;
        }
    }

    private void RebuildSources()
    {
        var previousSource = SelectedSource;

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

    private void ApplyView()
    {
        var selectedPackageId = SelectedPackage?.Id;

        IEnumerable<DiscoverPackageInfo> packages = _searchResults;

        if (!string.Equals(SelectedSource, AllSources, StringComparison.OrdinalIgnoreCase))
        {
            packages = packages.Where(package =>
                string.Equals(package.Source, SelectedSource, StringComparison.OrdinalIgnoreCase));
        }

        packages = SelectedSort switch
        {
            "Name A–Z" => packages.OrderBy(package => package.Name, StringComparer.OrdinalIgnoreCase),
            "Name Z–A" => packages.OrderByDescending(package => package.Name, StringComparer.OrdinalIgnoreCase),
            "Publisher A–Z" => packages
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
}