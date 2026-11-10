using UWPHook.Core.Diagnostics;

namespace UWPHook.Core.Steam;

/// <summary>Steam's collections in <c>userdata/&lt;id&gt;/config/cloudstorage</c>. Not implemented yet.</summary>
public sealed class CloudStorageCollections(ShortcutsBackupFolder backups, TimeProvider time, ILog log) : ISteamCollections
{
    public CollectionsOutcome AddShortcuts(string userFolder, IReadOnlyList<string> collectionNames, IReadOnlyCollection<uint> appIds)
    {
        _ = (backups, time, log);
        return CollectionsOutcome.Unchanged;
    }
}
