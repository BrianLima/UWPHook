namespace UWPHook.Core.Steam;

/// <summary>Exports apps to Steam; see <see cref="ShortcutExporter"/>.</summary>
public interface IShortcutExporter
{
    Task<ExportSummary> ExportAsync(ExportRequest request, CancellationToken ct = default);
}

/// <summary>Removes UWPHook's shortcuts from Steam; see <see cref="ShortcutRemover"/>.</summary>
public interface IShortcutRemover
{
    /// <param name="mayCloseSteam">
    /// Asked once, only when Steam is running and there is something to remove: true lets UWPHook close it.
    /// </param>
    Task<RemoveSummary> RemoveAsync(SteamAccountChoice accounts, Func<bool> mayCloseSteam, CancellationToken ct = default);
}
