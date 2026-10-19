using Microsoft.Win32;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace PCGet.Services;

public sealed class StartupService
{
    private const string StartupTaskId = "PCGetStartup";

    // Unpackaged (Inno Setup) installs use the classic per-user Run key instead of an MSIX StartupTask.
    // The installer's "Start with Windows" task writes the same value, so both stay in sync.
    public const string StartupArgument = "--startup";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupApprovedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string RunValueName = "PCGet";

    public async Task<StartupTaskState> GetStateAsync()
    {
        if (!AppIdentity.IsPackaged)
            return GetRunKeyState();

        var task = await StartupTask.GetAsync(StartupTaskId);
        return task.State;
    }

    public async Task<StartupTaskState> SetEnabledAsync(bool enabled)
    {
        if (!AppIdentity.IsPackaged)
            return SetRunKeyEnabled(enabled);

        var task = await StartupTask.GetAsync(StartupTaskId);

        if (!enabled)
        {
            if (task.State == StartupTaskState.Enabled)
                task.Disable();

            return task.State;
        }

        if (task.State == StartupTaskState.Disabled)
            return await task.RequestEnableAsync();

        return task.State;
    }

    public static void RemoveRunKey()
    {
        try
        {
            using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            runKey?.DeleteValue(RunValueName, throwOnMissingValue: false);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private static StartupTaskState GetRunKeyState()
    {
        try
        {
            using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);

            if (runKey?.GetValue(RunValueName) is not string)
                return StartupTaskState.Disabled;

            // Keep the command pointing at the current install location.
            runKey.SetValue(RunValueName, GetRunCommand());

            // Task Manager's "Disable" is stored here; an odd first byte means disabled by the user.
            using var approvedKey = Registry.CurrentUser.OpenSubKey(StartupApprovedKeyPath);

            if (approvedKey?.GetValue(RunValueName) is byte[] { Length: > 0 } approved && (approved[0] & 1) == 1)
                return StartupTaskState.DisabledByUser;

            return StartupTaskState.Enabled;
        }
        catch
        {
            return StartupTaskState.DisabledByPolicy;
        }
    }

    private static StartupTaskState SetRunKeyEnabled(bool enabled)
    {
        try
        {
            if (enabled)
            {
                using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
                runKey.SetValue(RunValueName, GetRunCommand());
            }
            else
            {
                RemoveRunKey();
            }
        }
        catch
        {
            // Fall through and report whatever state the registry is actually in.
        }

        return GetRunKeyState();
    }

    private static string GetRunCommand() => $"\"{AppIdentity.ExecutablePath}\" {StartupArgument}";
}
