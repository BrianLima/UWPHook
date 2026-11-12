using UWPHook.Core.Diagnostics;

namespace UWPHook.Core.Steam;

/// <summary>
/// The shortcuts.vdf of one Steam user (a <c>userdata/&lt;id&gt;</c> folder): loads it (a missing file is
/// an empty one), backs it up before every write, and replaces it atomically, so a crash mid-write
/// can't leave the user with a truncated file. Only shortcuts UWPHook created are ever changed.
/// </summary>
public sealed class SteamUserShortcuts(string userFolder, ShortcutsBackupFolder backups, ILog log)
{
    public string ShortcutsPath => Path.Combine(userFolder, "config", "shortcuts.vdf");

    /// <summary>
    /// Merges <paramref name="wanted"/> into the user's shortcuts and saves them.
    /// Throws, leaving the file untouched, when the existing file can't be read.
    /// </summary>
    /// <returns>The merge, which holds the appid each wanted shortcut got for this user.</returns>
    public MergeResult Export(IReadOnlyList<ShortcutSpec> wanted)
    {
        var file = Load();
        var merge = ShortcutMerger.Merge(file.Entries, wanted);
        foreach (var duplicate in merge.UntouchedDuplicateIndexes)
        {
            var entry = file.Entries[duplicate];
            log.Warning("Leaving duplicate UWPHook shortcut untouched: {AppName} ({LaunchOptions})", entry.AppName, entry.LaunchOptions);
        }

        file.Apply(merge);
        Save(file);
        return merge;
    }

    /// <summary>How many shortcuts UWPHook created for this user, without writing anything.</summary>
    public int CountUwpHookShortcuts() =>
        File.Exists(ShortcutsPath) ? Load().Entries.Count(UwpHookShortcut.IsCreatedByUwpHook) : 0;

    /// <summary>Removes the shortcuts UWPHook created; nothing is written when there are none.</summary>
    /// <returns>How many shortcuts were removed.</returns>
    public int RemoveUwpHookShortcuts()
    {
        if (!File.Exists(ShortcutsPath)) return 0;

        var file = Load();
        var removed = file.RemoveWhere(UwpHookShortcut.IsCreatedByUwpHook);
        if (removed > 0) Save(file);

        return removed;
    }

    private string UserId => Path.GetFileName(userFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

    private ShortcutsFile Load()
    {
        if (!File.Exists(ShortcutsPath)) return ShortcutsFile.Empty();

        using var stream = File.OpenRead(ShortcutsPath);
        return ShortcutsFile.Load(stream);
    }

    private void Save(ShortcutsFile file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ShortcutsPath)!);
        if (File.Exists(ShortcutsPath))
        {
            backups.BackUp(ShortcutsPath, UserId);
        }
        else
        {
            log.Information("No shortcuts.vdf to back up for {UserFolder}", userFolder);
        }

        var temporary = ShortcutsPath + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                file.Save(stream);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, ShortcutsPath, overwrite: true);
        }
        catch
        {
            // The original file is still intact; don't leave a half-written copy next to it
            DeleteQuietly(temporary);
            throw;
        }

        log.Debug("Shortcuts written to {Path}", ShortcutsPath);
    }

    private void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            log.Warning(e, "Could not delete {Path}", path);
        }
    }
}
