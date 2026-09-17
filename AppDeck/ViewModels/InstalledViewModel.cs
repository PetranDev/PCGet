using AppDeck.Models;
using AppDeck.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace AppDeck.ViewModels;

public partial class InstalledViewModel : ObservableObject
{
    private readonly IWinGetService _winGetService;

    public ObservableCollection<PackageInfo> Packages { get; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public string StatusText
    {
        get
        {
            if (IsLoading)
                return "Loading installed applications...";

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

            Packages.Clear();

            foreach (var package in packages)
                Packages.Add(package);
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
}