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

        var title = updateCount == 1 ? LocalizationService.GetString("Notification_OneUpdateAvailable") : LocalizationService.Format("Notification_UpdatesAvailable", updateCount);

        ShowNotification(
            title,
            LocalizationService.GetString("Notification_OpenPCGet"));
    }

    public void ShowSelfUpdateSucceeded()
    {
        ShowNotification(
            LocalizationService.GetString("Notification_SelfUpdateSucceededTitle"),
            LocalizationService.GetString("Notification_SelfUpdateSucceededMessage"));
    }

    public void ShowSelfUpdateFailed(int exitCode)
    {
        ShowNotification(
            LocalizationService.GetString("Notification_SelfUpdateFailedTitle"),
            LocalizationService.Format("Notification_SelfUpdateFailedMessage", exitCode));
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
