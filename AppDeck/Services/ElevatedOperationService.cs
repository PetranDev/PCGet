using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace AppDeck.Services;

public sealed class ElevatedOperationService
{
    private readonly AppSettingsService _settingsService;

    public ElevatedOperationService(
        AppSettingsService settingsService)
    {
        _settingsService =
            settingsService;
    }

    public async Task UpdatePackageAsync(
        string packageId)
    {
        var helperPath =
            GetHelperPath();

        if (!File.Exists(helperPath))
        {
            throw new FileNotFoundException(
                "The AppDeck elevated helper could not be found.",
                helperPath);
        }

        var startInfo =
            new ProcessStartInfo
            {
                FileName = helperPath,
                UseShellExecute = true,
                Verb = "runas"
            };

        startInfo.ArgumentList.Add(
            "--update");

        startInfo.ArgumentList.Add(
            packageId);

        if (_settingsService.SilentPackageOperations)
        {
            startInfo.ArgumentList.Add(
                "--silent");
        }

        using var process =
            Process.Start(startInfo);

        if (process is null)
        {
            throw new InvalidOperationException(
                "Unable to start the elevated AppDeck helper.");
        }

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                GetFailureMessage(
                    process.ExitCode));
        }
    }

    private static string GetHelperPath()
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            "Elevated",
            "AppDeck.Elevated.exe");
    }

    private static string GetFailureMessage(
        int exitCode)
    {
        var logPath =
            Path.Combine(
                Path.GetTempPath(),
                "AppDeck.Elevated.log");

        if (File.Exists(logPath))
        {
            try
            {
                var log =
                    File.ReadAllText(logPath);

                if (!string.IsNullOrWhiteSpace(log))
                {
                    return
                        $"The elevated update failed with exit code {exitCode}.{Environment.NewLine}{Environment.NewLine}{log}";
                }
            }
            catch
            {
            }
        }

        return
            $"The elevated update failed with exit code {exitCode}.";
    }
}