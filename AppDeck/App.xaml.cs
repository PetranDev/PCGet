using AppDeck.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;

namespace AppDeck;

public partial class App : Application
{
    private MainWindow? _window;
    private TrayIconService? _trayIconService;

    public static App? CurrentApp => Current as App;
    public DispatcherQueue DispatcherQueue { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
        DispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _trayIconService = new TrayIconService();
        _trayIconService.OpenRequested += TrayIconService_OpenRequested;
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

    private void TrayIconService_OpenRequested(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(ShowMainWindow);
    }

    private void Window_Closed(object sender, WindowEventArgs args)
    {
        if (_window is not null)
            _window.Closed -= Window_Closed;

        _window = null;

        if (_trayIconService is not null)
        {
            _trayIconService.OpenRequested -= TrayIconService_OpenRequested;
            _trayIconService.Dispose();
            _trayIconService = null;
        }
    }
}