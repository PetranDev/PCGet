using AppDeck.Models;
using Microsoft.Management.Deployment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using WindowsPackageManager.Interop;

namespace AppDeck.Services;

public sealed class WinGetService : IWinGetService
{
    public async Task<IReadOnlyList<PackageInfo>> GetAvailableUpdatesAsync()
    {
        var factory = CreateFactory();
        var packageManager = factory.CreatePackageManager();

        var compositeCatalog = CreateCompositeCatalog(
            factory,
            packageManager);

        var connectResult = compositeCatalog.Connect();

        if (connectResult.Status != ConnectResultStatus.Ok)
        {
            throw new InvalidOperationException(
                $"Unable to connect to WinGet composite catalog. " +
                $"Status: {connectResult.Status}");
        }

        var findOptions =
            factory.CreateFindPackagesOptions();

        var searchResult =
            await connectResult.PackageCatalog.FindPackagesAsync(
                findOptions);

        var result = new List<PackageInfo>();

        foreach (var match in searchResult.Matches.ToArray())
        {
            var package = match.CatalogPackage;

            if (!package.IsUpdateAvailable)
                continue;

            var installedVersion = package.InstalledVersion;
            var availableVersion = package.DefaultInstallVersion;

            if (installedVersion is null ||
                availableVersion is null)
            {
                continue;
            }

            result.Add(new PackageInfo
            {
                Id = package.Id ?? string.Empty,

                Name =
                    installedVersion.DisplayName ??
                    package.Name ??
                    package.Id ??
                    "Unknown",

                InstalledVersion =
                    installedVersion.Version ??
                    string.Empty,

                AvailableVersion =
                    availableVersion.Version ??
                    string.Empty,

                Source =
                    availableVersion.PackageCatalog?.Info?.Name ??
                    string.Empty
            });
        }

        return result
            .GroupBy(
                package => package.Id,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(package => package.Name)
            .ToList();
    }

    public async Task UpdatePackageAsync(
        string packageId,
        IProgress<PackageUpdateProgress>? progress = null)
    {
        progress?.Report(
            new PackageUpdateProgress(
                "Preparing...",
                0));

        var factory = CreateFactory();
        var packageManager = factory.CreatePackageManager();

        var compositeCatalog = CreateCompositeCatalog(
            factory,
            packageManager);

        var connectResult = compositeCatalog.Connect();

        if (connectResult.Status != ConnectResultStatus.Ok)
        {
            throw new InvalidOperationException(
                $"Unable to connect to WinGet composite catalog. " +
                $"Status: {connectResult.Status}");
        }

        var findOptions =
            factory.CreateFindPackagesOptions();

        var idFilter =
            factory.CreatePackageMatchFilter();

        idFilter.Field =
            PackageMatchField.Id;

        idFilter.Option =
            PackageFieldMatchOption.Equals;

        idFilter.Value =
            packageId;

        findOptions.Filters.Add(idFilter);

        var searchResult =
            await connectResult.PackageCatalog.FindPackagesAsync(
                findOptions);

        var package = searchResult
            .Matches
            .ToArray()
            .Select(match => match.CatalogPackage)
            .FirstOrDefault(candidate =>
                string.Equals(
                    candidate.Id,
                    packageId,
                    StringComparison.OrdinalIgnoreCase));

        if (package is null)
        {
            throw new InvalidOperationException(
                $"Package '{packageId}' could not be found.");
        }

        if (!package.IsUpdateAvailable)
        {
            throw new InvalidOperationException(
                $"Package '{packageId}' no longer has an update available.");
        }

        var installOptions =
            factory.CreateInstallOptions();

        installOptions.PackageInstallMode =
            PackageInstallMode.Silent;

        var operation =
            packageManager.UpgradePackageAsync(
                package,
                installOptions);

        operation.Progress =
            (_, installProgress) =>
            {
                var status = installProgress.State switch
                {
                    PackageInstallProgressState.Queued =>
                        "Queued",

                    PackageInstallProgressState.Downloading =>
                        $"Downloading {installProgress.DownloadProgress * 100:0}%",

                    PackageInstallProgressState.Installing =>
                        $"Installing {installProgress.InstallationProgress * 100:0}%",

                    PackageInstallProgressState.PostInstall =>
                        "Finishing...",

                    PackageInstallProgressState.Finished =>
                        "Finishing...",

                    _ =>
                        "Updating..."
                };

                var percent = installProgress.State switch
                {
                    PackageInstallProgressState.Downloading =>
                        installProgress.DownloadProgress * 50,

                    PackageInstallProgressState.Installing =>
                        50 +
                        (installProgress.InstallationProgress * 45),

                    PackageInstallProgressState.PostInstall =>
                        98,

                    PackageInstallProgressState.Finished =>
                        100,

                    _ =>
                        0
                };

                progress?.Report(
                    new PackageUpdateProgress(
                        status,
                        Math.Clamp(percent, 0, 100)));
            };

        var result =
            await operation;

        if (result.Status != InstallResultStatus.Ok)
        {
            var message =
                $"WinGet failed to update '{packageId}'. " +
                $"Status: {result.Status}.";

            if (result.ExtendedErrorCode is not null)
            {
                message +=
                    $" Error: {result.ExtendedErrorCode.Message}";
            }

            throw new InvalidOperationException(message);
        }

        progress?.Report(
            new PackageUpdateProgress(
                "Completed",
                100));
    }

    private static WindowsPackageManagerFactory CreateFactory()
    {
        using var identity =
            WindowsIdentity.GetCurrent();

        var principal =
            new WindowsPrincipal(identity);

        return principal.IsInRole(
            WindowsBuiltInRole.Administrator)
            ? new WindowsPackageManagerElevatedFactory()
            : new WindowsPackageManagerStandardFactory();
    }

    private static PackageCatalogReference CreateCompositeCatalog(
        WindowsPackageManagerFactory factory,
        PackageManager packageManager)
    {
        var compositeOptions =
            factory.CreateCreateCompositePackageCatalogOptions();

        foreach (var catalog in
                 packageManager.GetPackageCatalogs().ToArray())
        {
            compositeOptions.Catalogs.Add(catalog);
        }

        compositeOptions.CompositeSearchBehavior =
            CompositeSearchBehavior.LocalCatalogs;

        return packageManager.CreateCompositePackageCatalog(
            compositeOptions);
    }
}