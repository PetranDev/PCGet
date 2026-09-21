using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;

namespace PCGet.Services;

public sealed class NotificationService : IDisposable
{
    private readonly AppNotificationManager _notificationManager;
    private bool _registered;
    private bool _disposed;

    public event EventHandler? NotificationInvoked;

    public NotificationService()
    {
        _notificationManager = AppNotificationManager.Default;
    }

    public void Register()
    {
        if (_registered)
            return;

        _notificationManager.NotificationInvoked += NotificationManager_NotificationInvoked;
        _notificationManager.Register();
        _registered = true;
    }

    public void ShowUpdatesAvailable(int updateCount)
    {
        if (!_registered || updateCount <= 0)
            return;

        var title = updateCount == 1 ? "1 update is available" : $"{updateCount} updates are available";

        ShowNotification(
            title,
            "Open PCGet to review and install available updates.");
    }

    public void ShowSelfUpdateSucceeded()
    {
        ShowNotification(
            "PCGet updated successfully",
            "The latest version of PCGet has been installed.");
    }

    public void ShowSelfUpdateFailed(int exitCode)
    {
        ShowNotification(
            "PCGet update failed",
            $"The update could not be installed. WinGet returned exit code {exitCode}.");
    }

    private void ShowNotification(string title, string message)
    {
        if (!_registered)
            return;

        var notification = new AppNotificationBuilder()
            .AddText(title)
            .AddText(message)
            .BuildNotification();

        _notificationManager.Show(notification);
    }

    private void NotificationManager_NotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        NotificationInvoked?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (!_registered)
            return;

        _notificationManager.NotificationInvoked -= NotificationManager_NotificationInvoked;
        _notificationManager.Unregister();
        _registered = false;
    }
}