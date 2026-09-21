using PCGet.Models;
using Microsoft.Management.Deployment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using WindowsPackageManager.Interop;

namespace PCGet.Services;

public sealed class WinGetService : IWinGetService
{
    private readonly AppSettingsService _settingsService;

    public WinGetService()
    {
        _settingsService = new AppSettingsService();
    }

    public async Task<IReadOnlyList<PackageInfo>> GetInstalledPackagesAsync()
    {
        var factory = CreateFactory();
        var packageManager = factory.CreatePackageManager();

        var compositeCatalog =
            CreateCompositeCatalog(
                factory,
                packageManager);

        var connectResult =
            compositeCatalog.Connect();

        if (connectResult.Status != ConnectResultStatus.Ok)
        {
            throw new InvalidOperationException(
                $"Unable to connect to WinGet composite catalog. Status: {connectResult.Status}");
        }

        var findOptions =
            factory.CreateFindPackagesOptions();

        var searchResult =
            await connectResult.PackageCatalog.FindPackagesAsync(
                findOptions);

        var result =
            new List<PackageInfo>();

        foreach (var match in searchResult.Matches.ToArray())
        {
            var package =
                match.CatalogPackage;

            var installedVersion =
                package.InstalledVersion;

            if (installedVersion is null)
                continue;

            result.Add(
                new PackageInfo
                {
                    Id =
                        package.Id ??
                        string.Empty,

                    Name =
                        installedVersion.DisplayName ??
                        package.Name ??
                        package.Id ??
                        "Unknown",

                    InstalledVersion =
                        installedVersion.Version ??
                        string.Empty,

                    AvailableVersion =
                        string.Empty,

                    Source =
                        installedVersion.PackageCatalog?.Info?.Name ??
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

    public async Task<IReadOnlyList<PackageInfo>> GetAvailableUpdatesAsync()
    {
        var factory = CreateFactory();
        var packageManager = factory.CreatePackageManager();

        var compositeCatalog =
            CreateCompositeCatalog(
                factory,
                packageManager);

        var connectResult =
            compositeCatalog.Connect();

        if (connectResult.Status != ConnectResultStatus.Ok)
        {
            throw new InvalidOperationException(
                $"Unable to connect to WinGet composite catalog. Status: {connectResult.Status}");
        }

        var findOptions =
            factory.CreateFindPackagesOptions();

        var searchResult =
            await connectResult.PackageCatalog.FindPackagesAsync(
                findOptions);

        var result =
            new List<PackageInfo>();

        foreach (var match in searchResult.Matches.ToArray())
        {
            var package =
                match.CatalogPackage;

            if (!package.IsUpdateAvailable)
                continue;

            var installedVersion =
                package.InstalledVersion;

            var availableVersion =
                package.DefaultInstallVersion;

            if (installedVersion is null ||
                availableVersion is null)
            {
                continue;
            }

            result.Add(
                new PackageInfo
                {
                    Id =
                        package.Id ??
                        string.Empty,

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

    public async Task<IReadOnlyList<DiscoverPackageInfo>> SearchPackagesAsync(
        string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var factory = CreateFactory();
        var packageManager = factory.CreatePackageManager();

        var result =
            new List<DiscoverPackageInfo>();

        foreach (var catalogReference in
                 packageManager.GetPackageCatalogs().ToArray())
        {
            var connectResult =
                catalogReference.Connect();

            if (connectResult.Status != ConnectResultStatus.Ok)
                continue;

            var findOptions =
                factory.CreateFindPackagesOptions();

            var searchFilter =
                factory.CreatePackageMatchFilter();

            searchFilter.Field =
                PackageMatchField.CatalogDefault;

            searchFilter.Value =
                query.Trim();

            findOptions.Selectors.Add(
                searchFilter);

            var searchResult =
                await connectResult.PackageCatalog.FindPackagesAsync(
                    findOptions);

            if (searchResult.Status != FindPackagesResultStatus.Ok)
                continue;

            foreach (var match in searchResult.Matches.ToArray())
            {
                var package =
                    match.CatalogPackage;

                var availableVersion =
                    package.DefaultInstallVersion;

                if (availableVersion is null)
                    continue;

                CatalogPackageMetadata? metadata = null;

                try
                {
                    metadata =
                        availableVersion.GetCatalogPackageMetadata();
                }
                catch
                {
                }

                var iconUrl =
                    metadata?
                        .Icons?
                        .ToArray()
                        .Select(icon => icon.Url)
                        .FirstOrDefault(url =>
                            !string.IsNullOrWhiteSpace(url)) ??
                    string.Empty;

                var tags =
                    metadata?.Tags is not null
                        ? string.Join(
                            ", ",
                            metadata.Tags.ToArray())
                        : string.Empty;

                result.Add(
                    new DiscoverPackageInfo
                    {
                        Id =
                            package.Id ??
                            string.Empty,

                        Name =
                            package.Name ??
                            package.Id ??
                            "Unknown",

                        Version =
                            availableVersion.Version ??
                            string.Empty,

                        Source =
                            availableVersion.PackageCatalog?.Info?.Name ??
                            string.Empty,

                        Publisher =
                            metadata?.Publisher ??
                            availableVersion.Publisher ??
                            string.Empty,

                        Description =
                            metadata?.Description ??
                            string.Empty,

                        ShortDescription =
                            metadata?.ShortDescription ??
                            string.Empty,

                        License =
                            metadata?.License ??
                            string.Empty,

                        PackageUrl =
                            metadata?.PackageUrl ??
                            string.Empty,

                        PublisherUrl =
                            metadata?.PublisherUrl ??
                            string.Empty,

                        LicenseUrl =
                            metadata?.LicenseUrl ??
                            string.Empty,

                        Tags =
                            tags,

                        IconUrl =
                            iconUrl,

                        IsInstalled =
                            package.InstalledVersion is not null
                    });
            }
        }

        return result
            .Where(package =>
                !string.IsNullOrWhiteSpace(package.Id))
            .GroupBy(
                package => package.Id,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(package => package.Name)
            .ToList();
    }

    public async Task InstallPackageAsync(
        string packageId,
        IProgress<PackageInstallProgress>? progress = null)
    {
        progress?.Report(
            new PackageInstallProgress(
                "Preparing...",
                0));

        var factory = CreateFactory();
        var packageManager = factory.CreatePackageManager();

        var package =
            await FindRemotePackageAsync(
                factory,
                packageManager,
                packageId);

        if (package is null)
        {
            throw new InvalidOperationException(
                $"Package '{packageId}' could not be found.");
        }

        if (package.InstalledVersion is not null)
        {
            throw new InvalidOperationException(
                $"Package '{packageId}' is already installed.");
        }

        var installOptions =
            factory.CreateInstallOptions();

        installOptions.PackageInstallMode =
            GetPackageInstallMode();

        var operation =
            packageManager.InstallPackageAsync(
                package,
                installOptions);

        operation.Progress =
            (_, installProgress) =>
            {
                var status =
                    installProgress.State switch
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
                            "Installing..."
                    };

                var percent =
                    installProgress.State switch
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
                    new PackageInstallProgress(
                        status,
                        Math.Clamp(
                            percent,
                            0,
                            100)));
            };

        var result =
            await operation;

        if (result.Status != InstallResultStatus.Ok)
        {
            var message =
                $"WinGet failed to install '{packageId}'. Status: {result.Status}.";

            if (result.ExtendedErrorCode is not null)
            {
                message +=
                    $" Error: {result.ExtendedErrorCode.Message}";
            }

            throw new InvalidOperationException(
                message);
        }

        progress?.Report(
            new PackageInstallProgress(
                "Completed",
                100));
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

        var compositeCatalog =
            CreateCompositeCatalog(
                factory,
                packageManager);

        var connectResult =
            compositeCatalog.Connect();

        if (connectResult.Status != ConnectResultStatus.Ok)
        {
            throw new InvalidOperationException(
                $"Unable to connect to WinGet composite catalog. Status: {connectResult.Status}");
        }

        var package =
            await FindPackageAsync(
                factory,
                connectResult,
                packageId);

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
            GetPackageInstallMode();

        var operation =
            packageManager.UpgradePackageAsync(
                package,
                installOptions);

        operation.Progress =
            (_, installProgress) =>
            {
                var status =
                    installProgress.State switch
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

                var percent =
                    installProgress.State switch
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
                        Math.Clamp(
                            percent,
                            0,
                            100)));
            };

        var result =
            await operation;

        if (result.Status != InstallResultStatus.Ok)
        {
            var message =
                $"WinGet failed to update '{packageId}'. Status: {result.Status}.";

            if (result.ExtendedErrorCode is not null)
            {
                message +=
                    $" Error: {result.ExtendedErrorCode.Message}";
            }

            throw new InvalidOperationException(
                message);
        }

        progress?.Report(
            new PackageUpdateProgress(
                "Completed",
                100));
    }

    public async Task UninstallPackageAsync(
        string packageId,
        IProgress<PackageUninstallProgress>? progress = null,
        bool interactive = false)
    {
        progress?.Report(
            new PackageUninstallProgress(
                "Preparing...",
                0));

        var factory = CreateFactory();
        var packageManager = factory.CreatePackageManager();

        var compositeCatalog =
            CreateCompositeCatalog(
                factory,
                packageManager);

        var connectResult =
            compositeCatalog.Connect();

        if (connectResult.Status != ConnectResultStatus.Ok)
        {
            throw new InvalidOperationException(
                $"Unable to connect to WinGet composite catalog. Status: {connectResult.Status}");
        }

        var package =
            await FindPackageAsync(
                factory,
                connectResult,
                packageId);

        if (package is null ||
            package.InstalledVersion is null)
        {
            throw new InvalidOperationException(
                $"Installed package '{packageId}' could not be found.");
        }

        var uninstallOptions =
            factory.CreateUninstallOptions();

        uninstallOptions.PackageUninstallMode =
            interactive
                ? PackageUninstallMode.Interactive
                : GetPackageUninstallMode();

        var operation =
            packageManager.UninstallPackageAsync(
                package,
                uninstallOptions);

        operation.Progress =
            (_, uninstallProgress) =>
            {
                var percent =
                    uninstallProgress.State switch
                    {
                        PackageUninstallProgressState.Queued =>
                            0,

                        PackageUninstallProgressState.Uninstalling =>
                            uninstallProgress.UninstallationProgress * 100,

                        PackageUninstallProgressState.PostUninstall =>
                            98,

                        PackageUninstallProgressState.Finished =>
                            100,

                        _ =>
                            0
                    };

                var status =
                    uninstallProgress.State switch
                    {
                        PackageUninstallProgressState.Queued =>
                            "Queued",

                        PackageUninstallProgressState.Uninstalling =>
                            $"Uninstalling {percent:0}%",

                        PackageUninstallProgressState.PostUninstall =>
                            "Finishing...",

                        PackageUninstallProgressState.Finished =>
                            "Finishing...",

                        _ =>
                            "Uninstalling..."
                    };

                progress?.Report(
                    new PackageUninstallProgress(
                        status,
                        Math.Clamp(
                            percent,
                            0,
                            100)));
            };

        var result =
            await operation;

        if (result.Status != UninstallResultStatus.Ok)
        {
            var message =
                $"WinGet failed to uninstall '{packageId}'. Status: {result.Status}.";

            if (result.ExtendedErrorCode is not null)
            {
                message +=
                    $" Error: {result.ExtendedErrorCode.Message}";
            }

            throw new InvalidOperationException(
                message);
        }

        progress?.Report(
            new PackageUninstallProgress(
                "Completed",
                100));
    }

    private PackageInstallMode GetPackageInstallMode()
    {
        return _settingsService.SilentPackageOperations
            ? PackageInstallMode.Silent
            : PackageInstallMode.Interactive;
    }

    private PackageUninstallMode GetPackageUninstallMode()
    {
        return _settingsService.SilentPackageOperations
            ? PackageUninstallMode.Silent
            : PackageUninstallMode.Interactive;
    }

    private static async Task<CatalogPackage?> FindRemotePackageAsync(
        WindowsPackageManagerFactory factory,
        Microsoft.Management.Deployment.PackageManager packageManager,
        string packageId)
    {
        foreach (var catalogReference in
                 packageManager.GetPackageCatalogs().ToArray())
        {
            var connectResult =
                catalogReference.Connect();

            if (connectResult.Status != ConnectResultStatus.Ok)
                continue;

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

            findOptions.Filters.Add(
                idFilter);

            var searchResult =
                await connectResult.PackageCatalog.FindPackagesAsync(
                    findOptions);

            if (searchResult.Status != FindPackagesResultStatus.Ok)
                continue;

            var package =
                searchResult
                    .Matches
                    .ToArray()
                    .Select(match => match.CatalogPackage)
                    .FirstOrDefault(
                        candidate =>
                            string.Equals(
                                candidate.Id,
                                packageId,
                                StringComparison.OrdinalIgnoreCase));

            if (package is not null)
                return package;
        }

        return null;
    }

    private static async Task<CatalogPackage?> FindPackageAsync(
        WindowsPackageManagerFactory factory,
        ConnectResult connectResult,
        string packageId)
    {
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

        findOptions.Filters.Add(
            idFilter);

        var searchResult =
            await connectResult.PackageCatalog.FindPackagesAsync(
                findOptions);

        return searchResult
            .Matches
            .ToArray()
            .Select(match => match.CatalogPackage)
            .FirstOrDefault(
                candidate =>
                    string.Equals(
                        candidate.Id,
                        packageId,
                        StringComparison.OrdinalIgnoreCase));
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
        Microsoft.Management.Deployment.PackageManager packageManager)
    {
        var compositeOptions =
            factory.CreateCreateCompositePackageCatalogOptions();

        foreach (var catalog in
                 packageManager.GetPackageCatalogs().ToArray())
        {
            compositeOptions.Catalogs.Add(
                catalog);
        }

        compositeOptions.CompositeSearchBehavior =
            CompositeSearchBehavior.LocalCatalogs;

        return packageManager.CreateCompositePackageCatalog(
            compositeOptions);
    }
}