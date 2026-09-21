using PCGet.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PCGet.Services;

public sealed class BackgroundUpdateService : IDisposable
{
    private readonly IWinGetService _winGetService;
    private readonly AppSettingsService _settingsService;
    private readonly SemaphoreSlim _checkLock = new(1, 1);

    private Timer? _timer;
    private HashSet<string>? _lastUpdateIds;
    private bool _disposed;

    public event EventHandler<BackgroundUpdateCheckCompletedEventArgs>? CheckCompleted;

    public BackgroundUpdateService(IWinGetService winGetService, AppSettingsService settingsService)
    {
        _winGetService = winGetService;
        _settingsService = settingsService;
    }

    public void ApplySettings()
    {
        Stop();

        if (!_settingsService.BackgroundUpdateChecks)
            return;

        var interval = TimeSpan.FromMinutes(_settingsService.BackgroundUpdateIntervalMinutes);

        _timer = new Timer(
            TimerCallback,
            null,
            interval,
            interval);
    }

    public async Task<IReadOnlyList<PackageInfo>> CheckNowAsync()
    {
        if (!await _checkLock.WaitAsync(0))
            return [];

        try
        {
            var updates = await Task.Run(async () => await _winGetService.GetAvailableUpdatesAsync());
            var updateIds = updates.Select(update => update.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var hasChanged = _lastUpdateIds is null || !_lastUpdateIds.SetEquals(updateIds);

            _lastUpdateIds = updateIds;

            CheckCompleted?.Invoke(
                this,
                new BackgroundUpdateCheckCompletedEventArgs(updates, hasChanged));

            return updates;
        }
        finally
        {
            _checkLock.Release();
        }
    }

    private void TimerCallback(object? state)
    {
        _ = CheckInBackgroundAsync();
    }

    private async Task CheckInBackgroundAsync()
    {
        try
        {
            await CheckNowAsync();
        }
        catch
        {
            // A failed background check must not terminate the resident application.
        }
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        Stop();
        _checkLock.Dispose();
    }
}

public sealed class BackgroundUpdateCheckCompletedEventArgs : EventArgs
{
    public IReadOnlyList<PackageInfo> Updates { get; }
    public bool HasChanged { get; }

    public BackgroundUpdateCheckCompletedEventArgs(IReadOnlyList<PackageInfo> updates, bool hasChanged)
    {
        Updates = updates;
        HasChanged = hasChanged;
    }
}