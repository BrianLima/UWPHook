using System.Net.Http;
using UWPHook.Core.Diagnostics;
using UWPHook.Core.Steam;

namespace UWPHook.Infrastructure
{
    /// <summary>
    /// An export's artwork (<see cref="IArtworkInstaller"/>): SteamGridDB images downloaded per app to a temp folder
    /// (<see cref="ArtworkDownloader"/>), copied to each account's grid folder under that account's appid
    /// (<see cref="GridInstaller"/>), and each shortcut's icon copied to the icons folder (<see cref="IconPersister"/>).
    /// </summary>
    internal sealed class SteamGridArtwork : IArtworkInstaller
    {
        private static readonly HttpClient imageClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        private readonly SteamGridDbService _sgdb;
        private readonly TempGridFolder _temp;
        private readonly ArtworkDownloader _downloader;
        private readonly GridInstaller _grids;
        private readonly IconPersister _icons;
        private readonly Action _onKeyRejected;
        private readonly ILog _log;

        /// <param name="tempRoot">Where the downloads go, one folder per app; deleted by <see cref="CleanUp"/>.</param>
        /// <param name="iconsFolder"><c>%AppData%\Briano\UWPHook\icons</c>.</param>
        /// <param name="onKeyRejected">Called once per export when SteamGridDB rejects the API key.</param>
        /// <param name="iconIn">Picks the icon in an app's logo folder; <see cref="SquareIcons.WidestIn"/> by default.</param>
        public SteamGridArtwork(SteamGridDbService sgdb, string tempRoot, string iconsFolder, Action onKeyRejected, ILog log,
            Func<string, string>? iconIn = null, HttpClient? images = null)
        {
            _sgdb = sgdb;
            _log = log;
            _onKeyRejected = onKeyRejected;
            _temp = new TempGridFolder(tempRoot, log);
            _downloader = new ArtworkDownloader(sgdb, _temp, images ?? imageClient, log);
            _grids = new GridInstaller(_temp, log);
            _icons = new IconPersister(iconsFolder, _temp, iconIn ?? (folder => SquareIcons.WidestIn(folder)), log);
        }

        /// <summary>
        /// Downloads each app's artwork; does nothing without an API key. A rejected key is told once per export.
        /// Returns the names of the apps whose download failed.
        /// </summary>
        public async Task<IReadOnlyList<string>> PrepareAsync(IReadOnlyList<ExportApp> apps, CancellationToken ct)
        {
            _log.Debug("Downloading SteamGridDB artwork: {HasApiKey}", _sgdb.HasApiKey);
            if (!_sgdb.HasApiKey) return [];

            var downloads = await _downloader.DownloadAllAsync(apps, ct);
            if (downloads.KeyRejected)
            {
                // The key is kept so the user can fix it in Settings
                _log.Warning("SteamGridDB rejected the API key (HTTP 401); exporting without artwork");
                _onKeyRejected();
            }

            return downloads.FailedApps;
        }

        public string IconFor(ExportApp app) => _icons.IconFor(app);

        public void InstallFor(string userFolder, IReadOnlyDictionary<string, uint> gridIdsByAumid) => _grids.InstallFor(userFolder, gridIdsByAumid);

        public void CleanUp() => _temp.Delete();
    }
}
