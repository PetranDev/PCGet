using PCGet.Models;
using PCGet.Services;
using PCGet.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using System;
using Windows.System;

namespace PCGet.Views;

public sealed partial class DiscoverPage : Page
{
    private const double MinimumResultsWidth =
        350;

    private const double MinimumDetailsWidth =
        300;

    private readonly AppSettingsService _settingsService;

    public DiscoverViewModel ViewModel { get; }

    public DiscoverPage()
    {
        _settingsService =
            new AppSettingsService();

        var elevatedOperationService =
            new ElevatedOperationService(
                _settingsService);

        ViewModel =
            new DiscoverViewModel(
                new WinGetService(),
                elevatedOperationService);

        InitializeComponent();

        ViewModel.PropertyChanged +=
            ViewModel_PropertyChanged;
    }

    private async void SearchBox_QuerySubmitted(
        AutoSuggestBox sender,
        AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        await ViewModel
            .SearchCommand
            .ExecuteAsync(null);
    }

    private async void InstallButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        if (button.Tag is not DiscoverPackageInfo package)
            return;

        var succeeded =
            await ViewModel.InstallAsync(
                package,
                showError: false);

        if (succeeded)
            return;

        if (!ViewModel.LastInstallRequiresElevation)
        {
            ViewModel.ErrorMessage =
                ViewModel.LastInstallError;

            return;
        }

        var dialog =
            new ContentDialog
            {
                XamlRoot =
                    XamlRoot,

                Title =
                    LocalizationService.GetString("Dialog_AdminRequiredTitle"),

                Content =
                    LocalizationService.Format("Dialog_InstallAdminMessage", package.Name),

                PrimaryButtonText =
                    LocalizationService.GetString("Dialog_RetryAdminButton"),

                CloseButtonText =
                    LocalizationService.GetString("Common_Cancel"),

                DefaultButton =
                    ContentDialogButton.Primary
            };

        var result =
            await dialog.ShowAsync();

        if (result !=
            ContentDialogResult.Primary)
        {
            ViewModel.ErrorMessage =
                ViewModel.LastInstallError;

            return;
        }

        await ViewModel
            .InstallAsAdministratorAsync(
                package);
    }

    private void Splitter_DragDelta(
        object sender,
        DragDeltaEventArgs e)
    {
        var totalWidth =
            ResultsColumn.ActualWidth +
            DetailsColumn.ActualWidth;

        var newResultsWidth =
            ResultsColumn.ActualWidth +
            e.HorizontalChange;

        var newDetailsWidth =
            totalWidth -
            newResultsWidth;

        if (newResultsWidth <
            MinimumResultsWidth)
        {
            newResultsWidth =
                MinimumResultsWidth;

            newDetailsWidth =
                totalWidth -
                newResultsWidth;
        }

        if (newDetailsWidth <
            MinimumDetailsWidth)
        {
            newDetailsWidth =
                MinimumDetailsWidth;

            newResultsWidth =
                totalWidth -
                newDetailsWidth;
        }

        ResultsColumn.Width =
            new GridLength(
                newResultsWidth,
                GridUnitType.Pixel);

        DetailsColumn.Width =
            new GridLength(
                newDetailsWidth,
                GridUnitType.Pixel);
    }

    private void Splitter_PointerEntered(
        object sender,
        PointerRoutedEventArgs e)
    {
        ProtectedCursor =
            InputSystemCursor.Create(
                InputSystemCursorShape.SizeWestEast);
    }

    private void Splitter_PointerExited(
        object sender,
        PointerRoutedEventArgs e)
    {
        ProtectedCursor =
            null;
    }

    private async void LinkButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        if (button.Tag is not string url ||
            string.IsNullOrWhiteSpace(
                url))
        {
            return;
        }

        if (!Uri.TryCreate(
                url,
                UriKind.Absolute,
                out var uri))
        {
            return;
        }

        await Launcher.LaunchUriAsync(
            uri);
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
