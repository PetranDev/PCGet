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
    public partial bool IsUpdating { get; set; }

    [ObservableProperty]
    public partial string UpdateStatus { get; set; } = "Ready";

    [ObservableProperty]
    public partial double UpdateProgress { get; set; }

    public bool IsUpdateEnabled =>
        !IsUpdating;

    partial void OnIsUpdatingChanged(bool value)
    {
        OnPropertyChanged(
            nameof(IsUpdateEnabled));
    }
}