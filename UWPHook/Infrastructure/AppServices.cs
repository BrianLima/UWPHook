using System.IO;
using System.Windows.Threading;
using Serilog;
using UWPHook.Core.Configuration;
using UWPHook.Core.Diagnostics;
using UWPHook.Core.Install;
using UWPHook.Core.Steam;
using UWPHook.Presentation;
using UWPHook.Presentation.Games;
using UWPHook.Presentation.Settings;

namespace UWPHook.Infrastructure
{
    /// <summary>
    /// The composition root: builds UWPHook's services once, by hand (no container), on the real Windows pieces
    /// (registry, steam.exe, Appx, the <c>%AppData%\Briano\UWPHook</c> folders). A launch from Steam only needs
    /// <see cref="Settings"/>; the windows' services are built by <see cref="GamesWindow"/>.
    /// </summary>
    internal sealed class AppServices
    {
        private static readonly string AppDataFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Briano", "UWPHook");

        private readonly ILog _log = SerilogLog.Instance;

        public AppServices()
        {
            // One store for the app's lifetime: it remembers a key it could not decrypt, so saving does not erase it.
            Settings = new SettingsService(NewStore(), () => LegacySettingsImport.From(LegacySettingsImport.DefaultBrianoFolder), _log);
        }

        public ISettingsService Settings { get; }

        /// <summary>The games list, and the Settings window it opens, with the services they share.</summary>
        public UWPHook.GamesWindow GamesWindow(Dispatcher dispatcher)
        {
            var dialogs = new WpfDialogs(dispatcher);
            var steam = new SteamLibrary(new WindowsSteamRegistry(), _log);
            // One picker for the list and the export, so the icon a game shows is the one its shortcut gets. The list's
            // also decodes each thumbnail off the UI thread.
            var icons = new SquareIconPicker(_log);
            var backups = new ShortcutsBackupFolder(Path.Combine(AppDataFolder, "backups"), TimeProvider.System, _log);
            var artwork = new SteamGridArtwork(new SteamGridDbService(() => Settings.Current), TempGridFolder, Path.Combine(AppDataFolder, "icons"), icons,
                () => dialogs.Show(MessageSeverity.Warning, SteamGridDbService.InvalidKeyMessage), _log);
            // One restarter for both, so an export and a removal never stop, write and start Steam at the same time.
            var restarter = NewSteamRestarter(steam);
            var exporter = new ShortcutExporter(steam, restarter, backups, artwork, new CloudStorageCollections(backups, TimeProvider.System, _log), _log);
            var remover = new ShortcutRemover(steam, restarter, backups, _log);

            var games = new GamesViewModel(new KnownAppsCatalog(new PackageManagerCatalog(new WindowsPackageLocations(WindowsOsVersion.Capabilities))), new ThumbnailingIconPicker(icons, IconThumbnails.Shared), exporter, Settings, dialogs, _log, StableExePath);
            return new UWPHook.GamesWindow(games, () => new SettingsWindow(new SettingsViewModel(Settings, steam, remover, dialogs, new AppHost(), _log)));
        }

        private static string TempGridFolder => Path.Combine(Path.GetTempPath(), "UWPHook", "tmp_grid");

        /// <summary>The UWPHook.exe path that survives updates (the stable launcher path).</summary>
        private static string StableExePath() =>
            InstallLayout.StableExePath(Environment.ProcessPath ?? throw new InvalidOperationException("Cannot find the path of UWPHook.exe"));

        /// <summary>
        /// Stops and starts the Steam the locator found. Steam may write its own copy of shortcuts.vdf when it
        /// exits, so shortcuts are written while it is closed (stop, write, start), never before a restart.
        /// </summary>
        private static SteamRestarter NewSteamRestarter(ISteamLocator steam) =>
            new SteamRestarter(new WindowsSteamProcess(steam), TimeProvider.System,
                timeout: TimeSpan.FromSeconds(15), poll: TimeSpan.FromMilliseconds(500));

        private static SettingsStore NewStore() =>
            new SettingsStore(Path.Combine(AppDataFolder, "settings.json"), new DpapiSecretProtector())
            {
                OnUnreadableFile = (e, movedTo) =>
                {
                    if (movedTo is null)
                        Log.Error(e, "settings.json could not be read, so the defaults are used for now; the file was left as it is");
                    else
                        Log.Error(e, "settings.json is damaged, so the defaults are used; it was moved to {MovedTo}", movedTo);
                },
                OnUndecryptableKey = e => Log.Warning(e, "The SteamGridDB key in settings.json was saved by another Windows user or PC and cannot be read; enter it again in Settings"),
            };
    }
}
