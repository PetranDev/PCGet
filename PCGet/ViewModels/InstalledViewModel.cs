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

public partial class InstalledViewModel : ObservableObject
{
    private const string AllSources = "All sources";

    private readonly IWinGetService _winGetService;
    private readonly ElevatedOperationService _elevatedOperationService;
    private readonly List<PackageInfo> _allPackages = [];

    public ObservableCollection<PackageInfo> Packages { get; } = [];
    public ObservableCollection<string> Sources { get; } = [AllSources];
    public string[] SortOptions { get; } = ["Name A–Z", "Name Z–A", "Version A–Z", "Version Z–A", "Source A–Z"];

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsUninstalling { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string? LastUninstallError { get; set; }

    [ObservableProperty]
    public partial bool LastUninstallRequiresElevation { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedSource { get; set; } = AllSources;

    [ObservableProperty]
    public partial string SelectedSort { get; set; } = "Name A–Z";

    public string StatusText
    {
        get
        {
            if (IsLoading)
                return "Loading installed applications...";

            if (IsUninstalling)
            {
                var package = _allPackages.FirstOrDefault(item => item.IsUninstalling);
                return package?.UninstallStatus ?? "Uninstalling application...";
            }

            var isFiltered = !string.IsNullOrWhiteSpace(SearchText) ||
                             !string.Equals(SelectedSource, AllSources, StringComparison.OrdinalIgnoreCase);

            if (isFiltered)
            {
                return Packages.Count switch
                {
                    0 => "No matching applications.",
                    1 => $"1 of {_allPackages.Count} applications shown",
                    _ => $"{Packages.Count} of {_allPackages.Count} applications shown"
                };
            }

            return Packages.Count switch
            {
                0 => "No installed applications found.",
                1 => "1 installed application",
                _ => $"{Packages.Count} installed applications"
            };
        }
    }

    public InstalledViewModel(IWinGetService winGetService, ElevatedOperationService elevatedOperationService)
    {
        _winGetService = winGetService;
        _elevatedOperationService = elevatedOperationService;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsLoading || IsUninstalling)
            return;

        try
        {
            IsLoading = true;
            ErrorMessage = null;
            OnPropertyChanged(nameof(StatusText));

            var packages = await Task.Run(async () => await _winGetService.GetInstalledPackagesAsync());

            _allPackages.Clear();
            _allPackages.AddRange(packages);

            RebuildSources();
            ApplyView();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public async Task<bool> UninstallAsync(PackageInfo package, bool interactive = false, bool showError = true)
    {
        if (IsUninstalling)
            return false;

        var succeeded = false;

        try
        {
            IsUninstalling = true;
            ErrorMessage = null;
            LastUninstallError = null;
            LastUninstallRequiresElevation = false;

            package.IsUninstalling = true;
            package.UninstallStatus = $"Preparing to uninstall {package.Name}...";
            package.UninstallProgress = 0;
            package.IsUninstallIndeterminate = false;

            OnPropertyChanged(nameof(StatusText));

            var progress = new Progress<PackageUninstallProgress>(value =>
            {
                package.UninstallStatus = $"{package.Name}: {value.Status}";
                package.UninstallProgress = value.Percent;
                OnPropertyChanged(nameof(StatusText));
            });

            await Task.Run(async () => await _winGetService.UninstallPackageAsync(package.Id, progress, interactive));

            package.UninstallStatus = "Uninstalled";
            package.UninstallProgress = 100;

            RemovePackage(package);
            succeeded = true;
        }
        catch (Exception ex)
        {
            package.UninstallStatus = "Failed";
            package.UninstallProgress = 0;

            LastUninstallError = $"{package.Name}: {ex.Message}";
            LastUninstallRequiresElevation = PackageOperationErrorClassifier.RequiresElevation(ex.Message);

            if (showError)
                ErrorMessage = LastUninstallError;
        }
        finally
        {
            package.IsUninstallIndeterminate = false;
            package.IsUninstalling = false;

            IsUninstalling = false;
            OnPropertyChanged(nameof(StatusText));
        }

        return succeeded;
    }

    public async Task<bool> UninstallAsAdministratorAsync(PackageInfo package, bool interactive)
    {
        if (IsUninstalling)
            return false;

        try
        {
            IsUninstalling = true;
            ErrorMessage = null;
            LastUninstallError = null;
            LastUninstallRequiresElevation = false;

            package.IsUninstalling = true;
            package.UninstallStatus = $"Uninstalling {package.Name} as administrator...";
            package.UninstallProgress = 0;
            package.IsUninstallIndeterminate = true;

            OnPropertyChanged(nameof(StatusText));

            await _elevatedOperationService.UninstallPackageAsync(package.Id, interactive);

            package.IsUninstallIndeterminate = false;
            package.UninstallStatus = "Uninstalled";
            package.UninstallProgress = 100;

            RemovePackage(package);

            return true;
        }
        catch (Exception ex)
        {
            package.IsUninstallIndeterminate = false;
            package.UninstallStatus = "Failed";
            package.UninstallProgress = 0;

            LastUninstallError = $"{package.Name}: {ex.Message}";
            ErrorMessage = LastUninstallError;

            return false;
        }
        finally
        {
            package.IsUninstallIndeterminate = false;
            package.IsUninstalling = false;

            IsUninstalling = false;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public bool IsPackageUninstalling(PackageInfo package)
    {
        return package.IsUninstalling;
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyView();
    }

    partial void OnSelectedSourceChanged(string value)
    {
        ApplyView();
    }

    partial void OnSelectedSortChanged(string value)
    {
        ApplyView();
    }

    private void RemovePackage(PackageInfo package)
    {
        _allPackages.RemoveAll(item => string.Equals(item.Id, package.Id, StringComparison.OrdinalIgnoreCase));

        RebuildSources();
        ApplyView();
    }

    private void RebuildSources()
    {
        var previousSource = SelectedSource;

        Sources.Clear();
        Sources.Add(AllSources);

        foreach (var source in _allPackages
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
        Packages.Clear();

        IEnumerable<PackageInfo> packages = _allPackages;
        var searchText = SearchText.Trim();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            packages = packages.Where(package =>
                Contains(package.Name, searchText) ||
                Contains(package.Id, searchText) ||
                Contains(package.InstalledVersion, searchText) ||
                Contains(package.Source, searchText));
        }

        if (!string.Equals(SelectedSource, AllSources, StringComparison.OrdinalIgnoreCase))
        {
            packages = packages.Where(package =>
                string.Equals(package.Source, SelectedSource, StringComparison.OrdinalIgnoreCase));
        }

        packages = SelectedSort switch
        {
            "Name Z–A" => packages.OrderByDescending(package => package.Name, StringComparer.OrdinalIgnoreCase),
            "Version A–Z" => packages.OrderBy(package => GetVersionSortKey(package.InstalledVersion)),
            "Version Z–A" => packages.OrderByDescending(package => GetVersionSortKey(package.InstalledVersion)),
            "Source A–Z" => packages
                .OrderBy(package => package.Source, StringComparer.OrdinalIgnoreCase)
                .ThenBy(package => package.Name, StringComparer.OrdinalIgnoreCase),
            _ => packages.OrderBy(package => package.Name, StringComparer.OrdinalIgnoreCase)
        };

        foreach (var package in packages)
            Packages.Add(package);

        OnPropertyChanged(nameof(StatusText));
    }

    private static VersionSortKey GetVersionSortKey(string? value)
    {
        if (Version.TryParse(value, out var version))
            return new VersionSortKey(true, version, value ?? string.Empty);

        return new VersionSortKey(false, new Version(0, 0), value ?? string.Empty);
    }

    private static bool Contains(string? value, string searchText)
    {
        return value?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true;
    }

    private sealed class VersionSortKey : IComparable<VersionSortKey>
    {
        public bool IsVersion { get; }
        public Version Version { get; }
        public string Text { get; }

        public VersionSortKey(bool isVersion, Version version, string text)
        {
            IsVersion = isVersion;
            Version = version;
            Text = text;
        }

        public int CompareTo(VersionSortKey? other)
        {
            if (other is null)
                return 1;

            if (IsVersion && other.IsVersion)
                return Version.CompareTo(other.Version);

            if (IsVersion != other.IsVersion)
                return IsVersion ? -1 : 1;

            return StringComparer.OrdinalIgnoreCase.Compare(Text, other.Text);
        }
    }
}