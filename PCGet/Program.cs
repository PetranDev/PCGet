using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
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

        var currentInstance = AppInstance.GetCurrent();
        var activatedArgs = currentInstance.GetActivatedEventArgs();
        var mainInstance = AppInstance.FindOrRegisterForKey(InstanceKey);

        if (!mainInstance.IsCurrent)
        {
            await mainInstance.RedirectActivationToAsync(activatedArgs);
            return;
        }

        mainInstance.Activated += MainInstance_Activated;

        var startedByWindows = activatedArgs.Kind == ExtendedActivationKind.StartupTask;

        Application.Start(_ =>
        {
            var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(dispatcherQueue));
            new App(startedByWindows);
        });
    }

    private static void MainInstance_Activated(object? sender, AppActivationArguments args)
    {
        App.CurrentApp?.DispatcherQueue.TryEnqueue(() => App.CurrentApp.ShowMainWindow());
    }
}