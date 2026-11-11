using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PCGet.Services;

public sealed class ElevatedOperationService
{
    private const string WinGetArgumentsSeparator = "--winget-args";

    private readonly AppSettingsService _settingsService;

    public ElevatedOperationService(AppSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public Task InstallPackageAsync(string packageId)
    {
        return RunPackageOperationAsync(
            "--install",
            packageId,
            _settingsService.SilentPackageOperations,
            includeUnknown: false,
            _settingsService.InstallAdditionalArguments);
    }

    public Task UpdatePackageAsync(string packageId, string? additionalArguments = null)
    {
        return RunPackageOperationAsync(
            "--update",
            packageId,
            _settingsService.SilentPackageOperations,
            _settingsService.IncludeUnknownVersions,
            additionalArguments ?? _settingsService.UpdateAdditionalArguments);
    }

    public Task UninstallPackageAsync(string packageId, bool interactive = false)
    {
        var silent = !interactive && _settingsService.SilentPackageOperations;

        return RunPackageOperationAsync(
            "--uninstall",
            packageId,
            silent,
            includeUnknown: false,
            _settingsService.UninstallAdditionalArguments);
    }

    private static async Task RunPackageOperationAsync(string operation, string packageId, bool silent, bool includeUnknown, string? additionalArguments)
    {
        var helperPath = GetHelperPath();

        if (!File.Exists(helperPath))
            throw new FileNotFoundException(LocalizationService.GetString("Error_ElevatedHelperNotFound"), helperPath);

        var parsedArguments = ParseArguments(additionalArguments);

        // The elevated helper reports its log through this pipe rather than a file in %TEMP%.
        var logPipeName = $"PCGet.Elevated.{Guid.NewGuid():N}";
        // A buffer so the helper's log writes don't each have to wait for this side to read them.
        using var logPipe = new NamedPipeServerStream(logPipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, inBufferSize: 64 * 1024, outBufferSize: 0);

        var startInfo = new ProcessStartInfo
        {
            FileName = helperPath,
            UseShellExecute = true,
            Verb = "runas"
        };

        startInfo.ArgumentList.Add(operation);
        startInfo.ArgumentList.Add(packageId);
        startInfo.ArgumentList.Add(silent ? "--silent" : "--interactive");

        if (includeUnknown)
            startInfo.ArgumentList.Add("--include-unknown");

        startInfo.ArgumentList.Add("--log-pipe");
        startInfo.ArgumentList.Add(logPipeName);

        if (parsedArguments.Count > 0)
        {
            startInfo.ArgumentList.Add(WinGetArgumentsSeparator);

            foreach (var argument in parsedArguments)
                startInfo.ArgumentList.Add(argument);
        }

        // Start listening before the helper exists so its connection can't race ahead of us.
        using var stopListening = new CancellationTokenSource();
        var logTask = ReadHelperLogAsync(logPipe, stopListening.Token);
        int exitCode;

        try
        {
            using var process = Process.Start(startInfo);

            if (process is null)
                throw new InvalidOperationException(LocalizationService.GetString("Error_ElevatedHelperStart"));

            await process.WaitForExitAsync();
            exitCode = process.ExitCode;
        }
        finally
        {
            // Only matters if the helper never connected (UAC cancelled, or it exited before logging).
            stopListening.Cancel();
        }

        var log = await logTask;

        if (exitCode != 0)
            throw new InvalidOperationException(GetFailureMessage(exitCode, log));
    }

    private static async Task<string> ReadHelperLogAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        try
        {
            await pipe.WaitForConnectionAsync(cancellationToken);

            // Ends when the helper closes its end of the pipe, i.e. when it exits.
            using var reader = new StreamReader(pipe, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
            return await reader.ReadToEndAsync();
        }
        catch
        {
            // The log is best-effort: cancelled (helper never connected), broken pipe, or the pipe was
            // disposed because starting the helper failed (e.g. the UAC prompt was declined).
            return string.Empty;
        }
    }

    private static List<string> ParseArguments(string? commandLine)
    {
        var arguments = new List<string>();

        if (string.IsNullOrWhiteSpace(commandLine))
            return arguments;

        var current = new StringBuilder();
        var inQuotes = false;
        var quoteCharacter = '\0';

        for (var i = 0; i < commandLine.Length; i++)
        {
            var character = commandLine[i];

            if ((character == '"' || character == '\'') && (!inQuotes || character == quoteCharacter))
            {
                if (inQuotes)
                {
                    inQuotes = false;
                    quoteCharacter = '\0';
                }
                else
                {
                    inQuotes = true;
                    quoteCharacter = character;
                }

                continue;
            }

            if (char.IsWhiteSpace(character) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    arguments.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            if (character == '\\' && i + 1 < commandLine.Length && commandLine[i + 1] == quoteCharacter)
            {
                current.Append(commandLine[++i]);
                continue;
            }

            current.Append(character);
        }

        if (inQuotes)
            throw new ArgumentException(LocalizationService.GetString("Error_UnmatchedQuote"));

        if (current.Length > 0)
            arguments.Add(current.ToString());

        return arguments;
    }

    private static string GetHelperPath()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "PCGet.Elevated",
            "PCGet.Elevated.exe"));
    }

    private static string GetFailureMessage(int exitCode, string log)
    {
        return string.IsNullOrWhiteSpace(log)
            ? LocalizationService.Format("Error_ElevatedFailed", exitCode)
            : LocalizationService.Format("Error_ElevatedFailedWithLog", exitCode, log.TrimEnd());
    }
}
