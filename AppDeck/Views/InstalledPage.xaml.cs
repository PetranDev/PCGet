using AppDeck.Models;
using AppDeck.Services;
using AppDeck.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace AppDeck.Views;

public sealed partial class InstalledPage : Page
{
    private readonly AppSettingsService _settingsService;

    public InstalledViewModel ViewModel { get; }

    public InstalledPage()
    {
        _settingsService =
            new AppSettingsService();

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

    private async void UninstallButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        if (button.Tag is not PackageInfo package)
            return;

        if (ViewModel.IsUninstalling)
            return;

        var confirmationDialog =
            new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Uninstall application?",
                Content =
                    $"Are you sure you want to uninstall {package.Name}?",
                PrimaryButtonText = "Uninstall",
                CloseButtonText = "Cancel",
                DefaultButton =
                    ContentDialogButton.Close
            };

        var confirmationResult =
            await confirmationDialog.ShowAsync();

        if (confirmationResult != ContentDialogResult.Primary)
            return;

        var silentMode =
            _settingsService.SilentPackageOperations;

        var succeeded =
            await ViewModel.UninstallAsync(
                package,
                showError: !silentMode);

        if (succeeded)
            return;

        if (!silentMode)
            return;

        var silentError =
            ViewModel.LastUninstallError;

        var retryDialog =
            new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Silent uninstall failed",
                Content =
                    $"{package.Name} could not be uninstalled silently. Would you like to retry using the application's interactive uninstaller?",
                PrimaryButtonText = "Retry interactively",
                CloseButtonText = "Cancel",
                DefaultButton =
                    ContentDialogButton.Primary
            };

        var retryResult =
            await retryDialog.ShowAsync();

        if (retryResult != ContentDialogResult.Primary)
        {
            ViewModel.ErrorMessage =
                silentError;

            return;
        }

        await ViewModel.UninstallAsync(
            package,
            interactive: true,
            showError: true);
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