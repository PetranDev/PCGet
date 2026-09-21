using System;
using System.IO;

namespace PCGet.Services;

public static class UpdaterCleanupService
{
    private static readonly TimeSpan MinimumAge = TimeSpan.FromHours(1);

    public static void Cleanup()
    {
        try
        {
            var updaterRoot = Path.Combine(Path.GetTempPath(), "PCGet", "Updater");

            if (!Directory.Exists(updaterRoot))
                return;

            foreach (var directory in Directory.EnumerateDirectories(updaterRoot))
                TryDeleteDirectory(directory);

            TryDeleteRootIfEmpty(updaterRoot);
        }
        catch
        {
            // Temporary updater cleanup must never prevent PCGet from starting.
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
            // A later PCGet launch will try again.
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