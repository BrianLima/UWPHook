using UWPHook.Core.Diagnostics;

namespace UWPHook.Core.Steam;

/// <summary>
/// The folder that keeps a copy of shortcuts.vdf (and of Steam's collections file) from before each write (<c>%AppData%\Briano\UWPHook\backups</c>),
/// named as <see cref="ShortcutsBackup"/> says, with the time from <paramref name="time"/>.
/// </summary>
public sealed class ShortcutsBackupFolder(string folder, TimeProvider time, ILog log)
{
    /// <summary>
    /// Copies <paramref name="shortcutsPath"/> here as <c>&lt;user id&gt;_&lt;timestamp&gt;_shortcuts.vdf</c>, then keeps only
    /// this user's newest <see cref="ShortcutsBackup.KeepPerUser"/> backups. Throws when the copy fails, so the
    /// write it protects doesn't happen.
    /// </summary>
    public void BackUp(string shortcutsPath, string userId) => BackUp(shortcutsPath, userId, ShortcutsBackup.ShortcutsSuffix);

    /// <summary>The same for Steam's collections file: <c>&lt;user id&gt;_&lt;timestamp&gt;_collections.json</c>, with its own newest twenty.</summary>
    public void BackUpCollections(string collectionsPath, string userId) => BackUp(collectionsPath, userId, ShortcutsBackup.CollectionsSuffix);

    private void BackUp(string path, string userId, string suffix)
    {
        Directory.CreateDirectory(folder);
        var backup = Path.Combine(folder, ShortcutsBackup.FileName(userId, time.GetLocalNow().DateTime, suffix));
        File.Copy(path, backup);
        log.Debug("Backup created: {Backup}", backup);

        Prune(userId, suffix);
    }

    /// <summary>A backup that can't be deleted is only logged: it must not stop the write it protects.</summary>
    private void Prune(string userId, string suffix)
    {
        var names = Directory.GetFiles(folder).Select(path => Path.GetFileName(path));
        foreach (var old in ShortcutsBackup.ToPrune(names, userId, suffix: suffix))
        {
            var path = Path.Combine(folder, old);
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
}
