using UWPHook.Core.Diagnostics;

namespace UWPHook.Core.Steam;

public sealed class ShortcutExporter
{
    public ShortcutExporter(ISteamLocator steam, SteamRestarter restarter, ShortcutsBackupFolder backups, IArtworkInstaller artwork, ILog log)
    {
    }

    public Task<ExportSummary> ExportAsync(ExportRequest request, CancellationToken ct = default) => throw new NotImplementedException();
}
