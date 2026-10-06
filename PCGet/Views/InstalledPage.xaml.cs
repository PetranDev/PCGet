using PCGet.Models;
using PCGet.Services;
using PCGet.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PCGet.Views;

public sealed partial class InstalledPage : Page
{
    private readonly AppSettingsService _settingsService;

    public InstalledViewModel ViewModel { get; }

    public InstalledPage()
    {
        _settingsService = new AppSettingsService();

        var elevatedOperationService = new ElevatedOperationService(_settingsService);
        ViewModel = new InstalledViewModel(new WinGetService(), elevatedOperationService);

        InitializeComponent();

        Loaded += InstalledPage_Loaded;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private async void InstalledPage_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= InstalledPage_Loaded;
        await ViewModel.RefreshCommand.ExecuteAsync(null);
    }

    private async void UninstallButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        if (button.Tag is not PackageInfo package)
            return;

        if (ViewModel.IsUninstalling)
            return;

        var confirmationDialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = LocalizationService.GetString("Dialog_UninstallTitle"),
            Content = LocalizationService.Format("Dialog_UninstallMessage", package.Name),
            PrimaryButtonText = LocalizationService.GetString("Common_Uninstall"),
            CloseButtonText = LocalizationService.GetString("Common_Cancel"),
            DefaultButton = ContentDialogButton.Close
        };

        var confirmationResult = await confirmationDialog.ShowAsync();

        if (confirmationResult != ContentDialogResult.Primary)
            return;

        await UninstallPackageAsync(package);
    }

    private async void UninstallSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsUninstalling)
            return;

        var packages = PackagesListView.SelectedItems.OfType<PackageInfo>().ToArray();

        if (packages.Length == 0)
            return;

        var confirmationDialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = packages.Length == 1 ? LocalizationService.GetString("Dialog_UninstallSelectedOneTitle") : LocalizationService.GetString("Dialog_UninstallSelectedManyTitle"),
            Content = packages.Length == 1
                ? LocalizationService.Format("Dialog_UninstallMessage", packages[0].Name)
                : LocalizationService.Format("Dialog_UninstallManyMessage", packages.Length),
            PrimaryButtonText = packages.Length == 1 ? LocalizationService.GetString("Common_Uninstall") : LocalizationService.Format("Dialog_UninstallManyButton", packages.Length),
            CloseButtonText = LocalizationService.GetString("Common_Cancel"),
            DefaultButton = ContentDialogButton.Close
        };

        var confirmationResult = await confirmationDialog.ShowAsync();

        if (confirmationResult != ContentDialogResult.Primary)
            return;

        UninstallSelectedButton.IsEnabled = false;

        foreach (var package in packages)
            await UninstallPackageAsync(package);

        UpdateSelectionStatus();
    }

    private async Task<bool> UninstallPackageAsync(PackageInfo package)
    {
        var silentMode = _settingsService.SilentPackageOperations;
        var succeeded = await ViewModel.UninstallAsync(package, showError: false);

        if (succeeded)
            return true;

        if (ViewModel.LastUninstallRequiresElevation)
            return await OfferAdministratorRetryAsync(package, interactive: !silentMode);

        if (!silentMode)
        {
            ViewModel.ErrorMessage = ViewModel.LastUninstallError;
            return false;
        }

        var silentError = ViewModel.LastUninstallError;

        var retryDialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = LocalizationService.GetString("Dialog_SilentUninstallFailedTitle"),
            Content = LocalizationService.Format("Dialog_SilentUninstallFailedMessage", package.Name),
            PrimaryButtonText = LocalizationService.GetString("Dialog_RetryInteractively"),
            CloseButtonText = LocalizationService.GetString("Common_Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };

        var retryResult = await retryDialog.ShowAsync();

        if (retryResult != ContentDialogResult.Primary)
        {
            ViewModel.ErrorMessage = silentError;
            return false;
        }

        succeeded = await ViewModel.UninstallAsync(package, interactive: true, showError: false);

        if (succeeded)
            return true;

        if (ViewModel.LastUninstallRequiresElevation)
            return await OfferAdministratorRetryAsync(package, interactive: true);

        ViewModel.ErrorMessage = ViewModel.LastUninstallError;
        return false;
    }

    private async Task<bool> OfferAdministratorRetryAsync(PackageInfo package, bool interactive)
    {
        var error = ViewModel.LastUninstallError;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = LocalizationService.GetString("Dialog_AdminRequiredTitle"),
            Content = LocalizationService.Format("Dialog_UninstallAdminMessage", package.Name),
            PrimaryButtonText = LocalizationService.GetString("Dialog_RetryAdminButton"),
            CloseButtonText = LocalizationService.GetString("Common_Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary)
        {
            ViewModel.ErrorMessage = error;
            return false;
        }

        return await ViewModel.UninstallAsAdministratorAsync(package, interactive);
    }

    private void PackagesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSelectionStatus();
    }

    private void UpdateSelectionStatus()
    {
        var count = PackagesListView.SelectedItems.Count;

        UninstallSelectedButton.IsEnabled = count > 0 && !ViewModel.IsUninstalling;

        SelectionStatusText.Text = count switch
        {
            0 => string.Empty,
            1 => LocalizationService.GetString("Installed_OneSelected"),
            _ => LocalizationService.Format("Installed_SelectedCount", count)
        };
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.ErrorMessage))
        {
            ErrorInfoBar.Message = ViewModel.ErrorMessage ?? string.Empty;
            ErrorInfoBar.IsOpen = !string.IsNullOrWhiteSpace(ViewModel.ErrorMessage);
            return;
        }

        if (e.PropertyName == nameof(ViewModel.IsUninstalling))
            UpdateSelectionStatus();
    }
}
