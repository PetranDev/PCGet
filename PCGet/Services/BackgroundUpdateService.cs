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
    // Don't compete with sign-in and app startup for CPU/network when a check is already overdue.
    private static readonly TimeSpan MinimumFirstCheckDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromMinutes(15);

    private readonly SemaphoreSlim _checkLock = new(1, 1);

    private Timer? _timer;
    private TimeSpan? _appliedInterval;
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
        if (!_settingsService.BackgroundUpdateChecks)
        {
            Stop();
            return;
        }

        var interval = TimeSpan.FromMinutes(_settingsService.BackgroundUpdateIntervalMinutes);

        if (interval < MinimumInterval)
            interval = MinimumInterval;

        // Re-applying unchanged settings must not restart the countdown.
        if (_timer is not null && _appliedInterval == interval)
            return;

        Stop();

        var firstCheckDelay = GetFirstCheckDelay(_settingsService.LastBackgroundUpdateCheckUtc, interval, DateTimeOffset.UtcNow);

        _timer = new Timer(
            TimerCallback,
            null,
            firstCheckDelay,
            interval);

        _appliedInterval = interval;
    }

    // The first check is due one interval after the last completed check, so a PC that is shut down
    // every evening still gets its daily check soon after the next sign-in instead of never.
    internal static TimeSpan GetFirstCheckDelay(DateTimeOffset? lastCheckUtc, TimeSpan interval, DateTimeOffset nowUtc)
    {
        if (lastCheckUtc is null)
            return MinimumFirstCheckDelay;

        var delay = lastCheckUtc.Value + interval - nowUtc;

        if (delay < MinimumFirstCheckDelay)
            return MinimumFirstCheckDelay;

        // A last-check time in the future (clock changed) must not postpone checks beyond one interval.
        return delay > interval ? interval : delay;
    }

    public async Task<IReadOnlyList<PackageInfo>> CheckNowAsync()
    {
        if (!await _checkLock.WaitAsync(0))
            return [];

        try
        {
            var updates = await Task.Run(async () => await _winGetService.GetAvailableUpdatesAsync());
            _settingsService.LastBackgroundUpdateCheckUtc = DateTimeOffset.UtcNow;

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
        _appliedInterval = null;
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