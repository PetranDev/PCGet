using PCGet.Models;
using PCGet.Services;
using PCGet.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace PCGet.Views;

public sealed partial class UpdatesPage : Page
{
    private static bool _startupCheckHandled;
    private readonly AppSettingsService _settingsService;
    public UpdatesViewModel ViewModel { get; }

    public UpdatesPage()
    {
        _settingsService = new AppSettingsService();
        var elevatedOperationService = new ElevatedOperationService(_settingsService);

        ViewModel = new UpdatesViewModel(new WinGetService(), elevatedOperationService);

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

        if (package.IsPCGet)
        {
            await UpdatePCGetAsync(package);
            return;
        }

        if (package.UpdateState == PackageUpdateState.Failed)
        {
            await RetryAsAdministratorAsync(package);
            return;
        }

        await ViewModel.UpdatePackageCommand.ExecuteAsync(package);
    }

    private async Task UpdatePCGetAsync(PackageInfo package)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Update PCGet?",
            Content = $"PCGet {package.AvailableVersion} is available. PCGet will close, install the update, and start again automatically.",
            PrimaryButtonText = "Update PCGet",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary)
            return;

        try
        {
            var packagedUpdaterPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "PCGet.Updater",
                "PCGet.Updater.exe"));

            if (!File.Exists(packagedUpdaterPath))
                throw new FileNotFoundException("The PCGet updater could not be found.", packagedUpdaterPath);

            var temporaryUpdaterDirectory = Path.Combine(
                Path.GetTempPath(),
                "PCGet",
                "Updater",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(temporaryUpdaterDirectory);

            var temporaryUpdaterPath = Path.Combine(temporaryUpdaterDirectory, "PCGet.Updater.exe");
            File.Copy(packagedUpdaterPath, temporaryUpdaterPath, true);

            var packageId = Package.Current.Id;
            var pcGetAumid = $"{packageId.FamilyName}!App";
            var processId = Environment.ProcessId;

            Process.Start(new ProcessStartInfo
            {
                FileName = temporaryUpdaterPath,
                ArgumentList =
                {
                    processId.ToString(),
                    pcGetAumid
                },
                UseShellExecute = false
            });

            App.CurrentApp?.ExitApplication();
        }
        catch (Exception ex)
        {
            ViewModel.ErrorMessage = $"PCGet could not start the self-update process: {ex.Message}";
        }
    }

    private async Task RetryAsAdministratorAsync(PackageInfo package)
    {
        var argumentsTextBox = new TextBox
        {
            Header = "Additional WinGet arguments",
            Text = _settingsService.UpdateAdditionalArguments,
            PlaceholderText = "Example: --include-unknown",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var rememberCheckBox = new CheckBox
        {
            Content = "Remember these arguments for updates"
        };

        var content = new StackPanel
        {
            Spacing = 16
        };

        content.Children.Add(new TextBlock
        {
            Text = $"{package.Name} could not be updated normally. PCGet can retry the update with administrator privileges.",
            TextWrapping = TextWrapping.Wrap
        });

        content.Children.Add(argumentsTextBox);
        content.Children.Add(rememberCheckBox);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Retry as administrator?",
            Content = content,
            PrimaryButtonText = "Retry as administrator",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary)
            return;

        var additionalArguments = argumentsTextBox.Text.Trim();

        if (rememberCheckBox.IsChecked == true)
            _settingsService.UpdateAdditionalArguments = additionalArguments;

        await ViewModel.UpdatePackageAsAdministratorAsync(package, additionalArguments);
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ViewModel.ErrorMessage))
            return;

        ErrorInfoBar.Message = ViewModel.ErrorMessage ?? string.Empty;
        ErrorInfoBar.IsOpen = !string.IsNullOrWhiteSpace(ViewModel.ErrorMessage);
    }
}