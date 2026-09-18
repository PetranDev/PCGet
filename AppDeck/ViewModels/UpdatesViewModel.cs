using AppDeck.Models;
using AppDeck.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace AppDeck.ViewModels;

public partial class UpdatesViewModel : ObservableObject
{
    private readonly IWinGetService _winGetService;
    private readonly ElevatedOperationService _elevatedOperationService;
    private readonly Queue<PackageInfo> _updateQueue = new();

    private bool _isProcessingQueue;

    public ObservableCollection<PackageInfo> Updates { get; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public bool IsUpdating => _isProcessingQueue;
    public int QueuedUpdateCount => _updateQueue.Count;

    public bool CanUpdateAll =>
        !IsLoading &&
        Updates.Any(package =>
            package.UpdateState == PackageUpdateState.Ready ||
            package.UpdateState == PackageUpdateState.Failed);

    public string StatusText
    {
        get
        {
            if (IsLoading)
                return "Checking for updates...";

            if (IsUpdating)
            {
                return QueuedUpdateCount switch
                {
                    0 => "Installing update...",
                    1 => "Installing update · 1 queued",
                    _ => $"Installing update · {QueuedUpdateCount} queued"
                };
            }

            return Updates.Count switch
            {
                0 => "Your applications are up to date.",
                1 => "1 update available",
                _ => $"{Updates.Count} updates available"
            };
        }
    }

    public UpdatesViewModel(IWinGetService winGetService, ElevatedOperationService elevatedOperationService)
    {
        _winGetService = winGetService;
        _elevatedOperationService = elevatedOperationService;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsLoading || IsUpdating)
            return;

        await LoadUpdatesAsync();
    }

    [RelayCommand]
    private Task UpdatePackageAsync(PackageInfo package)
    {
        QueuePackage(package);
        StartQueueIfNecessary();

        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task UpdateAllAsync()
    {
        foreach (var package in Updates.ToArray())
            QueuePackage(package);

        StartQueueIfNecessary();

        return Task.CompletedTask;
    }

    public async Task UpdatePackageAsAdministratorAsync(PackageInfo package)
    {
        if (package.UpdateState == PackageUpdateState.Updating)
            return;

        try
        {
            ErrorMessage = null;

            package.UpdateState = PackageUpdateState.Updating;
            package.UpdateStatus = "Updating as administrator...";
            package.UpdateProgress = 0;
            package.IsUpdateIndeterminate = true;

            NotifyQueueStateChanged();

            await _elevatedOperationService.UpdatePackageAsync(package.Id);

            package.IsUpdateIndeterminate = false;
            package.UpdateStatus = "Completed";
            package.UpdateProgress = 100;

            await Task.Delay(500);

            Updates.Remove(package);
        }
        catch (Exception ex)
        {
            package.IsUpdateIndeterminate = false;
            package.UpdateState = PackageUpdateState.Failed;
            package.UpdateStatus = "Failed";
            package.UpdateProgress = 0;

            ErrorMessage = $"{package.Name}: {ex.Message}";
        }
        finally
        {
            package.IsUpdateIndeterminate = false;
            NotifyQueueStateChanged();
        }
    }

    private void QueuePackage(PackageInfo package)
    {
        if (package.UpdateState == PackageUpdateState.Queued ||
            package.UpdateState == PackageUpdateState.Updating)
            return;

        package.UpdateState = PackageUpdateState.Queued;
        package.UpdateStatus = "Queued";
        package.UpdateProgress = 0;
        package.IsUpdateIndeterminate = false;

        _updateQueue.Enqueue(package);

        NotifyQueueStateChanged();
    }

    private void StartQueueIfNecessary()
    {
        if (!_isProcessingQueue && _updateQueue.Count > 0)
            _ = ProcessUpdateQueueAsync();
    }

    private async Task ProcessUpdateQueueAsync()
    {
        if (_isProcessingQueue)
            return;

        _isProcessingQueue = true;
        NotifyQueueStateChanged();

        try
        {
            while (_updateQueue.Count > 0)
            {
                var package = _updateQueue.Dequeue();

                package.UpdateState = PackageUpdateState.Updating;
                NotifyQueueStateChanged();

                await ProcessPackageAsync(package);
            }
        }
        finally
        {
            _isProcessingQueue = false;
            NotifyQueueStateChanged();
        }
    }

    private async Task ProcessPackageAsync(PackageInfo package)
    {
        try
        {
            ErrorMessage = null;

            package.UpdateStatus = "Preparing...";
            package.UpdateProgress = 0;
            package.IsUpdateIndeterminate = false;

            var progress = new Progress<PackageUpdateProgress>(value =>
            {
                package.UpdateStatus = value.Status;
                package.UpdateProgress = value.Percent;
            });

            await _winGetService.UpdatePackageAsync(package.Id, progress);

            package.UpdateStatus = "Completed";
            package.UpdateProgress = 100;

            await Task.Delay(300);

            Updates.Remove(package);
        }
        catch (Exception ex)
        {
            package.UpdateState = PackageUpdateState.Failed;
            package.UpdateStatus = "Failed";
            package.UpdateProgress = 0;

            ErrorMessage = $"{package.Name}: {ex.Message}";
        }
        finally
        {
            package.IsUpdateIndeterminate = false;
            NotifyQueueStateChanged();
        }
    }

    private async Task LoadUpdatesAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            NotifyQueueStateChanged();

            var packages = await _winGetService.GetAvailableUpdatesAsync();

            Updates.Clear();

            foreach (var package in packages)
                Updates.Add(package);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
            NotifyQueueStateChanged();
        }
    }

    private void NotifyQueueStateChanged()
    {
        OnPropertyChanged(nameof(IsUpdating));
        OnPropertyChanged(nameof(QueuedUpdateCount));
        OnPropertyChanged(nameof(CanUpdateAll));
        OnPropertyChanged(nameof(StatusText));
    }
}