using AppDeck.Services;
using AppDeck.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace AppDeck.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        ViewModel = new SettingsViewModel(new AppSettingsService(), new StartupService());
        InitializeComponent();
    }
}