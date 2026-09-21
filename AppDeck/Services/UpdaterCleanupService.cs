using System;
using System.IO;

namespace AppDeck.Services;

public static class UpdaterCleanupService
{
    private static readonly TimeSpan MinimumAge = TimeSpan.FromHours(1);

    public static void Cleanup()
    {
        try
        {
            var updaterRoot = Path.Combine(Path.GetTempPath(), "AppDeck", "Updater");

            if (!Directory.Exists(updaterRoot))
                return;

            foreach (var directory in Directory.EnumerateDirectories(updaterRoot))
                TryDeleteDirectory(directory);

            TryDeleteRootIfEmpty(updaterRoot);
        }
        catch
        {
            // Temporary updater cleanup must never prevent AppDeck from starting.
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            var createdUtc = Directory.GetCreationTimeUtc(directory);

            if (DateTime.UtcNow - createdUtc < MinimumAge)
                return;

            Directory.Delete(directory, true);
        }
        catch
        {
            // The updater may still be running or a file may temporarily be locked.
            // A later AppDeck launch will try again.
        }
    }

    private static void TryDeleteRootIfEmpty(string updaterRoot)
    {
        try
        {
            if (!Directory.EnumerateFileSystemEntries(updaterRoot).GetEnumerator().MoveNext())
                Directory.Delete(updaterRoot);
        }
        catch
        {
            // Cleanup is best-effort.
        }
    }
}