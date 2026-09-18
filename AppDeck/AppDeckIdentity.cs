using System;

namespace AppDeck;

public static class AppDeckIdentity
{
    public const string PackageId = "PCG.AppDeck";

    public static bool IsAppDeckPackage(string packageId)
    {
        return string.Equals(packageId, PackageId, StringComparison.OrdinalIgnoreCase);
    }
}