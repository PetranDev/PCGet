using AppDeck.Models;
using AppDeck.Services;
using AppDeck.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace AppDeck.Views;

public sealed partial class UpdatesPage : Page
{
    private static bool _startupCheckHandled;

    private readonly AppSettingsService _settingsService;

    public UpdatesViewModel ViewModel { get; }

    public UpdatesPage()
    {
        _settingsService = new AppSettingsService();

        var elevatedOperationService = new ElevatedOperationService(_settingsService);

        ViewModel = new UpdatesViewModel(
            new WinGetService(),
            elevatedOperationService);

        InitializeComponent();

        Loaded += UpdatesPage_Loaded;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private async void UpdatesPage_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= UpdatesPage_Loaded;

        if (_startupCheckHandled)
            return;

        _startupCheckHandled = true;

        if (!_settingsService.CheckUpdatesOnStartup)
            return;

        await ViewModel.RefreshCommand.ExecuteAsync(null);
    }

    private async void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        if (button.Tag is not PackageInfo package)
            return;

        if (package.IsAppDeck)
        {
            await UpdateAppDeckAsync(package);
            return;
        }

        if (package.UpdateState == PackageUpdateState.Failed)
        {
            await RetryAsAdministratorAsync(package);
            return;
        }

        await ViewModel.UpdatePackageCommand.ExecuteAsync(package);
    }

    private async Task UpdateAppDeckAsync(PackageInfo package)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Update AppDeck?",
            Content = $"AppDeck {package.AvailableVersion} is available. AppDeck will close, install the update, and start again automatically.",
            PrimaryButtonText = "Update AppDeck",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary)
            return;

        try
        {
            var updaterPath = Path.Combine(AppContext.BaseDirectory, "Updater", "AppDeck.Updater.exe");

            if (!File.Exists(updaterPath))
                throw new FileNotFoundException("The AppDeck updater could not be found.", updaterPath);

            var appDeckExecutable = Environment.ProcessPath;

            if (string.IsNullOrWhiteSpace(appDeckExecutable))
                throw new InvalidOperationException("The AppDeck executable path could not be determined.");

            var processId = Environment.ProcessId;

            Process.Start(new ProcessStartInfo
            {
                FileName = updaterPath,
                ArgumentList =
                {
                    processId.ToString(),
                    appDeckExecutable
                },
                UseShellExecute = false
            });

            App.CurrentApp?.ExitApplication();
        }
        catch (Exception ex)
        {
            ViewModel.ErrorMessage = $"AppDeck could not start the self-update process: {ex.Message}";
        }
    }

    private async Task RetryAsAdministratorAsync(PackageInfo package)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Retry as administrator?",
            Content = $"{package.Name} could not be updated normally. AppDeck can retry the update with administrator privileges.",
            PrimaryButtonText = "Retry as administrator",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary)
            return;

        await ViewModel.UpdatePackageAsAdministratorAsync(package);
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ViewModel.ErrorMessage))
            return;

        ErrorInfoBar.Message = ViewModel.ErrorMessage ?? string.Empty;
        ErrorInfoBar.IsOpen = !string.IsNullOrWhiteSpace(ViewModel.ErrorMessage);
    }
}