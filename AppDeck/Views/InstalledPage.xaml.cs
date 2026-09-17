using AppDeck.Services;
using AppDeck.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace AppDeck.Views;

public sealed partial class InstalledPage : Page
{
    public InstalledViewModel ViewModel { get; }

    public InstalledPage()
    {
        ViewModel =
            new InstalledViewModel(
                new WinGetService());

        InitializeComponent();

        Loaded += InstalledPage_Loaded;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private async void InstalledPage_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        Loaded -= InstalledPage_Loaded;

        await ViewModel.RefreshCommand.ExecuteAsync(null);
    }

    private void ViewModel_PropertyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ViewModel.ErrorMessage))
            return;

        ErrorInfoBar.Message =
            ViewModel.ErrorMessage ??
            string.Empty;

        ErrorInfoBar.IsOpen =
            !string.IsNullOrWhiteSpace(
                ViewModel.ErrorMessage);
    }
}