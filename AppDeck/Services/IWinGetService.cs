using AppDeck.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AppDeck.Services;

public interface IWinGetService
{
    Task<IReadOnlyList<PackageInfo>> GetInstalledPackagesAsync();

    Task<IReadOnlyList<PackageInfo>> GetAvailableUpdatesAsync();

    Task UpdatePackageAsync(
        string packageId,
        IProgress<PackageUpdateProgress>? progress = null);
}

public sealed record PackageUpdateProgress(
    string Status,
    double Percent);