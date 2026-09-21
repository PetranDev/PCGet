using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PCGet.Updater;

public static class Program
{
    private const string PCGetPackageId = "PCG.PCGet";

    public static async Task<int> Main(string[] args)
    {
        string? pcGetAumid = null;

        try
        {
            var dryRun = args.Any(arg => string.Equals(arg, "--dry-run", StringComparison.OrdinalIgnoreCase));
            var simulateFailure = args.Any(arg => string.Equals(arg, "--simulate-failure", StringComparison.OrdinalIgnoreCase));
            var positionalArgs = args.Where(arg => !arg.StartsWith("--", StringComparison.Ordinal)).ToArray();

            if (positionalArgs.Length < 2 ||
                !int.TryParse(positionalArgs[0], out var pcGetProcessId) ||
                string.IsNullOrWhiteSpace(positionalArgs[1]))
                return 1;

            pcGetAumid = positionalArgs[1];

            await WaitForPCGetToExitAsync(pcGetProcessId);

            if (simulateFailure)
                throw new InvalidOperationException("Simulated self-update failure.");

            if (dryRun)
            {
                WriteUpdateResult(true, 0);
                RestartPCGet(pcGetAumid);
                return 0;
            }

            var exitCode = await UpdatePCGetAsync();

            WriteUpdateResult(exitCode == 0, exitCode);
            RestartPCGet(pcGetAumid);

            return exitCode;
        }
        catch
        {
            if (string.IsNullOrWhiteSpace(pcGetAumid))
                return 1;

            try
            {
                WriteUpdateResult(false, 1);
            }
            catch
            {
                // Failure to write the result must not prevent PCGet from restarting.
            }

            try
            {
                RestartPCGet(pcGetAumid);
            }
            catch
            {
                // Nothing else can be done if package activation itself fails.
            }

            return 1;
        }
    }

    private static async Task WaitForPCGetToExitAsync(int processId)
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
            // PCGet has already exited.
        }
    }

    private static async Task<int> UpdatePCGetAsync()
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "winget.exe",
            Arguments = $"upgrade --id {PCGetPackageId} --exact --silent --accept-source-agreements --accept-package-agreements",
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
        var directory = Path.Combine(Path.GetTempPath(), "PCGet");
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

    private static void RestartPCGet(string pcGetAumid)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"shell:AppsFolder\\{pcGetAumid}",
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