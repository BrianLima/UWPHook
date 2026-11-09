using UWPHook.Core.Diagnostics;

namespace UWPHook.Core.Steam;

/// <summary>
/// Exports the selected apps to every Steam account. Everything that can stop the export (nothing selected,
/// no Steam, no account) is checked before Steam is touched; the artwork and icons are prepared once; then each
/// account's shortcuts.vdf is backed up, merged and saved, and its artwork copied, while Steam is closed (M7).
/// </summary>
public sealed class ShortcutExporter(
    ISteamLocator steam,
    SteamRestarter restarter,
    ShortcutsBackupFolder backups,
    IArtworkInstaller artwork,
    ILog log)
{
    /// <summary>What really happened; see <see cref="ExportSummary"/>. The artwork is cleaned up whatever the outcome.</summary>
    public async Task<ExportSummary> ExportAsync(ExportRequest request, CancellationToken ct = default)
    {
        try
        {
            return await ExportUniqueAppsAsync(request, ct).ConfigureAwait(false);
        }
        finally
        {
            artwork.CleanUp();
        }
    }

    private async Task<ExportSummary> ExportUniqueAppsAsync(ExportRequest request, CancellationToken ct)
    {
        var apps = request.UniqueApps;
        if (apps.Count == 0) return ExportSummary.NothingSelected();

        var installFolder = steam.InstallFolder();
        if (installFolder is null)
        {
            log.Warning("Steam was not found in the registry (HKCU SteamPath, HKLM InstallPath); nothing was exported");
            return ExportSummary.SteamNotFound();
        }

        var users = steam.Users(installFolder);
        if (users.Count == 0)
        {
            log.Warning("No Steam account folder in {SteamFolder}\\userdata; nothing was exported", installFolder);
            return ExportSummary.NoSteamUsers();
        }

        await artwork.PrepareAsync(apps, ct).ConfigureAwait(false);

        // The same shortcuts go to every account, so each icon is persisted once, not once per account
        var tags = request.Tags;
        var wanted = apps
            .Select(app => ShortcutSpec.ForApp(app.Name, app.Aumid, app.Executable, request.UwpHookExePath, artwork.IconFor(app), tags))
            .ToList();

        var folders = users.Select(user => user.FolderIn(installFolder)).ToList();
        AccountWrites? writes = null;
        var stopped = await restarter.WhileStoppedAsync(() =>
        {
            writes = WriteEveryAccount(folders, wanted);
            return Task.CompletedTask;
        }, ct).ConfigureAwait(false);

        if (stopped is StopOutcome.StillRunning or StopOutcome.ExecutableUnknown)
        {
            log.Warning("Steam is still running ({Outcome}); nothing was exported", stopped);
            return ExportSummary.SteamNotStopped(stopped);
        }

        // Not running or stopped: the write ran.
        return ExportSummary.ForUsers(writes!.Written, writes.Failed, stopped).WithArtworkFailures(writes.ArtworkFailed);
    }

    private sealed record AccountWrites(int Written, int Failed, IReadOnlyList<string> ArtworkFailed);

    /// <summary>Writes the shortcuts to each account, then copies the artwork to the ones that were written.</summary>
    private AccountWrites WriteEveryAccount(IReadOnlyList<string> folders, IReadOnlyList<ShortcutSpec> wanted)
    {
        var gridIdsByFolder = new List<(string Folder, IReadOnlyDictionary<string, uint> GridIds)>();
        foreach (var folder in folders)
        {
            try
            {
                // Update UWPHook's own shortcuts in place (keeping their appid), never touch anyone else's.
                // A shortcuts.vdf that can't be read throws here and is left as it is.
                var merge = new SteamUserShortcuts(folder, backups, log).Export(wanted);
                gridIdsByFolder.Add((folder, GridIdsByAumid(merge)));
            }
            catch (Exception e)
            {
                // Counted in the summary, which points to the log
                log.Error(e, "Exporting your games to {User} failed", folder);
            }
        }

        var artworkFailed = gridIdsByFolder
            .Where(account => !TryInstallArtwork(account.Folder, account.GridIds))
            .Select(account => Path.GetFileName(account.Folder))
            .ToList();

        return new AccountWrites(gridIdsByFolder.Count, folders.Count - gridIdsByFolder.Count, artworkFailed);
    }

    /// <summary>The shortcuts are already written, so failed artwork is a note in the summary (R33), not a failed export.</summary>
    private bool TryInstallArtwork(string folder, IReadOnlyDictionary<string, uint> gridIds)
    {
        try
        {
            artwork.InstallFor(folder, gridIds);
            return true;
        }
        catch (Exception e)
        {
            log.Error(e, "Could not copy the artwork to {User}", folder);
            return false;
        }
    }

    /// <summary>The grid id (unsigned appid) of every shortcut UWPHook wrote, by AUMID.</summary>
    private static Dictionary<string, uint> GridIdsByAumid(MergeResult merge)
    {
        var gridIds = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        foreach (var merged in merge.Shortcuts.Where(merged => merged.Spec is not null))
        {
            gridIds[merged.Spec!.Aumid] = unchecked((uint)merged.AppId);
        }

        return gridIds;
    }
}
