namespace UWPHook.Core.Steam;

/// <summary>
/// The artwork side of an export, which the app does (SteamGridDB downloads and file copies).
/// <see cref="ShortcutExporter"/> calls it in this order: prepare, icons, install per user, clean up.
/// </summary>
public interface IArtworkInstaller
{
    /// <summary>Before Steam is touched: fetches whatever artwork the apps get. Failures are the installer's to log.</summary>
    /// <returns>The names of the apps whose artwork could not be downloaded.</returns>
    Task<IReadOnlyList<string>> PrepareAsync(IReadOnlyList<ExportApp> apps, CancellationToken ct);

    /// <summary>The icon path for the app's shortcut (the same for every Steam account).</summary>
    string IconFor(ExportApp app);

    /// <summary>
    /// Copies the prepared artwork to one Steam account, named after the appid each app's shortcut got there
    /// (<paramref name="gridIdsByAumid"/>). Throws when it can't; the account's shortcuts are already written.
    /// </summary>
    void InstallFor(string userFolder, IReadOnlyDictionary<string, uint> gridIdsByAumid);

    /// <summary>After the export, whatever its outcome. Never throws.</summary>
    void CleanUp();
}
