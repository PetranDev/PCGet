using AppDeck.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI.ApplicationSettings;

namespace AppDeck;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ContentFrame.Navigate(typeof(UpdatesPage));
    }

    private void NavigationView_SelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItem is not NavigationViewItem item)
            return;

        var pageType = item.Tag?.ToString() switch
        {
            "Updates" => typeof(UpdatesPage),
            "Installed" => typeof(InstalledPage),
            "Discover" => typeof(DiscoverPage),
            _ => null
        };

        if (pageType is not null &&
            ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }
    }
}