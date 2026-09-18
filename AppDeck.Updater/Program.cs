using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace AppDeck.Updater;

public static class Program
{
    private const string AppDeckPackageId = "PCG.AppDeck";

    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length < 2 ||
                !int.TryParse(args[0], out var appDeckProcessId) ||
                string.IsNullOrWhiteSpace(args[1]))
                return 1;

            var appDeckExecutable = Path.GetFullPath(args[1]);

            await WaitForAppDeckToExitAsync(appDeckProcessId);

            var exitCode = await UpdateAppDeckAsync();

            if (exitCode != 0)
                return exitCode;

            if (!File.Exists(appDeckExecutable))
                return 2;

            Process.Start(new ProcessStartInfo
            {
                FileName = appDeckExecutable,
                UseShellExecute = true
            });

            return 0;
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
}