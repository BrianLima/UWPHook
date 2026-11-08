using UWPHook.Core.Diagnostics;

namespace UWPHook.Core.Steam;

public sealed class ShortcutRemover
{
    public ShortcutRemover(ISteamLocator steam, SteamRestarter restarter, ShortcutsBackupFolder backups, ILog log)
    {
    }

    public Task<RemoveSummary> RemoveAsync(CancellationToken ct = default) => throw new NotImplementedException();
}
