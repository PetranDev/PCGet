using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace AppDeck.Updater;

public static class Program
{
    private const string AppDeckPackageId = "PCG.AppDeck";

    public static async Task<int> Main(string[] args)
    {
        try
        {
            var dryRun = args.Any(arg => string.Equals(arg, "--dry-run", StringComparison.OrdinalIgnoreCase));
            var positionalArgs = args.Where(arg => !arg.StartsWith("--", StringComparison.Ordinal)).ToArray();

            if (positionalArgs.Length < 2 ||
                !int.TryParse(positionalArgs[0], out var appDeckProcessId) ||
                string.IsNullOrWhiteSpace(positionalArgs[1]))
                return 1;

            var appDeckAumid = positionalArgs[1];

            await WaitForAppDeckToExitAsync(appDeckProcessId);

            if (dryRun)
            {
                RestartAppDeck(appDeckAumid);
                return 0;
            }

            var exitCode = await UpdateAppDeckAsync();

            if (exitCode == 0)
            {
                RestartAppDeck(appDeckAumid);
                return 0;
            }

            RestartAppDeck(appDeckAumid);
            return exitCode;
        }
        catch
        {
            return 1;
        }
    }

    private static async Task WaitForAppDeckToExitAsync(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);

            if (process.HasExited)
                return;

            await process.WaitForExitAsync();
        }
        catch (ArgumentException)
        {
            // AppDeck has already exited.
        }
    }

    private static async Task<int> UpdateAppDeckAsync()
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "winget.exe",
            Arguments = $"upgrade --id {AppDeckPackageId} --exact --silent --accept-source-agreements --accept-package-agreements",
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (process is null)
            return 1;

        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    private static void RestartAppDeck(string appDeckAumid)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"shell:AppsFolder\\{appDeckAumid}",
            UseShellExecute = true
        });
    }
}