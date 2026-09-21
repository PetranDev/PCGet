using PCGet.Services;
using PCGet.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace PCGet.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        ViewModel = new SettingsViewModel(new AppSettingsService(), new StartupService());
        InitializeComponent();
    }
}