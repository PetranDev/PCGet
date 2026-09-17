using AppDeck.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AppDeck.Services;

public sealed class SimulatedWinGetService : IWinGetService
{
    private readonly List<PackageInfo> _packages =
    [
        CreatePackage(
            "Microsoft.VisualStudioCode",
            "Visual Studio Code",
            "1.104.0",
            "1.105.1"),

        CreatePackage(
            "Google.Chrome",
            "Google Chrome",
            "139.0.7258.155",
            "140.0.7339.81"),

        CreatePackage(
            "7zip.7zip",
            "7-Zip",
            "24.09",
            "25.01"),

        CreatePackage(
            "VideoLAN.VLC",
            "VLC media player",
            "3.0.21",
            "3.0.22"),

        CreatePackage(
            "Notepad++.Notepad++",
            "Notepad++",
            "8.7.8",
            "8.8.4"),

        CreatePackage(
            "Git.Git",
            "Git",
            "2.49.0",
            "2.51.0"),

        CreatePackage(
            "Microsoft.PowerToys",
            "Microsoft PowerToys",
            "0.92.1",
            "0.93.0"),

        CreatePackage(
            "Fake.VeryLongApplicationName",
            "An Application With An Extremely Long Name To Test AppDeck Layout",
            "12.4.1837",
            "13.0.2041")
    ];

    public Task<IReadOnlyList<PackageInfo>> GetAvailableUpdatesAsync()
    {
        IReadOnlyList<PackageInfo> result =
            _packages.ToArray();

        return Task.FromResult(result);
    }

    public async Task UpdatePackageAsync(
        string packageId,
        IProgress<PackageUpdateProgress>? progress = null)
    {
        progress?.Report(
            new PackageUpdateProgress(
                "Queued",
                0));

        await Task.Delay(500);

        for (var value = 0; value <= 100; value += 5)
        {
            progress?.Report(
                new PackageUpdateProgress(
                    $"Downloading {value}%",
                    value * 0.5));

            await Task.Delay(80);
        }

        await Task.Delay(300);

        for (var value = 0; value <= 100; value += 5)
        {
            progress?.Report(
                new PackageUpdateProgress(
                    $"Installing {value}%",
                    50 + value * 0.45));

            await Task.Delay(100);
        }

        progress?.Report(
            new PackageUpdateProgress(
                "Finishing...",
                98));

        await Task.Delay(600);

        progress?.Report(
            new PackageUpdateProgress(
                "Completed",
                100));

        await Task.Delay(300);

        _packages.RemoveAll(
            package =>
                string.Equals(
                    package.Id,
                    packageId,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static PackageInfo CreatePackage(
        string id,
        string name,
        string installedVersion,
        string availableVersion)
    {
        return new PackageInfo
        {
            Id = id,
            Name = name,
            InstalledVersion = installedVersion,
            AvailableVersion = availableVersion,
            Source = "winget"
        };
    }
}