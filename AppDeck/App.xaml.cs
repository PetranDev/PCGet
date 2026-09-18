using AppDeck.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;

namespace AppDeck;

public partial class App : Application
{
    private readonly AppSettingsService _settingsService;
    private readonly BackgroundUpdateService _backgroundUpdateService;

    private MainWindow? _window;
    private TrayIconService? _trayIconService;

    public static App? CurrentApp => Current as App;
    public DispatcherQueue DispatcherQueue { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
        DispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _settingsService = new AppSettingsService();
        _backgroundUpdateService = new BackgroundUpdateService(new WinGetService(), _settingsService);

        _trayIconService = new TrayIconService();
        _trayIconService.OpenRequested += TrayIconService_OpenRequested;

        _backgroundUpdateService.ApplySettings();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        ShowMainWindow();
    }

    public void ShowMainWindow()
    {
        if (_window is null)
        {
            _window = new MainWindow();
            _window.Closed += Window_Closed;
        }

        _window.Activate();
    }

    public void ApplyBackgroundUpdateSettings()
    {
        _backgroundUpdateService.ApplySettings();
    }

    private void TrayIconService_OpenRequested(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(ShowMainWindow);
    }

    private void Window_Closed(object sender, WindowEventArgs args)
    {
        if (_window is not null)
            _window.Closed -= Window_Closed;

        _window = null;

        _backgroundUpdateService.Dispose();

        if (_trayIconService is not null)
        {
            _trayIconService.OpenRequested -= TrayIconService_OpenRequested;
            _trayIconService.Dispose();
            _trayIconService = null;
        }
    }
}