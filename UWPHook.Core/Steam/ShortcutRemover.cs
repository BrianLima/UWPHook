using UWPHook.Core.Diagnostics;

namespace UWPHook.Core.Steam;

/// <summary>
/// "Remove UWPHook shortcuts": removes the shortcuts UWPHook created from every Steam account, backing up each
/// changed shortcuts.vdf first, and keeps everyone else's. Steam is only closed (and started again) when there
/// is something to remove, and the files are only written while it is closed.
/// </summary>
public sealed class ShortcutRemover(ISteamLocator steam, SteamRestarter restarter, ShortcutsBackupFolder backups, ILog log) : IShortcutRemover
{
    /// <summary>Removes them from every account.</summary>
    public Task<RemoveSummary> RemoveAsync(CancellationToken ct = default) => RemoveAsync(SteamAccountChoice.All, ct);

    /// <summary>Removes them from the chosen accounts (#76).</summary>
    public async Task<RemoveSummary> RemoveAsync(SteamAccountChoice accounts, CancellationToken ct = default)
    {
        log.Debug("Removing UWPHook's shortcuts from shortcuts.vdf");
        var installFolder = steam.InstallFolder();
        if (installFolder is null) return RemoveSummary.SteamNotFound();

        var shortcuts = steam.Users(installFolder)
            .Select(user => new SteamUserShortcuts(user.FolderIn(installFolder), backups, log))
            .ToList();
        var (found, unreadable) = Count(shortcuts);
        if (found == 0) return RemoveSummary.NothingFound(unreadable);

        var removed = 0;
        var failed = 0;
        var stopped = await restarter.WhileStoppedAsync(() =>
        {
            (removed, failed) = RemoveFrom(shortcuts);
            return Task.CompletedTask;
        }, ct).ConfigureAwait(false);

        return stopped is StopOutcome.StillRunning or StopOutcome.ExecutableUnknown
            ? RemoveSummary.SteamNotStopped(stopped)
            : RemoveSummary.ForUsers(removed, failed, stopped);
    }

    /// <summary>How many UWPHook shortcuts the accounts have, and how many accounts' files could not be read.</summary>
    private (int Found, int Unreadable) Count(IEnumerable<SteamUserShortcuts> accounts)
    {
        var found = 0;
        var unreadable = 0;
        foreach (var account in accounts)
        {
            try
            {
                found += account.CountUwpHookShortcuts();
            }
            catch (Exception e)
            {
                unreadable++;
                log.Error(e, "Could not read the shortcuts of {User}", account.ShortcutsPath);
            }
        }

        return (found, unreadable);
    }

    private (int Removed, int Failed) RemoveFrom(IEnumerable<SteamUserShortcuts> accounts)
    {
        var removed = 0;
        var failed = 0;
        foreach (var account in accounts)
        {
            try
            {
                removed += account.RemoveUwpHookShortcuts();
            }
            catch (Exception e)
            {
                failed++;
                log.Error(e, "Removing UWPHook's shortcuts from {User} failed", account.ShortcutsPath);
            }
        }

        return (removed, failed);
    }
}
