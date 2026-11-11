using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;

return await MainAsync(args);

static async Task<int> MainAsync(string[] args)
{
    try
    {
        // The log goes to a pipe PCGet created for this run, never to a file: this process runs as
        // administrator and must not write to paths the unelevated user controls (such as %TEMP%).
        HelperLog.Connect(GetControlArgumentValue(args, "--log-pipe"));

        Log($"Started: {DateTime.Now:O}");
        Log($"Administrator: {IsAdministrator()}");
        Log($"Arguments: {string.Join(" | ", args)}");

        if (!IsAdministrator())
        {
            Log("ERROR: PCGet.Elevated is not running as Administrator.");
            return 2;
        }

        if (args.Length == 0)
        {
            Log("ERROR: No command was specified.");
            return 3;
        }

        return args[0].ToLowerInvariant() switch
        {
            "--test" => TestElevation(),
            "--query" => await QueryPackageAsync(args),
            "--install" => await InstallPackageAsync(args),
            "--update" => await UpdatePackageAsync(args),
            "--uninstall" => await UninstallPackageAsync(args),
            _ => UnknownCommand(args[0])
        };
    }
    catch (Exception ex)
    {
        Log("UNHANDLED EXCEPTION:");
        Log(ex.ToString());
        return 1;
    }
    finally
    {
        HelperLog.Close();
    }
}

static bool IsAdministrator()
{
    using var identity = WindowsIdentity.GetCurrent();
    var principal = new WindowsPrincipal(identity);
    return principal.IsInRole(WindowsBuiltInRole.Administrator);
}

static int TestElevation()
{
    Log("Elevation test succeeded.");
    return 0;
}

static async Task<int> QueryPackageAsync(string[] args)
{
    if (!TryGetPackageId(args, out var packageId))
    {
        Log("ERROR: A package ID is required.");
        return 4;
    }

    Log($"Looking up package: {packageId}");

    var result = await RunWinGetAsync(
    [
        "list",
        "--id",
        packageId,
        "--exact",
        "--accept-source-agreements",
        "--disable-interactivity"
    ]);

    LogProcessResult(result);

    if (result.ExitCode != 0)
    {
        Log("ERROR: WinGet query failed.");
        return 5;
    }

    Log("Query completed successfully.");
    return 0;
}

static async Task<int> InstallPackageAsync(string[] args)
{
    if (!TryGetPackageId(args, out var packageId))
    {
        Log("ERROR: A package ID is required.");
        return 4;
    }

    var dryRun = HasControlArgument(args, "--dry-run");

    Log($"Installing package: {packageId}");
    Log($"Dry run: {dryRun}");

    var wingetArguments = new List<string>
    {
        "install",
        "--id",
        packageId,
        "--exact",
        "--accept-package-agreements",
        "--accept-source-agreements",
        "--disable-interactivity"
    };

    AddInstallerMode(args, wingetArguments);
    AddAdditionalWinGetArguments(args, wingetArguments);

    if (dryRun)
    {
        LogDryRun(wingetArguments);
        return 0;
    }

    var result = await RunWinGetAsync(wingetArguments);
    LogProcessResult(result);

    if (result.ExitCode != 0)
    {
        Log("ERROR: WinGet install failed.");
        return 9;
    }

    Log("Install completed successfully.");
    return 0;
}

static async Task<int> UpdatePackageAsync(string[] args)
{
    if (!TryGetPackageId(args, out var packageId))
    {
        Log("ERROR: A package ID is required.");
        return 4;
    }

    var dryRun = HasControlArgument(args, "--dry-run");

    Log($"Updating package: {packageId}");
    Log($"Dry run: {dryRun}");

    var wingetArguments = new List<string>
    {
        "upgrade",
        "--id",
        packageId,
        "--exact",
        "--accept-package-agreements",
        "--accept-source-agreements",
        "--disable-interactivity"
    };

    AddIncludeUnknown(args, wingetArguments);
    AddInstallerMode(args, wingetArguments);
    AddAdditionalWinGetArguments(args, wingetArguments);

    if (dryRun)
    {
        LogDryRun(wingetArguments);
        return 0;
    }

    var result = await RunWinGetAsync(wingetArguments);
    LogProcessResult(result);

    if (result.ExitCode != 0)
    {
        Log("ERROR: WinGet update failed.");
        return 10;
    }

    Log("Update completed successfully.");
    return 0;
}

