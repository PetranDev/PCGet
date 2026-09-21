using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
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
            _settingsService.InstallAdditionalArguments);
    }

    public Task UpdatePackageAsync(string packageId, string? additionalArguments = null)
    {
        return RunPackageOperationAsync(
            "--update",
            packageId,
            _settingsService.SilentPackageOperations,
            additionalArguments ?? _settingsService.UpdateAdditionalArguments);
    }

    public Task UninstallPackageAsync(string packageId, bool interactive = false)
    {
        var silent = !interactive && _settingsService.SilentPackageOperations;

        return RunPackageOperationAsync(
            "--uninstall",
            packageId,
            silent,
            _settingsService.UninstallAdditionalArguments);
    }

    private static async Task RunPackageOperationAsync(string operation, string packageId, bool silent, string? additionalArguments)
    {
        var helperPath = GetHelperPath();

        if (!File.Exists(helperPath))
            throw new FileNotFoundException("The PCGet elevated helper could not be found.", helperPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = helperPath,
            UseShellExecute = true,
            Verb = "runas"
        };

        startInfo.ArgumentList.Add(operation);
        startInfo.ArgumentList.Add(packageId);

        if (silent)
            startInfo.ArgumentList.Add("--silent");

        var parsedArguments = ParseArguments(additionalArguments);

        if (parsedArguments.Count > 0)
        {
            startInfo.ArgumentList.Add(WinGetArgumentsSeparator);

            foreach (var argument in parsedArguments)
                startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);

        if (process is null)
            throw new InvalidOperationException("Unable to start the elevated PCGet helper.");

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new InvalidOperationException(GetFailureMessage(process.ExitCode));
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
            throw new ArgumentException("Additional WinGet arguments contain an unmatched quote.");

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

    private static string GetFailureMessage(int exitCode)
    {
        var logPath = Path.Combine(Path.GetTempPath(), "PCGet.Elevated.log");

        if (File.Exists(logPath))
        {
            try
            {
                var log = File.ReadAllText(logPath);

                if (!string.IsNullOrWhiteSpace(log))
                    return $"The elevated package operation failed with exit code {exitCode}.{Environment.NewLine}{Environment.NewLine}{log}";
            }
            catch
            {
            }
        }

        return $"The elevated package operation failed with exit code {exitCode}.";
    }
}