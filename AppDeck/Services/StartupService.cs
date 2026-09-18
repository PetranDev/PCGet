using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace AppDeck.Services;

public sealed class StartupService
{
    private const string StartupTaskId = "AppDeckStartup";

    public async Task<StartupTaskState> GetStateAsync()
    {
        var task = await StartupTask.GetAsync(StartupTaskId);
        return task.State;
    }

    public async Task<StartupTaskState> SetEnabledAsync(bool enabled)
    {
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
}