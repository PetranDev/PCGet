using System.Diagnostics;
using System.Security.Principal;
using System.Text;

return await MainAsync(args);

static async Task<int> MainAsync(string[] args)
{
    try
    {
        ClearLog();

        Log($"Started: {DateTime.Now:O}");
        Log($"Administrator: {IsAdministrator()}");
        Log($"Arguments: {string.Join(" | ", args)}");

        if (!IsAdministrator())
        {
            Log(
                "ERROR: AppDeck.Elevated is not running as Administrator.");

            return 2;
        }

        if (args.Length == 0)
        {
            Log(
                "ERROR: No command was specified.");

            return 3;
        }

        return args[0].ToLowerInvariant() switch
        {
            "--test" =>
                TestElevation(),

            "--query" =>
                await QueryPackageAsync(args),

            "--install" =>
                await InstallPackageAsync(args),

            "--update" =>
                await UpdatePackageAsync(args),

            "--uninstall" =>
                await UninstallPackageAsync(args),

            _ =>
                UnknownCommand(args[0])
        };
    }
    catch (Exception ex)
    {
        Log(
            "UNHANDLED EXCEPTION:");

        Log(
            ex.ToString());

        return 1;
    }
}

static bool IsAdministrator()
{
    using var identity =
        WindowsIdentity.GetCurrent();

    var principal =
        new WindowsPrincipal(identity);

    return principal.IsInRole(
        WindowsBuiltInRole.Administrator);
}

static int TestElevation()
{
    Log(
        "Elevation test succeeded.");

    return 0;
}

static async Task<int> QueryPackageAsync(
    string[] args)
{
    if (!TryGetPackageId(
            args,
            out var packageId))
    {
        Log(
            "ERROR: A package ID is required.");

        return 4;
    }

    Log(
        $"Looking up package: {packageId}");

    var result =
        await RunWinGetAsync(
            [
                "list",
                "--id",
                packageId,
                "--exact",
                "--accept-source-agreements",
                "--disable-interactivity"
            ]);

    LogProcessResult(
        result);

    if (result.ExitCode != 0)
    {
        Log(
            "ERROR: WinGet query failed.");

        return 5;
    }

    Log(
        "Query completed successfully.");

    return 0;
}

static async Task<int> InstallPackageAsync(
    string[] args)
{
    if (!TryGetPackageId(
            args,
            out var packageId))
    {
        Log(
            "ERROR: A package ID is required.");

        return 4;
    }

    var silent =
        HasArgument(
            args,
            "--silent");

    var dryRun =
        HasArgument(
            args,
            "--dry-run");

    Log(
        $"Installing package: {packageId}");

    Log(
        $"Silent: {silent}");

    Log(
        $"Dry run: {dryRun}");

    var wingetArguments =
        new List<string>
        {
            "install",
            "--id",
            packageId,
            "--exact",
            "--accept-package-agreements",
            "--accept-source-agreements",
            "--disable-interactivity"
        };

    if (silent)
    {
        wingetArguments.Add(
            "--silent");
    }

    if (dryRun)
    {
        LogDryRun(
            wingetArguments);

        return 0;
    }

    var result =
        await RunWinGetAsync(
            wingetArguments);

    LogProcessResult(
        result);

    if (result.ExitCode != 0)
    {
        Log(
            "ERROR: WinGet install failed.");

        return 9;
    }

    Log(
        "Install completed successfully.");

    return 0;
}

static async Task<int> UpdatePackageAsync(
    string[] args)
{
    if (!TryGetPackageId(
            args,
            out var packageId))
    {
        Log(
            "ERROR: A package ID is required.");

        return 4;
    }

    var silent =
        HasArgument(
            args,
            "--silent");

    var dryRun =
        HasArgument(
            args,
            "--dry-run");

    Log(
        $"Updating package: {packageId}");

    Log(
        $"Silent: {silent}");

    Log(
        $"Dry run: {dryRun}");

    var wingetArguments =
        new List<string>
        {
            "upgrade",
            "--id",
            packageId,
            "--exact",
            "--accept-package-agreements",
            "--accept-source-agreements",
            "--disable-interactivity"
        };

    if (silent)
    {
        wingetArguments.Add(
            "--silent");
    }

    if (dryRun)
    {
        LogDryRun(
            wingetArguments);

        return 0;
    }

    var result =
        await RunWinGetAsync(
            wingetArguments);

    LogProcessResult(
        result);

    if (result.ExitCode != 0)
    {
        Log(
            "ERROR: WinGet update failed.");

        return 10;
    }

    Log(
        "Update completed successfully.");

    return 0;
}

