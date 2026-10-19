using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PCGet.Services;

/// <summary>
/// Tells PCGet whether it is running with MSIX package identity or as a classic
/// (Inno Setup) install, so identity-only APIs can fall back gracefully.
/// </summary>
public static class AppIdentity
{
    private const int AppModelErrorNoPackage = 15700;

    private static readonly Lazy<bool> IsPackagedLazy = new(DetectPackaged);

    public static bool IsPackaged => IsPackagedLazy.Value;

    public static string ExecutablePath =>
        Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "PCGet.exe");

    public static Version GetVersion()
    {
        if (IsPackaged)
        {
            var version = Windows.ApplicationModel.Package.Current.Id.Version;
            return new Version(version.Major, version.Minor, version.Build, version.Revision);
        }

        var assemblyVersion = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0, 0);

        return new Version(
            assemblyVersion.Major,
            assemblyVersion.Minor,
            Math.Max(assemblyVersion.Build, 0),
            Math.Max(assemblyVersion.Revision, 0));
    }

    private static bool DetectPackaged()
    {
        try
        {
            uint length = 0;
            return GetCurrentPackageFullName(ref length, IntPtr.Zero) != AppModelErrorNoPackage;
        }
        catch
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, IntPtr packageFullName);
}
