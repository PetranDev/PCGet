using System;

namespace AppDeck.Services;

public static class PackageOperationErrorClassifier
{
    private const string RequiresAdministratorHResult =
        "0x80073D2B";

    public static bool RequiresElevation(
        string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(
                errorMessage))
        {
            return false;
        }

        return
            errorMessage.Contains(
                RequiresAdministratorHResult,
                StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains(
                "administrator privileges are required",
                StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains(
                "requires administrator privileges",
                StringComparison.OrdinalIgnoreCase);
    }
}