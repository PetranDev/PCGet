using AppDeck.Models;
using AppDeck.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace AppDeck.ViewModels;

public partial class DiscoverViewModel : ObservableObject
{
    private readonly IWinGetService _winGetService;

    public ObservableCollection<DiscoverPackageInfo> Packages { get; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsSearching { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial DiscoverPackageInfo? SelectedPackage { get; set; }

    public bool HasSelectedPackage =>
        SelectedPackage is not null;

    public string StatusText
    {
        get
        {
            if (IsSearching)
                return "Searching...";

            if (string.IsNullOrWhiteSpace(SearchText))
                return "Search WinGet for applications.";

            return Packages.Count switch
            {
                0 => "No applications found.",
                1 => "1 application found",
                _ => $"{Packages.Count} applications found"
            };
        }
    }

    public DiscoverViewModel(
        IWinGetService winGetService)
    {
        _winGetService = winGetService;
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (IsSearching)
            return;

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            Packages.Clear();
            SelectedPackage = null;
            OnPropertyChanged(nameof(StatusText));
            return;
        }

        try
        {
            IsSearching = true;
            ErrorMessage = null;
            SelectedPackage = null;

            OnPropertyChanged(nameof(StatusText));

            var query =
                SearchText.Trim();

            var packages =
                await Task.Run(
                    async () =>
                        await _winGetService.SearchPackagesAsync(
                            query));

            Packages.Clear();

            foreach (var package in packages)
                Packages.Add(package);

            if (Packages.Count > 0)
                SelectedPackage = Packages[0];
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

    public async Task InstallAsync(
        DiscoverPackageInfo package)
    {
        if (package.IsInstalled ||
            package.IsInstalling)
        {
            return;
        }

        try
        {
            ErrorMessage = null;

            package.IsInstalling = true;
            package.InstallStatus = "Preparing...";
            package.InstallProgress = 0;

            var progress =
                new Progress<PackageInstallProgress>(
                    value =>
                    {
                        package.InstallStatus =
                            value.Status;

                        package.InstallProgress =
                            value.Percent;
                    });

            await Task.Run(
                async () =>
                    await _winGetService.InstallPackageAsync(
                        package.Id,
                        progress));

            package.InstallStatus = "Installed";
            package.InstallProgress = 100;
            package.IsInstalled = true;
        }
        catch (Exception ex)
        {
            package.InstallStatus = "Failed";
            package.InstallProgress = 0;

            ErrorMessage =
                $"{package.Name}: {ex.Message}";
        }
        finally
        {
            package.IsInstalling = false;
        }
    }

    partial void OnSelectedPackageChanged(
        DiscoverPackageInfo? value)
    {
        OnPropertyChanged(nameof(HasSelectedPackage));
    }
}