static async Task<int> UninstallPackageAsync(string[] args)
{
    if (!TryGetPackageId(args, out var packageId))
    {
        Log("ERROR: A package ID is required.");
        return 4;
    }

    var dryRun = HasControlArgument(args, "--dry-run");

    Log($"Uninstalling package: {packageId}");
    Log($"Dry run: {dryRun}");

    var wingetArguments = new List<string>
    {
        "uninstall",
        "--id",
        packageId,
        "--exact",
        "--accept-source-agreements",
        "--disable-interactivity"
    };

    AddInstallerMode(args, wingetArguments);
    AddAdditionalWinGetArguments(args, wingetArguments);

    if (dryRun)
    {
        LogDryRun(wingetArguments);
        return 0;
    }

    var result = await RunWinGetAsync(wingetArguments);
    LogProcessResult(result);

    if (result.ExitCode != 0)
    {
        Log("ERROR: WinGet uninstall failed.");
        return 11;
    }

    Log("Uninstall completed successfully.");
    return 0;
}

// PCGet always says which installer mode it wants. Without --silent or --interactive WinGet falls back
// to silent-with-progress, which would ignore a user who asked for the interactive installer.
static void AddInstallerMode(string[] args, List<string> wingetArguments)
{
    var silent = HasControlArgument(args, "--silent");
    var interactive = !silent && HasControlArgument(args, "--interactive");

    Log($"Silent: {silent}");
    Log($"Interactive: {interactive}");

    if (silent)
        wingetArguments.Add("--silent");
    else if (interactive)
        wingetArguments.Add("--interactive");
}

// WinGet refuses to upgrade a package whose installed version is unknown unless --include-unknown is
// given. PCGet passes it when the user has opted in to listing those packages.
static void AddIncludeUnknown(string[] args, List<string> wingetArguments)
{
    var includeUnknown = HasControlArgument(args, "--include-unknown") &&
                         !HasAdditionalWinGetArgument(args, "--include-unknown", "--unknown", "-u");

    Log($"Include unknown: {includeUnknown}");

    if (includeUnknown)
        wingetArguments.Add("--include-unknown");
}

static bool HasAdditionalWinGetArgument(string[] args, params string[] names)
{
    var separatorIndex = Array.FindIndex(args, argument =>
        string.Equals(argument, "--winget-args", StringComparison.OrdinalIgnoreCase));

    if (separatorIndex < 0)
        return false;

    return args
        .Skip(separatorIndex + 1)
        .Any(argument => names.Any(name => string.Equals(argument, name, StringComparison.OrdinalIgnoreCase)));
}

static void AddAdditionalWinGetArguments(string[] args, List<string> wingetArguments)
{
    var separatorIndex = Array.FindIndex(args, argument =>
        string.Equals(argument, "--winget-args", StringComparison.OrdinalIgnoreCase));

    if (separatorIndex < 0 || separatorIndex == args.Length - 1)
        return;

    var additionalArguments = args.Skip(separatorIndex + 1).ToArray();

    Log($"Additional WinGet arguments: {string.Join(" ", additionalArguments.Select(FormatArgumentForLog))}");

    wingetArguments.AddRange(additionalArguments);
}

static void LogDryRun(IEnumerable<string> arguments)
{
    var argumentList = arguments.ToArray();

    Log($"DRY RUN - would execute: winget.exe {string.Join(" ", argumentList.Select(FormatArgumentForLog))}");
    Log("Dry run completed successfully. No package operation was performed.");
}

static bool TryGetPackageId(string[] args, out string packageId)
{
    packageId = string.Empty;

    if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
        return false;

    packageId = args[1];
    return true;
}

