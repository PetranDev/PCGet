using System;

namespace PCGet.Services;

public static class PackageOperationErrorClassifier
{
    private const string RequiresAdministratorHResult = "0x80073D2B";
    private const string WinGetRequiresAdministratorHResult = "0x8A150019";
    private const string WinGetShellExecuteFailedHResult = "0x8A150006";
    private const string HttpNotFoundHResult = "0x80190194";

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

        if (IsHttpNotFound(errorMessage))
            return LocalizationService.GetString("Error_Http404");

        return errorMessage;
    }

    private static bool IsHttpNotFound(string errorMessage)
    {
        return
            errorMessage.Contains(HttpNotFoundHResult, StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("Not found (404)", StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("404 (Not Found)", StringComparison.OrdinalIgnoreCase);
    }
}
