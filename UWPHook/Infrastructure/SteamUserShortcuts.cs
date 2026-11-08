using System.IO;
using Serilog;
using UWPHook.Core.Steam;

namespace UWPHook.Infrastructure
{
    /// <summary>
    /// The shortcuts.vdf of one Steam user (a <c>userdata/&lt;id&gt;</c> folder): loads it (a missing file is
    /// an empty one), backs it up before every write, and replaces it atomically, so a crash mid-write
    /// can't leave the user with a truncated file. Only shortcuts UWPHook created are ever changed.
    /// </summary>
    internal sealed class SteamUserShortcuts
    {
        private readonly string _userPath;
        private readonly string _backupFolder;

        public SteamUserShortcuts(string userPath, string backupFolder)
        {
            _userPath = userPath;
            _backupFolder = backupFolder;
        }

        public string ShortcutsPath => Path.Combine(_userPath, "config", "shortcuts.vdf");

        /// <summary>
        /// Merges <paramref name="wanted"/> into the user's shortcuts and saves them.
        /// Throws, leaving the file untouched, when the existing file can't be read.
        /// </summary>
        /// <returns>The merge, which holds the appid each wanted shortcut got for this user.</returns>
        public MergeResult Export(IReadOnlyList<ShortcutSpec> wanted)
        {
            ShortcutsFile file = Load();
            MergeResult merge = ShortcutMerger.Merge(file.Entries, wanted);
            foreach (int duplicate in merge.UntouchedDuplicateIndexes)
            {
                ExistingShortcut entry = file.Entries[duplicate];
                Log.Warning("Leaving duplicate UWPHook shortcut untouched: {AppName} ({LaunchOptions})", entry.AppName, entry.LaunchOptions);
            }

            file.Apply(merge);
            Save(file);
            return merge;
        }

        /// <summary>Removes the shortcuts UWPHook created; nothing is written when there are none.</summary>
        /// <returns>How many shortcuts were removed.</returns>
        public int RemoveUwpHookShortcuts()
        {
            if (!File.Exists(ShortcutsPath))
            {
                return 0;
            }

            ShortcutsFile file = Load();
            int removed = file.RemoveWhere(UwpHookShortcut.IsCreatedByUwpHook);
            if (removed > 0)
            {
                Save(file);
            }

            return removed;
        }

        private ShortcutsFile Load()
        {
            if (!File.Exists(ShortcutsPath))
            {
                return ShortcutsFile.Empty();
            }

            using FileStream stream = File.OpenRead(ShortcutsPath);
            return ShortcutsFile.Load(stream);
        }

        private void Save(ShortcutsFile file)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ShortcutsPath)!);
            Backup();

            string temporary = ShortcutsPath + ".tmp";
            try
            {
                using (FileStream stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
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

            Log.Debug("Shortcuts written to {Path}", ShortcutsPath);
        }

        private static void DeleteQuietly(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception exception)
            {
                Log.Warning(exception, "Could not delete {Path}", path);
            }
        }

        /// <summary>Copies the current file to the backup folder as <c>&lt;user id&gt;_&lt;timestamp&gt;_shortcuts.vdf</c>.</summary>
        private void Backup()
        {
            if (!File.Exists(ShortcutsPath))
            {
                Log.Information("No shortcuts.vdf to back up for {UserPath}", _userPath);
                return;
            }

            Directory.CreateDirectory(_backupFolder);
            string userId = Path.GetFileName(_userPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string backup = Path.Combine(_backupFolder, $"{userId}_{DateTime.Now:yyyyMMddHHmmssfff}_shortcuts.vdf");
            File.Copy(ShortcutsPath, backup);
            Log.Debug("Backup created: {Backup}", backup);
        }
    }
}