static bool HasControlArgument(string[] args, string argument)
{
    var separatorIndex = Array.FindIndex(args, value =>
        string.Equals(value, "--winget-args", StringComparison.OrdinalIgnoreCase));

    var endIndex = separatorIndex >= 0 ? separatorIndex : args.Length;

    for (var i = 0; i < endIndex; i++)
    {
        if (string.Equals(args[i], argument, StringComparison.OrdinalIgnoreCase))
            return true;
    }

    return false;
}

static string? GetControlArgumentValue(string[] args, string argument)
{
    var separatorIndex = Array.FindIndex(args, value =>
        string.Equals(value, "--winget-args", StringComparison.OrdinalIgnoreCase));

    var endIndex = separatorIndex >= 0 ? separatorIndex : args.Length;

    for (var i = 0; i < endIndex - 1; i++)
    {
        if (string.Equals(args[i], argument, StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    }

    return null;
}

static async Task<ProcessResult> RunWinGetAsync(IEnumerable<string> arguments)
{
    var argumentList = arguments.ToArray();

    Log($"Executing: winget.exe {string.Join(" ", argumentList.Select(FormatArgumentForLog))}");

    var startInfo = new ProcessStartInfo
    {
        FileName = "winget.exe",
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8
    };

    foreach (var argument in argumentList)
        startInfo.ArgumentList.Add(argument);

    using var process = new Process
    {
        StartInfo = startInfo
    };

    process.Start();

    var standardOutputTask = process.StandardOutput.ReadToEndAsync();
    var standardErrorTask = process.StandardError.ReadToEndAsync();

    await process.WaitForExitAsync();

    var standardOutput = await standardOutputTask;
    var standardError = await standardErrorTask;

    return new ProcessResult(
        process.ExitCode,
        standardOutput,
        standardError);
}

static void LogProcessResult(ProcessResult result)
{
    Log($"WinGet exit code: {result.ExitCode}");

    if (!string.IsNullOrWhiteSpace(result.StandardOutput))
    {
        Log("STANDARD OUTPUT:");
        Log(result.StandardOutput);
    }

    if (!string.IsNullOrWhiteSpace(result.StandardError))
    {
        Log("STANDARD ERROR:");
        Log(result.StandardError);
    }
}

static string FormatArgumentForLog(string argument)
{
    if (!argument.Contains(' '))
        return argument;

    return $"\"{argument}\"";
}

static int UnknownCommand(string command)
{
    Log($"ERROR: Unknown command: {command}");
    return 7;
}

static void Log(string message)
{
    HelperLog.Write(message + Environment.NewLine);
}

internal sealed record ProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);

/// <summary>
/// Sends the helper's log to the named pipe PCGet created for this operation. Each operation gets its
/// own randomly named pipe, so concurrent elevated operations can't mix up their logs. Logging is
/// best-effort: if PCGet isn't listening, the operation still runs.
/// </summary>
internal static class HelperLog
{
    private static readonly Regex PipeNamePattern = new("^PCGet\\.Elevated\\.[0-9a-f]{32}$", RegexOptions.CultureInvariant);
    private static StreamWriter? _writer;

    public static void Connect(string? pipeName)
    {
        if (pipeName is null || !PipeNamePattern.IsMatch(pipeName))
            return;

        try
        {
            // Identification only: PCGet runs unelevated and must not be able to impersonate this process.
            var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.None, TokenImpersonationLevel.Identification);
            pipe.Connect(5000);
            _writer = new StreamWriter(pipe, new UTF8Encoding(false)) { AutoFlush = true };
        }
        catch
        {
            _writer = null;
        }
    }

    public static void Write(string text)
    {
        if (_writer is null)
            return;

        try
        {
            _writer.Write(text);
        }
        catch
        {
            // PCGet stopped listening; carry on without a log.
            Close();
        }
    }

    public static void Close()
    {
        try
        {
            _writer?.Dispose();
        }
        catch
        {
        }

        _writer = null;
    }
}