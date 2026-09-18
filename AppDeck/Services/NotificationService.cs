using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;

namespace AppDeck.Services;

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

        var title = updateCount == 1
            ? "1 update is available"
            : $"{updateCount} updates are available";

        var notification = new AppNotificationBuilder()
            .AddText(title)
            .AddText("Open AppDeck to review and install available updates.")
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