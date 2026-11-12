using UWPHook.Core.Diagnostics;

namespace UWPHook.Core.Steam;

/// <summary>
/// Exports the selected apps to every Steam account. Everything that can stop the export (nothing selected,
/// no Steam, no account) is checked before Steam is touched; the artwork and icons are prepared once; then, if the
/// user lets UWPHook close a running Steam, each account's shortcuts.vdf is backed up, merged and saved, and its
/// artwork copied, while Steam is closed. The Steam client no longer reads a shortcut's tags, so in that same
/// window the shortcuts are also added to the account's collections named after the tags.
/// </summary>
public sealed class ShortcutExporter(
    ISteamLocator steam,
    SteamRestarter restarter,
    ShortcutsBackupFolder backups,
    IArtworkInstaller artwork,
    ISteamCollections collections,
    ILog log) : IShortcutExporter
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

        var users = request.Accounts.ApplyLogged(steam.Users(installFolder), log);
        if (users.Count == 0)
        {
            log.Warning("No Steam account folder in {SteamFolder}\\userdata; nothing was exported", installFolder);
            return ExportSummary.NoSteamUsers();
        }

        var artworkFailed = await PrepareArtworkAsync(apps, ct).ConfigureAwait(false);

        // The same shortcuts go to every account, so each icon is persisted once, not once per account.
        // UWPHook.exe is only looked for now that nothing can stop the export.
        var tags = request.Tags;
        var exe = request.FindUwpHookExe();
        var wanted = apps
            .Select(app => ShortcutSpec.ForApp(app.Name, app.Aumid, app.Executable, exe, IconOf(app), tags))
            .ToList();

        var folders = users.Select(user => user.FolderIn(installFolder)).ToList();
        AccountWrites? writes = null;
        var stopped = await restarter.WhileStoppedAsync(() =>
        {
            writes = WriteEveryAccount(folders, wanted, tags);
            return Task.CompletedTask;
        }, request.MayCloseSteam, ct).ConfigureAwait(false);

        if (stopped.LeftSteamRunning())
        {
            log.Warning("Steam is still running ({Outcome}); nothing was exported", stopped);
            return ExportSummary.SteamNotStopped(stopped);
        }

        // Not running or stopped: the write ran.
        return ExportSummary.ForUsers(writes!.Written, writes.Failed, stopped)
            .WithArtworkDownloadFailures(artworkFailed)
            .WithArtworkFailures(writes.ArtworkFailed)
            .WithCollectionsSkipped(writes.CollectionsSkipped)
            .WithoutCollections(writes.CollectionsMissing);
    }

    /// <summary>
    /// The names of the apps whose artwork could not be downloaded. Artwork is optional: when preparing it
    /// throws, every app is exported without it. Cancelling still stops the export.
    /// </summary>
    private async Task<IReadOnlyList<string>> PrepareArtworkAsync(IReadOnlyList<ExportApp> apps, CancellationToken ct)
    {
        try
        {
            return await artwork.PrepareAsync(apps, ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            log.Error(e, "Preparing the artwork failed; the apps are exported without it");
            return [.. apps.Select(app => app.Name)];
        }
    }

    /// <summary>
    /// The app's icon path, or none when it can't be picked (a logo folder that can't be read, say): an icon is
    /// optional like the rest of the artwork, so that app is exported without one instead of stopping the export.
    /// </summary>
    private string IconOf(ExportApp app)
    {
        try
        {
            return artwork.IconFor(app);
        }
        catch (Exception e)
        {
            log.Warning(e, "Could not pick an icon for {App}; it is exported without one", app.Name);
            return "";
        }
    }

    private sealed record AccountWrites(
        int Written,
        int Failed,
        IReadOnlyList<string> ArtworkFailed,
        IReadOnlyList<string> CollectionsSkipped,
        IReadOnlyList<string> CollectionsMissing);

    /// <summary>
    /// Writes the shortcuts to each account, then, for the ones that were written, adds them to the collections
    /// named after the tags and copies the artwork.
    /// </summary>
    private AccountWrites WriteEveryAccount(
        IReadOnlyList<string> folders, IReadOnlyList<ShortcutSpec> wanted, IReadOnlyList<string> tags)
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

        var collectionOutcomes = gridIdsByFolder
            .Select(account => (
                User: Path.GetFileName(account.Folder),
                Outcome: AddToCollections(account.Folder, tags, account.GridIds)))
            .ToList();

        return new AccountWrites(
            gridIdsByFolder.Count,
            folders.Count - gridIdsByFolder.Count,
            artworkFailed,
            UsersWith(CollectionsOutcome.Skipped),
            UsersWith(CollectionsOutcome.NotFound));

        List<string> UsersWith(CollectionsOutcome outcome) =>
            [.. collectionOutcomes.Where(account => account.Outcome == outcome).Select(account => account.User)];
    }

    /// <summary>
    /// The collections promise not to throw. Should they anyway, the shortcuts are already written: the account is
    /// reported as skipped, and the export goes on to the other accounts and to starting Steam again.
    /// </summary>
    private CollectionsOutcome AddToCollections(string folder, IReadOnlyList<string> tags, IReadOnlyDictionary<string, uint> gridIds)
    {
        try
        {
            return collections.AddShortcuts(folder, tags, [.. gridIds.Values]);
        }
        catch (Exception e)
        {
            log.Error(e, "Could not add the shortcuts of {User} to its Steam collections", folder);
            return CollectionsOutcome.Skipped;
        }
    }

    /// <summary>The shortcuts are already written, so failed artwork is a note in the summary, not a failed export.</summary>
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
