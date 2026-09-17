using AppDeck.Models;
using AppDeck.Services;
using AppDeck.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace AppDeck.Views;

public sealed partial class DiscoverPage : Page
{
    public DiscoverViewModel ViewModel { get; }

    public DiscoverPage()
    {
        ViewModel =
            new DiscoverViewModel(
                new WinGetService());

        InitializeComponent();

        ViewModel.PropertyChanged +=
            ViewModel_PropertyChanged;
    }

    private async void SearchBox_QuerySubmitted(
        AutoSuggestBox sender,
        AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        await ViewModel.SearchCommand.ExecuteAsync(
            null);
    }

    private async void InstallButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        if (button.Tag is not DiscoverPackageInfo package)
            return;

        await ViewModel.InstallAsync(
            package);
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