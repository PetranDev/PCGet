using AppDeck.Models;
using AppDeck.Services;
using AppDeck.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace AppDeck.Views;

public sealed partial class UpdatesPage : Page
{
    private const bool UseSimulatedWinGet = true;

    public UpdatesViewModel ViewModel { get; }

    public UpdatesPage()
    {
        IWinGetService winGetService =
            UseSimulatedWinGet
                ? new SimulatedWinGetService()
                : new WinGetService();

        ViewModel =
            new UpdatesViewModel(
                winGetService);

        InitializeComponent();

        Loaded +=
            UpdatesPage_Loaded;

        ViewModel.PropertyChanged +=
            ViewModel_PropertyChanged;
    }

    private async void UpdatesPage_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        Loaded -=
            UpdatesPage_Loaded;

        await ViewModel.RefreshCommand.ExecuteAsync(
            null);
    }

    private async void UpdateButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        if (button.Tag is not PackageInfo package)
            return;

        button.IsEnabled = false;

        try
        {
            await ViewModel.UpdatePackageCommand.ExecuteAsync(
                package);
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private void ViewModel_PropertyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName !=
            nameof(ViewModel.ErrorMessage))
        {
            return;
        }

        ErrorInfoBar.Message =
            ViewModel.ErrorMessage ??
            string.Empty;

        ErrorInfoBar.IsOpen =
            !string.IsNullOrWhiteSpace(
                ViewModel.ErrorMessage);
    }
}