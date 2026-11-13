using System;

namespace PCGet.Services;

public static class PackageOperationErrorClassifier
{
    private const string RequiresAdministratorHResult = "0x80073D2B";
    private const string WinGetRequiresAdministratorHResult = "0x8A150019";
    private const string WinGetShellExecuteFailedHResult = "0x8A150006";
    private const string HttpNotFoundHResult = "0x80190194";

    // APPINSTALLER_CLI_ERROR_INSTALLER_HASH_MISMATCH: the download doesn't match the manifest's SHA-256.
    // The COM API reports it as an HRESULT; the elevated helper's log has winget's decimal exit code and text.
    private const string InstallerHashMismatchHResult = "0x8A150011";
    private const string InstallerHashMismatchExitCode = "-1978335215";

    public static bool RequiresElevation(string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            return false;

        return
            errorMessage.Contains(RequiresAdministratorHResult, StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains(WinGetRequiresAdministratorHResult, StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains(WinGetShellExecuteFailedHResult, StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("administrator privileges are required", StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("requires administrator privileges", StringComparison.OrdinalIgnoreCase);
    }

    public static string GetUserFriendlyMessage(string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            return LocalizationService.GetString("Error_PackageOperationFailed");

        if (IsInstallerHashMismatch(errorMessage))
            return LocalizationService.GetString("Error_InstallerHashMismatch");

        if (IsHttpNotFound(errorMessage))
            return LocalizationService.GetString("Error_Http404");

        return errorMessage;
    }

    // Failures caused by the package's WinGet entry or its download, which running the same operation
    // as administrator cannot fix (WinGet never lets an administrator skip the installer hash check).
    public static bool ElevationCannotHelp(string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            return false;

        return IsInstallerHashMismatch(errorMessage) || IsHttpNotFound(errorMessage);
    }

    private static bool IsInstallerHashMismatch(string errorMessage)
    {
        return
            errorMessage.Contains(InstallerHashMismatchHResult, StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains(InstallerHashMismatchExitCode, StringComparison.Ordinal) ||
            errorMessage.Contains("Installer hash does not match", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHttpNotFound(string errorMessage)
    {
        return
            errorMessage.Contains(HttpNotFoundHResult, StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("Not found (404)", StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("404 (Not Found)", StringComparison.OrdinalIgnoreCase);
    }
}
