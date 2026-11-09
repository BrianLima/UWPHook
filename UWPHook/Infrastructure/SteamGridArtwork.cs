using System.IO;
using System.Net.Http;
using Serilog;
using UWPHook.Core.Artwork;
using UWPHook.Core.Artwork.SteamGridDb;
using UWPHook.Core.Steam;

namespace UWPHook.Infrastructure
{
    /// <summary>
    /// An export's artwork: SteamGridDB images downloaded to a temp folder per app (named with
    /// <see cref="GridIdPlaceholder"/>, because the appid is only known per Steam account), copied to each
    /// account's grid folder under that account's appid, and each shortcut's icon copied to the icons folder.
    /// </summary>
    internal sealed class SteamGridArtwork : IArtworkInstaller
    {
        /// <summary>At most this many apps download SteamGridDB artwork at once (each one makes up to 8 requests).</summary>
        private const int MaxConcurrentDownloads = 4;

        private const string GridIdPlaceholder = "appid";

        private static readonly HttpClient imageClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        private readonly SteamGridDbService _sgdb;
        private readonly string _tempRoot;
        private readonly string _iconsFolder;
        private readonly Action _onKeyRejected;

        /// <param name="tempRoot">Where the downloads go, one folder per app; deleted by <see cref="CleanUp"/>.</param>
        /// <param name="iconsFolder"><c>%AppData%\Briano\UWPHook\icons</c>.</param>
        /// <param name="onKeyRejected">Called once per export when SteamGridDB rejects the API key.</param>
        public SteamGridArtwork(SteamGridDbService sgdb, string tempRoot, string iconsFolder, Action onKeyRejected)
        {
            _sgdb = sgdb;
            _tempRoot = tempRoot;
            _iconsFolder = iconsFolder;
            _onKeyRejected = onKeyRejected;
        }

        /// <summary>
        /// Downloads each app's artwork, a few apps at a time so a large selection doesn't hit SteamGridDB with
        /// hundreds of requests at once. Does nothing without an API key.
        /// </summary>
        public async Task PrepareAsync(IReadOnlyList<ExportApp> apps, CancellationToken ct)
        {
            Log.Verbose("downloadGridImages: " + _sgdb.HasApiKey);
            if (!_sgdb.HasApiKey)
            {
                return;
            }

            using var throttle = new SemaphoreSlim(MaxConcurrentDownloads);
            bool[] keyRejected = await Task.WhenAll(apps.Select(async app =>
            {
                await throttle.WaitAsync(ct);
                try
                {
                    Log.Verbose("Downloading grid images for app " + app.Name);
                    return await DownloadAsync(app, ct);
                }
                finally
                {
                    throttle.Release();
                }
            }));

            if (keyRejected.Any(rejected => rejected))
            {
                // Once per export; the key is kept so the user can fix it in Settings
                Log.Warning("SteamGridDB rejected the API key (HTTP 401); exporting without artwork");
                _onKeyRejected();
            }
        }

        /// <summary>
        /// The app's widest square logo, else the SteamGridDB logo downloaded for it, copied to the icons folder
        /// (some apps move their files when they update, which "loses" the icon). When neither can be copied,
        /// the app's own path (possibly empty) is kept.
        /// </summary>
        public string IconFor(ExportApp app) =>
            TryPersistIcon(app.Aumid, app.IconFolder) ?? TryPersistIcon(app.Aumid, DownloadedLogo(app.Aumid)) ?? app.IconFolder;

        public void InstallFor(string userFolder, IReadOnlyDictionary<string, uint> gridIdsByAumid)
        {
            string userGridDirectory = Path.Combine(userFolder, "config", "grid");
            foreach (var (aumid, gridId) in gridIdsByAumid)
            {
                string appGridDirectory = TempGridDirectoryFor(aumid);

                // No images were downloaded, maybe the key is invalid or the app had no image
                if (!Directory.Exists(appGridDirectory))
                {
                    continue;
                }

                Directory.CreateDirectory(userGridDirectory);
                foreach (string image in Directory.GetFiles(appGridDirectory, GridIdPlaceholder + "*"))
                {
                    // e.g. "appid_hero.png" -> "3144859588_hero.png", keeping whatever extension was saved
                    string destination = Path.Combine(userGridDirectory, gridId + Path.GetFileName(image).Substring(GridIdPlaceholder.Length));
                    CopyArtwork(image, destination);
                }
            }
        }

        public void CleanUp()
        {
            try
            {
                if (Directory.Exists(_tempRoot))
                {
                    Directory.Delete(_tempRoot, true);
                }
            }
            catch (Exception exception)
            {
                // Runs after every export: a leftover temp folder must not hide the export's own outcome
                Log.Warning(exception, "Could not remove the temporary grid images in {Directory}", _tempRoot);
            }
        }

        /// <summary>A stable, file-name-safe key for an app's files: its AUMID with invalid characters replaced.</summary>
        private static string GridKey(string aumid)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            return new string(aumid.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        }

        private string TempGridDirectoryFor(string aumid) => Path.Combine(_tempRoot, GridKey(aumid));

