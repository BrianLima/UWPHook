using System.IO;
using UWPHook.Core.Steam;

namespace UWPHook.Infrastructure
{
    /// <summary>
    /// Builds the Core export and remove services on the real Windows pieces (registry, steam.exe, the
    /// <c>%AppData%\Briano\UWPHook</c> folders). Task 10c-2's composition root replaces this.
    /// </summary>
    internal static class SteamServices
    {
        private static string AppDataFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Briano", "UWPHook");

        public static string BackupFolder => Path.Combine(AppDataFolder, "backups");

        public static string IconsFolder => Path.Combine(AppDataFolder, "icons");

        public static string TempGridFolder => Path.Combine(Path.GetTempPath(), "UWPHook", "tmp_grid");

        /// <param name="onKeyRejected">Shows that SteamGridDB rejected the key; called at most once per export, off the UI thread.</param>
        public static ShortcutExporter Exporter(Action onKeyRejected)
        {
            var steam = NewSteamLibrary();
            var artwork = new SteamGridArtwork(SteamGridDbService.Shared, TempGridFolder, IconsFolder, onKeyRejected, SerilogLog.Instance);
            return new ShortcutExporter(steam, NewSteamRestarter(steam), NewBackupFolder(), artwork, SerilogLog.Instance);
        }

        public static ShortcutRemover Remover()
        {
            var steam = NewSteamLibrary();
            return new ShortcutRemover(steam, NewSteamRestarter(steam), NewBackupFolder(), SerilogLog.Instance);
        }

        private static SteamLibrary NewSteamLibrary() => new SteamLibrary(new WindowsSteamRegistry(), SerilogLog.Instance);

        /// <summary>
        /// Stops and starts the Steam the locator found. Steam may write its own copy of shortcuts.vdf when it
        /// exits, so shortcuts are written while it is closed (stop, write, start), never before a restart.
        /// </summary>
        private static SteamRestarter NewSteamRestarter(ISteamLocator steam) =>
            new SteamRestarter(new WindowsSteamProcess(steam), TimeProvider.System,
                timeout: TimeSpan.FromSeconds(15), poll: TimeSpan.FromMilliseconds(500));

        private static ShortcutsBackupFolder NewBackupFolder() =>
            new ShortcutsBackupFolder(BackupFolder, TimeProvider.System, SerilogLog.Instance);
    }
}
