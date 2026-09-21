using System;

namespace PCGet.Services;

public static class PackageOperationErrorClassifier
{
    private const string RequiresAdministratorHResult = "0x80073D2B";
    private const string WinGetRequiresAdministratorHResult = "0x8A150019";
    private const string WinGetShellExecuteFailedHResult = "0x8A150006";

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
}