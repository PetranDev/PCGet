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
    private readonly List<PackageInfo> _allPackages = [];

    public ObservableCollection<PackageInfo> Packages { get; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    public string StatusText
    {
        get
        {
            if (IsLoading)
                return "Loading installed applications...";

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

    public InstalledViewModel(
        IWinGetService winGetService)
    {
        _winGetService = winGetService;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsLoading)
            return;

        try
        {
            IsLoading = true;
            ErrorMessage = null;
            OnPropertyChanged(nameof(StatusText));

            var packages =
                await _winGetService.GetInstalledPackagesAsync();

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

    partial void OnSearchTextChanged(
        string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        Packages.Clear();

        IEnumerable<PackageInfo> packages =
            _allPackages;

        var searchText =
            SearchText.Trim();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            packages =
                packages.Where(
                    package =>
                        Contains(
                            package.Name,
                            searchText) ||
                        Contains(
                            package.Id,
                            searchText) ||
                        Contains(
                            package.InstalledVersion,
                            searchText) ||
                        Contains(
                            package.Source,
                            searchText));
        }

        foreach (var package in packages)
            Packages.Add(package);

        OnPropertyChanged(nameof(StatusText));
    }

    private static bool Contains(
        string? value,
        string searchText)
    {
        return value?.Contains(
            searchText,
            StringComparison.OrdinalIgnoreCase) == true;
    }
}