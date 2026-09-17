using CommunityToolkit.Mvvm.ComponentModel;

namespace AppDeck.Models;

public partial class PackageInfo : ObservableObject
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string InstalledVersion { get; init; }
    public required string AvailableVersion { get; init; }
    public required string Source { get; init; }

    [ObservableProperty]
    public partial PackageUpdateState UpdateState { get; set; }

    [ObservableProperty]
    public partial string UpdateStatus { get; set; } = "Ready";

    [ObservableProperty]
    public partial double UpdateProgress { get; set; }

    public bool IsUpdating =>
        UpdateState != PackageUpdateState.Ready;

    public bool IsUpdateEnabled =>
        UpdateState == PackageUpdateState.Ready;

    public string UpdateButtonText =>
        UpdateState switch
        {
            PackageUpdateState.Queued => "Queued",
            PackageUpdateState.Updating => "Updating...",
            PackageUpdateState.Failed => "Retry",
            _ => "Update"
        };

    partial void OnUpdateStateChanged(
        PackageUpdateState value)
    {
        OnPropertyChanged(nameof(IsUpdating));
        OnPropertyChanged(nameof(IsUpdateEnabled));
        OnPropertyChanged(nameof(UpdateButtonText));
    }
}

public enum PackageUpdateState
{
    Ready,
    Queued,
    Updating,
    Failed
}