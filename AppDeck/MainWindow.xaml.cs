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
    private readonly AppWindow _appWindow;

    public MainWindow()
    {
        InitializeComponent();

        var windowHandle = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        ConfigureWindow();
        ContentFrame.Navigate(typeof(UpdatesPage));
    }

    private void ConfigureWindow()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        _appWindow.Resize(new Windows.Graphics.SizeInt32(1200, 800));

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppDeck.ico");

        if (File.Exists(iconPath))
            _appWindow.SetIcon(iconPath);
    }

    private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        Type? pageType;

        if (args.IsSettingsSelected)
        {
            pageType = typeof(SettingsPage);
        }
        else if (args.SelectedItem is NavigationViewItem item)
        {
            pageType = item.Tag?.ToString() switch
            {
                "Updates" => typeof(UpdatesPage),
                "Installed" => typeof(InstalledPage),
                "Discover" => typeof(DiscoverPage),
                _ => null
            };
        }
        else
        {
            pageType = null;
        }

        if (pageType is not null && ContentFrame.CurrentSourcePageType != pageType)
            ContentFrame.Navigate(pageType);
    }
}