static async Task<int> UninstallPackageAsync(
    string[] args)
{
    if (!TryGetPackageId(
            args,
            out var packageId))
    {
        Log(
            "ERROR: A package ID is required.");

        return 4;
    }

    var silent =
        HasArgument(
            args,
            "--silent");

    var dryRun =
        HasArgument(
            args,
            "--dry-run");

    Log(
        $"Uninstalling package: {packageId}");

    Log(
        $"Silent: {silent}");

    Log(
        $"Dry run: {dryRun}");

    var wingetArguments =
        new List<string>
        {
            "uninstall",
            "--id",
            packageId,
            "--exact",
            "--accept-source-agreements",
            "--disable-interactivity"
        };

    if (silent)
    {
        wingetArguments.Add(
            "--silent");
    }

    if (dryRun)
    {
        LogDryRun(
            wingetArguments);

        return 0;
    }

    var result =
        await RunWinGetAsync(
            wingetArguments);

    LogProcessResult(
        result);

    if (result.ExitCode != 0)
    {
        Log(
            "ERROR: WinGet uninstall failed.");

        return 11;
    }

    Log(
        "Uninstall completed successfully.");

    return 0;
}

static void LogDryRun(
    IEnumerable<string> arguments)
{
    var argumentList =
        arguments.ToArray();

    Log(
        $"DRY RUN - would execute: winget.exe {string.Join(" ", argumentList.Select(FormatArgumentForLog))}");

    Log(
        "Dry run completed successfully. No package operation was performed.");
}

static bool TryGetPackageId(
    string[] args,
    out string packageId)
{
    packageId =
        string.Empty;

    if (args.Length < 2 ||
        string.IsNullOrWhiteSpace(
            args[1]))
    {
        return false;
    }

    packageId =
        args[1];

    return true;
}

static bool HasArgument(
    string[] args,
    string argument)
{
    return args.Any(
        value =>
            string.Equals(
                value,
                argument,
                StringComparison.OrdinalIgnoreCase));
}

static async Task<ProcessResult> RunWinGetAsync(
    IEnumerable<string> arguments)
{
    var argumentList =
        arguments.ToArray();

    Log(
        $"Executing: winget.exe {string.Join(" ", argumentList.Select(FormatArgumentForLog))}");

    var startInfo =
        new ProcessStartInfo
        {
            FileName =
                "winget.exe",

            UseShellExecute =
                false,

            CreateNoWindow =
                true,

            RedirectStandardOutput =
                true,

            RedirectStandardError =
                true,

            StandardOutputEncoding =
                Encoding.UTF8,

            StandardErrorEncoding =
                Encoding.UTF8
        };

    foreach (var argument in argumentList)
    {
        startInfo.ArgumentList.Add(
            argument);
    }

    using var process =
        new Process
        {
            StartInfo =
                startInfo
        };

    process.Start();

    var standardOutputTask =
        process.StandardOutput.ReadToEndAsync();

    var standardErrorTask =
        process.StandardError.ReadToEndAsync();

    await process.WaitForExitAsync();

    var standardOutput =
        await standardOutputTask;

    var standardError =
        await standardErrorTask;

    return new ProcessResult(
        process.ExitCode,
        standardOutput,
        standardError);
}

static void LogProcessResult(
    ProcessResult result)
{
    Log(
        $"WinGet exit code: {result.ExitCode}");

    if (!string.IsNullOrWhiteSpace(
            result.StandardOutput))
    {
        Log(
            "STANDARD OUTPUT:");

        Log(
            result.StandardOutput);
    }

    if (!string.IsNullOrWhiteSpace(
            result.StandardError))
    {
        Log(
            "STANDARD ERROR:");

        Log(
            result.StandardError);
    }
}

static string FormatArgumentForLog(
    string argument)
{
    if (!argument.Contains(' '))
        return argument;

    return
        $"\"{argument}\"";
}

static int UnknownCommand(
    string command)
{
    Log(
        $"ERROR: Unknown command: {command}");

    return 7;
}

static string GetLogPath()
{
    return Path.Combine(
        Path.GetTempPath(),
        "AppDeck.Elevated.log");
}

static void ClearLog()
{
    var path =
        GetLogPath();

    if (File.Exists(path))
    {
        File.Delete(path);
    }
}

static void Log(
    string message)
{
    File.AppendAllText(
        GetLogPath(),
        message +
        Environment.NewLine);
}

internal sealed record ProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);