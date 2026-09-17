using AppDeck.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AppDeck.Services;

public interface IWinGetService
{
    Task<IReadOnlyList<PackageInfo>> GetInstalledPackagesAsync();

    Task<IReadOnlyList<PackageInfo>> GetAvailableUpdatesAsync();

    Task<IReadOnlyList<DiscoverPackageInfo>> SearchPackagesAsync(
        string query);

    Task InstallPackageAsync(
        string packageId,
        IProgress<PackageInstallProgress>? progress = null);

    Task UpdatePackageAsync(
        string packageId,
        IProgress<PackageUpdateProgress>? progress = null);

    Task UninstallPackageAsync(
        string packageId,
        IProgress<PackageUninstallProgress>? progress = null,
        bool interactive = false);
}

public sealed record PackageInstallProgress(
    string Status,
    double Percent);

public sealed record PackageUpdateProgress(
    string Status,
    double Percent);

public sealed record PackageUninstallProgress(
    string Status,
    double Percent);