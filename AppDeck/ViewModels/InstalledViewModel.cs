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

public partial class InstalledViewModel : ObservableObject
{
    private readonly IWinGetService _winGetService;
    private readonly ElevatedOperationService _elevatedOperationService;
    private readonly List<PackageInfo> _allPackages = [];

    public ObservableCollection<PackageInfo> Packages { get; } = [];

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

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                return Packages.Count switch
                {
                    0 => "No matching applications.",
                    1 => "1 matching application",
                    _ => $"{Packages.Count} matching applications"
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

            var packages = await Task.Run(async () =>
                await _winGetService.GetInstalledPackagesAsync());

            _allPackages.Clear();
            _allPackages.AddRange(packages);

            ApplyFilter();
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

            await Task.Run(async () =>
                await _winGetService.UninstallPackageAsync(package.Id, progress, interactive));

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
        ApplyFilter();
    }

    private void RemovePackage(PackageInfo package)
    {
        _allPackages.RemoveAll(item =>
            string.Equals(item.Id, package.Id, StringComparison.OrdinalIgnoreCase));

        ApplyFilter();
    }

    private void ApplyFilter()
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

        foreach (var package in packages)
            Packages.Add(package);

        OnPropertyChanged(nameof(StatusText));
    }

    private static bool Contains(string? value, string searchText)
    {
        return value?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true;
    }
}