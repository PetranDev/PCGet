using AppDeck.Views;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using WinRT.Interop;

namespace AppDeck;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ConfigureWindow();

        ContentFrame.Navigate(typeof(UpdatesPage));
    }

    private void ConfigureWindow()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        var windowHandle = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppDeck.ico");

        if (File.Exists(iconPath))
            appWindow.SetIcon(iconPath);
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

        if (pageType is not null && ContentFrame.CurrentSourcePageType != pageType)
            ContentFrame.Navigate(pageType);
    }
}