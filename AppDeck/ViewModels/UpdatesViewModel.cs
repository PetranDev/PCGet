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
    private const string AllSources = "All sources";

    private readonly IWinGetService _winGetService;
    private readonly ElevatedOperationService _elevatedOperationService;
    private readonly Queue<PackageInfo> _updateQueue = new();
    private readonly List<PackageInfo> _allUpdates = [];

    private bool _isProcessingQueue;

    public ObservableCollection<PackageInfo> Updates { get; } = [];
    public ObservableCollection<string> Sources { get; } = [AllSources];
    public string[] SortOptions { get; } = ["Name A–Z", "Name Z–A", "Source A–Z"];

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedSource { get; set; } = AllSources;

    [ObservableProperty]
    public partial string SelectedSort { get; set; } = "Name A–Z";

    public bool IsUpdating => _isProcessingQueue;
    public int QueuedUpdateCount => _updateQueue.Count;

    public bool CanUpdateAll =>
        !IsLoading &&
        Updates.Any(package =>
            !package.IsAppDeck &&
            (package.UpdateState == PackageUpdateState.Ready ||
             package.UpdateState == PackageUpdateState.Failed));

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

            var isFiltered = !string.IsNullOrWhiteSpace(SearchText) ||
                             !string.Equals(SelectedSource, AllSources, StringComparison.OrdinalIgnoreCase);

            if (isFiltered)
            {
                return Updates.Count switch
                {
                    0 => "No matching updates.",
                    1 => $"1 of {_allUpdates.Count} updates shown",
                    _ => $"{Updates.Count} of {_allUpdates.Count} updates shown"
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
        foreach (var package in Updates.Where(package => !package.IsAppDeck).ToArray())
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

            RemoveUpdate(package);
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

            RemoveUpdate(package);
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

            _allUpdates.Clear();
            _allUpdates.AddRange(packages);

            RebuildSources();
            ApplyView();
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

    private void RemoveUpdate(PackageInfo package)
    {
        _allUpdates.RemoveAll(item => string.Equals(item.Id, package.Id, StringComparison.OrdinalIgnoreCase));

        RebuildSources();
        ApplyView();
    }

    private void RebuildSources()
    {
        var previousSource = SelectedSource;

        Sources.Clear();
        Sources.Add(AllSources);

        foreach (var source in _allUpdates
            .Select(package => package.Source)
            .Where(source => !string.IsNullOrWhiteSpace(source))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(source => source, StringComparer.OrdinalIgnoreCase))
        {
            Sources.Add(source);
        }

        SelectedSource = Sources.Any(source => string.Equals(source, previousSource, StringComparison.OrdinalIgnoreCase))
            ? previousSource
            : AllSources;
    }

    private void ApplyView()
    {
        IEnumerable<PackageInfo> packages = _allUpdates;
        var searchText = SearchText.Trim();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            packages = packages.Where(package =>
                Contains(package.Name, searchText) ||
                Contains(package.Id, searchText) ||
                Contains(package.InstalledVersion, searchText) ||
                Contains(package.AvailableVersion, searchText) ||
                Contains(package.Source, searchText));
        }

        if (!string.Equals(SelectedSource, AllSources, StringComparison.OrdinalIgnoreCase))
        {
            packages = packages.Where(package =>
                string.Equals(package.Source, SelectedSource, StringComparison.OrdinalIgnoreCase));
        }

        packages = SelectedSort switch
        {
            "Name Z–A" => packages.OrderByDescending(package => package.Name, StringComparer.OrdinalIgnoreCase),
            "Source A–Z" => packages
                .OrderBy(package => package.Source, StringComparer.OrdinalIgnoreCase)
                .ThenBy(package => package.Name, StringComparer.OrdinalIgnoreCase),
            _ => packages.OrderBy(package => package.Name, StringComparer.OrdinalIgnoreCase)
        };

        Updates.Clear();

        foreach (var package in packages)
            Updates.Add(package);

        NotifyQueueStateChanged();
    }

    private void NotifyQueueStateChanged()
    {
        OnPropertyChanged(nameof(IsUpdating));
        OnPropertyChanged(nameof(QueuedUpdateCount));
        OnPropertyChanged(nameof(CanUpdateAll));
        OnPropertyChanged(nameof(StatusText));
    }

    private static bool Contains(string? value, string searchText)
    {
        return value?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true;
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyView();
    }

    partial void OnSelectedSourceChanged(string value)
    {
        ApplyView();
    }

    partial void OnSelectedSortChanged(string value)
    {
        ApplyView();
    }
}