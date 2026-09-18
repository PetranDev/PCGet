using AppDeck.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;

namespace AppDeck;

public partial class App : Application
{
    private readonly AppSettingsService _settingsService;
    private readonly BackgroundUpdateService _backgroundUpdateService;
    private readonly NotificationService _notificationService;

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
        _notificationService = new NotificationService();

        _trayIconService = new TrayIconService();
        _trayIconService.OpenRequested += TrayIconService_OpenRequested;

        _notificationService.Register();

        _backgroundUpdateService.CheckCompleted += BackgroundUpdateService_CheckCompleted;
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

    private void BackgroundUpdateService_CheckCompleted(object? sender, BackgroundUpdateCheckCompletedEventArgs e)
    {
        if (e.Updates.Count == 0)
            return;

        _notificationService.ShowUpdatesAvailable(e.Updates.Count);
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

        _backgroundUpdateService.CheckCompleted -= BackgroundUpdateService_CheckCompleted;
        _backgroundUpdateService.Dispose();
        _notificationService.Dispose();

        if (_trayIconService is not null)
        {
            _trayIconService.OpenRequested -= TrayIconService_OpenRequested;
            _trayIconService.Dispose();
            _trayIconService = null;
        }
    }
}