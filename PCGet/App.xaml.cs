using PCGet.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Text.Json;

namespace PCGet;

public partial class App : Application
{
    private readonly AppSettingsService _settingsService;
    private readonly BackgroundUpdateService _backgroundUpdateService;
    private readonly NotificationService _notificationService;
    private readonly bool _startedByWindows;

    private MainWindow? _window;
    private TrayIconService? _trayIconService;
    private bool _isExiting;

    public static App? CurrentApp => Current as App;
    public DispatcherQueue DispatcherQueue { get; private set; } = null!;

    public App(bool startedByWindows)
    {
        _settingsService = new AppSettingsService();
        LocalizationService.ApplyLanguage(_settingsService.Language);

        InitializeComponent();
        DispatcherQueue = DispatcherQueue.GetForCurrentThread();

        UpdaterCleanupService.Cleanup();

        _startedByWindows = startedByWindows;
        _backgroundUpdateService = new BackgroundUpdateService(new WinGetService(), _settingsService);
        _notificationService = new NotificationService();

        _trayIconService = new TrayIconService();
        _trayIconService.OpenRequested += TrayIconService_OpenRequested;
        _trayIconService.CheckUpdatesRequested += TrayIconService_CheckUpdatesRequested;
        _trayIconService.ExitRequested += TrayIconService_ExitRequested;

        _notificationService.NotificationInvoked += NotificationService_NotificationInvoked;
        _notificationService.Register();

        _backgroundUpdateService.CheckCompleted += BackgroundUpdateService_CheckCompleted;
        _backgroundUpdateService.ApplySettings();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (_startedByWindows && _settingsService.BackgroundUpdateChecks)
            return;

        ShowMainWindow();
        ShowPendingSelfUpdateResult();
    }

    public void ShowMainWindow()
    {
        if (_window is null)
        {
            _window = new MainWindow();
            _window.AppWindow.Closing += AppWindow_Closing;
            _window.Closed += Window_Closed;
        }

        _window.AppWindow.Show();
        _window.Activate();
    }

    public void ApplyBackgroundUpdateSettings()
    {
        _backgroundUpdateService.ApplySettings();
    }

    public void ExitApplication()
    {
        if (_isExiting)
            return;

        _isExiting = true;

        if (_window is not null)
        {
            _window.Close();
            return;
        }

        DisposeServices();
        Exit();
    }

    private void ShowPendingSelfUpdateResult()
    {
        var path = Path.Combine(Path.GetTempPath(), "PCGet", "SelfUpdateResult.json");

        if (!File.Exists(path))
            return;

        try
        {
            var json = File.ReadAllText(path);
            var result = JsonSerializer.Deserialize<SelfUpdateResult>(json);

            if (result is null)
                return;

            if (result.Succeeded)
                _notificationService.ShowSelfUpdateSucceeded();
            else
                _notificationService.ShowSelfUpdateFailed(result.ExitCode);
        }
        catch
        {
            // A malformed or inaccessible result file must never prevent PCGet from starting.
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // Failure to delete the result file must not terminate PCGet.
            }
        }
    }

    private void BackgroundUpdateService_CheckCompleted(object? sender, BackgroundUpdateCheckCompletedEventArgs e)
    {
        if (!e.HasChanged || e.Updates.Count == 0)
            return;

        _notificationService.ShowUpdatesAvailable(e.Updates.Count);
    }

    private void NotificationService_NotificationInvoked(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ShowMainWindow();
            _window?.NavigateToUpdates();
        });
    }

    private void TrayIconService_OpenRequested(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(ShowMainWindow);
    }

    private void TrayIconService_CheckUpdatesRequested(object? sender, EventArgs e)
    {
        _ = CheckForUpdatesFromTrayAsync();
    }

    private async System.Threading.Tasks.Task CheckForUpdatesFromTrayAsync()
    {
        try
        {
            await _backgroundUpdateService.CheckNowAsync();
        }
        catch
        {
            // A manual tray check failure must not terminate PCGet.
        }
    }

    private void TrayIconService_ExitRequested(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(ExitApplication);
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_isExiting || !_settingsService.BackgroundUpdateChecks)
            return;

        args.Cancel = true;
        sender.Hide();
    }

    private void Window_Closed(object sender, WindowEventArgs args)
    {
        if (_window is not null)
        {
            _window.AppWindow.Closing -= AppWindow_Closing;
            _window.Closed -= Window_Closed;
        }

        _window = null;

        DisposeServices();

        if (_isExiting)
            Exit();
    }

    private void DisposeServices()
    {
        _backgroundUpdateService.CheckCompleted -= BackgroundUpdateService_CheckCompleted;
        _backgroundUpdateService.Dispose();

        _notificationService.NotificationInvoked -= NotificationService_NotificationInvoked;
        _notificationService.Dispose();

        if (_trayIconService is null)
            return;

        _trayIconService.OpenRequested -= TrayIconService_OpenRequested;
        _trayIconService.CheckUpdatesRequested -= TrayIconService_CheckUpdatesRequested;
        _trayIconService.ExitRequested -= TrayIconService_ExitRequested;
        _trayIconService.Dispose();
        _trayIconService = null;
    }

    private sealed class SelfUpdateResult
    {
        public bool Succeeded { get; set; }
        public int ExitCode { get; set; }
        public DateTimeOffset CompletedAtUtc { get; set; }
    }
}