        /// <summary>
        /// Takes the first search result and the first image of each kind. SteamGridDB errors are logged and leave
        /// the app without artwork; they never stop the export.
        /// </summary>
        /// <returns>True when SteamGridDB rejected the API key</returns>
        private async Task<bool> DownloadAsync(ExportApp app, CancellationToken ct)
        {
            SteamGridDbClient api = _sgdb.Client;
            ArtworkFilter filter = _sgdb.Filter();
            string tmpGridDirectory = TempGridDirectoryFor(app.Aumid);

            // Start from an empty folder, so images left by an earlier, interrupted export are never used
            if (Directory.Exists(tmpGridDirectory))
            {
                Directory.Delete(tmpGridDirectory, true);
            }

            var search = await api.SearchAsync(app.Name, ct);
            if (search.Status == SgdbStatus.InvalidApiKey)
            {
                return true;
            }
            if (!search.IsOk || search.Value.Count == 0)
            {
                Log.Information("No SteamGridDB game found for {App} ({Result})", app.Name, search);
                return false;
            }

            var game = search.Value[0];
            Log.Verbose("Detected game {Name} (SteamGridDB id {Id}) for {App}", game.Name, game.Id, app.Name);

            var results = await Task.WhenAll(
                api.GetGridsAsync(game.Id, filter, "460x215,920x430", ct),
                api.GetGridsAsync(game.Id, filter, "600x900,342x482,660x930", ct),
                api.GetHeroesAsync(game.Id, filter, null, ct),
                api.GetLogosAsync(game.Id, filter, null, ct));

            string[] fileNames = { $"{GridIdPlaceholder}.png", $"{GridIdPlaceholder}p.png", $"{GridIdPlaceholder}_hero.png", $"{GridIdPlaceholder}_logo.png" };

            CreateTempGridDirectory(tmpGridDirectory);

            var saves = new List<Task>();
            for (int i = 0; i < results.Length; i++)
            {
                if (results[i].IsOk && results[i].Value.Count > 0)
                {
                    saves.Add(SaveImageAsync(results[i].Value[0].Url, Path.Combine(tmpGridDirectory, fileNames[i])));
                }
                else if (!results[i].IsOk)
                {
                    Log.Warning("SteamGridDB has no {File} for {App}: {Result}", fileNames[i], app.Name, results[i]);
                }
            }

            await Task.WhenAll(saves);

            return results.Any(result => result.Status == SgdbStatus.InvalidApiKey);
        }

        private static void CreateTempGridDirectory(string tmpGridDirectory)
        {
            try
            {
                Directory.CreateDirectory(tmpGridDirectory);
            }
            catch (Exception exception)
            {
                Log.Error(exception, "Could not create {Directory}", tmpGridDirectory);
                throw new IOException("UWPHook was not able to create the tmp directory for some reason." + Environment.NewLine +
                    "You may solve this by manually creating the following folder:" + Environment.NewLine +
                    tmpGridDirectory, exception);
            }
        }

        /// <summary>
        /// Downloads the image at the url and saves its bytes untouched (animated images stay animated).
        /// The extension of <paramref name="destinationFilename"/> is replaced with the one from the url.
        /// A failed download is logged and skipped: artwork is optional.
        /// </summary>
        private static async Task SaveImageAsync(string imageUrl, string destinationFilename)
        {
            string destination = Path.ChangeExtension(destinationFilename, ArtworkFile.ExtensionFromUrl(imageUrl));

            try
            {
                byte[] bytes = await imageClient.GetByteArrayAsync(imageUrl);
                DeleteStaleSiblings(destination);
                await File.WriteAllBytesAsync(destination, bytes);
            }
            catch (Exception exception)
            {
                Log.Error(exception, "Could not save image {Url} to {Destination}", imageUrl, destination);
            }
        }

        /// <summary>Copies first, so a failed copy never leaves the account without the old art. One locked file is only logged.</summary>
        private static void CopyArtwork(string image, string destination)
        {
            try
            {
                File.Copy(image, destination, true);
                DeleteStaleSiblings(destination);
            }
            catch (Exception exception)
            {
                Log.Error(exception, "Could not copy artwork {Image} to {Destination}", image, destination);
            }
        }

        /// <summary>
        /// Deletes the files next to <paramref name="artworkPath"/> that have its base name and another
        /// supported extension, so a re-export can't leave e.g. "123p.png" beside a new "123p.webp"
        /// </summary>
        private static void DeleteStaleSiblings(string artworkPath)
        {
            string directory = Path.GetDirectoryName(artworkPath) ?? "";
            foreach (string sibling in ArtworkFile.SiblingNames(Path.GetFileName(artworkPath)))
            {
                File.Delete(Path.Combine(directory, sibling));
            }
        }

        /// <summary>The SteamGridDB logo downloaded for the app in this export, in any supported format.</summary>
        private string? DownloadedLogo(string aumid) =>
            ArtworkFile.NamesFor(GridIdPlaceholder + "_logo")
                .Select(name => Path.Combine(TempGridDirectoryFor(aumid), name))
                .FirstOrDefault(File.Exists);

        /// <summary>
        /// Copies <paramref name="source"/> to <c>icons\&lt;grid key&gt;&lt;ext&gt;</c>: one stable name per app,
        /// overwritten on every export. Null when there is nothing to copy or the copy failed.
        /// </summary>
        private string? TryPersistIcon(string aumid, string? source)
        {
            if (String.IsNullOrEmpty(source) || !File.Exists(source))
            {
                return null;
            }

            string destination = Path.Combine(_iconsFolder, GridKey(aumid) + Path.GetExtension(source));
            try
            {
                Directory.CreateDirectory(_iconsFolder);
                File.Copy(source, destination, overwrite: true);
                return destination;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Log.Warning(e, "Could not copy icon {Icon} to {Destination}", source, destination);
                return null;
            }
        }
    }
}
