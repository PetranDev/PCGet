using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppNotifications;
using PCGet.Services;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinRT;

namespace PCGet;

public static class Program
{
    private const string InstanceKey = "PCGet";

    [STAThread]
    public static async Task Main(string[] args)
    {
        ComWrappersSupport.InitializeComWrappers();

        // Invoked by the Inno Setup uninstaller to remove per-user registrations of an unpackaged install.
        if (HasArgument("--uninstall-cleanup"))
        {
            RunUninstallCleanup();
            return;
        }

        var currentInstance = AppInstance.GetCurrent();
        var activatedArgs = currentInstance.GetActivatedEventArgs();
        var mainInstance = AppInstance.FindOrRegisterForKey(InstanceKey);

        if (!mainInstance.IsCurrent)
        {
            await mainInstance.RedirectActivationToAsync(activatedArgs);
            return;
        }

        mainInstance.Activated += MainInstance_Activated;

        var startedByWindows = activatedArgs.Kind == ExtendedActivationKind.StartupTask ||
                               HasArgument(StartupService.StartupArgument);

        Application.Start(_ =>
        {
            var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(dispatcherQueue));
            new App(startedByWindows);
        });
    }

    private static bool HasArgument(string argument)
    {
        return Environment.GetCommandLineArgs()
            .Skip(1)
            .Any(arg => string.Equals(arg, argument, StringComparison.OrdinalIgnoreCase));
    }

    private static void RunUninstallCleanup()
    {
        StartupService.RemoveRunKey();

        try
        {
            AppNotificationManager.Default.UnregisterAll();
        }
        catch
        {
            // Best-effort: notification registration may never have happened.
        }
    }

    private static void MainInstance_Activated(object? sender, AppActivationArguments args)
    {
        App.CurrentApp?.DispatcherQueue.TryEnqueue(() => App.CurrentApp.ShowMainWindow());
    }
}