using System;

namespace PCGet;

public static class PCGetIdentity
{
    public const string PackageId = "PCG.PCGet";

    public static bool IsPCGetPackage(string packageId)
    {
        return string.Equals(packageId, PackageId, StringComparison.OrdinalIgnoreCase);
    }
}