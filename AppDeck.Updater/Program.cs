using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
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
                WriteUpdateResult(true, 0);
                RestartAppDeck(appDeckAumid);
                return 0;
            }

            var exitCode = await UpdateAppDeckAsync();

            WriteUpdateResult(exitCode == 0, exitCode);
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

    private static void WriteUpdateResult(bool succeeded, int exitCode)
    {
        var directory = Path.Combine(Path.GetTempPath(), "AppDeck");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, "SelfUpdateResult.json");

        var result = new SelfUpdateResult
        {
            Succeeded = succeeded,
            ExitCode = exitCode,
            CompletedAtUtc = DateTimeOffset.UtcNow
        };

        File.WriteAllText(path, JsonSerializer.Serialize(result));
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

    private sealed class SelfUpdateResult
    {
        public bool Succeeded { get; set; }
        public int ExitCode { get; set; }
        public DateTimeOffset CompletedAtUtc { get; set; }
    }
}