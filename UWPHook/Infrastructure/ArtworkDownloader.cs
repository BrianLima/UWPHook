using System.IO;
using System.Net.Http;
using UWPHook.Core.Artwork;
using UWPHook.Core.Artwork.SteamGridDb;
using UWPHook.Core.Diagnostics;
using UWPHook.Core.Steam;

namespace UWPHook.Infrastructure
{
    /// <summary>
    /// Downloads each app's SteamGridDB artwork (grid, poster, hero, logo) to its <see cref="TempGridFolder"/>,
    /// a few apps at a time. SteamGridDB errors leave the app without artwork; they never stop the export.
    /// </summary>
    internal sealed class ArtworkDownloader(SteamGridDbService sgdb, TempGridFolder temp, HttpClient images, ILog log)
    {
        /// <summary>At most this many apps download at once (each one makes up to 8 requests).</summary>
        private const int MaxConcurrentDownloads = 4;

        private static readonly string[] FileNames =
        {
            $"{TempGridFolder.Placeholder}.png", $"{TempGridFolder.Placeholder}p.png",
            $"{TempGridFolder.Placeholder}_hero.png", $"{TempGridFolder.Placeholder}_logo.png",
        };

        /// <returns>True when SteamGridDB rejected the API key for any app.</returns>
        public async Task<bool> DownloadAllAsync(IReadOnlyList<ExportApp> apps, CancellationToken ct)
        {
            using var throttle = new SemaphoreSlim(MaxConcurrentDownloads);
            bool[] keyRejected = await Task.WhenAll(apps.Select(async app =>
            {
                await throttle.WaitAsync(ct);
                try
                {
                    log.Debug("Downloading grid images for app {App}", app.Name);
                    return await DownloadAsync(app, ct);
                }
                finally
                {
                    throttle.Release();
                }
            }));
            return keyRejected.Any(rejected => rejected);
        }

        /// <summary>Takes the first search result and the first image of each kind. True when the key was rejected.</summary>
        private async Task<bool> DownloadAsync(ExportApp app, CancellationToken ct)
        {
            string folder = temp.For(app.Aumid);

            // Start from an empty folder, so images left by an earlier, interrupted export are never used
            if (Directory.Exists(folder)) Directory.Delete(folder, true);

            var search = await sgdb.Client.SearchAsync(app.Name, ct);
            if (search.Status == SgdbStatus.InvalidApiKey) return true;
            if (search.Status == SgdbStatus.Failed)
            {
                // 10a: a failed search is worth a Warning, unlike a search that simply found nothing
                log.Information("Searching SteamGridDB for {App} failed ({Result}); it gets no artwork", app.Name, search); // RED stub: the old level
                return false;
            }
            if (!search.IsOk || search.Value.Count == 0)
            {
                log.Information("No SteamGridDB game found for {App} ({Result})", app.Name, search);
                return false;
            }

            var game = search.Value[0];
            log.Debug("Detected game {Name} (SteamGridDB id {Id}) for {App}", game.Name, game.Id, app.Name);
            var results = await ImagesOf(game.Id, sgdb.Filter(), ct);

            CreateFolder(folder);
            await Task.WhenAll(results.Select((result, i) => SaveFirstAsync(result, Path.Combine(folder, FileNames[i]), app)));
            return results.Any(result => result.Status == SgdbStatus.InvalidApiKey);
        }

        private Task<SgdbResult<IReadOnlyList<SgdbImage>>[]> ImagesOf(int gameId, ArtworkFilter filter, CancellationToken ct) =>
            Task.WhenAll(
                sgdb.Client.GetGridsAsync(gameId, filter, "460x215,920x430", ct),
                sgdb.Client.GetGridsAsync(gameId, filter, "600x900,342x482,660x930", ct),
                sgdb.Client.GetHeroesAsync(gameId, filter, null, ct),
                sgdb.Client.GetLogosAsync(gameId, filter, null, ct));

        private Task SaveFirstAsync(SgdbResult<IReadOnlyList<SgdbImage>> result, string destination, ExportApp app)
        {
            if (result.IsOk && result.Value.Count > 0) return SaveImageAsync(result.Value[0].Url, destination);
            if (!result.IsOk) log.Warning("SteamGridDB has no {File} for {App}: {Result}", Path.GetFileName(destination), app.Name, result);
            return Task.CompletedTask;
        }

        private void CreateFolder(string folder)
        {
            try
            {
                Directory.CreateDirectory(folder);
            }
            catch (Exception exception)
            {
                log.Error(exception, "Could not create {Directory}", folder);
                throw new IOException("UWPHook was not able to create the tmp directory for some reason." + Environment.NewLine +
                    "You may solve this by manually creating the following folder:" + Environment.NewLine + folder, exception);
            }
        }

        /// <summary>
        /// Saves the image's bytes untouched (animated images stay animated), with the url's extension.
        /// A failed download is logged and skipped: artwork is optional.
        /// </summary>
        private async Task SaveImageAsync(string imageUrl, string destinationFilename)
        {
            string destination = Path.ChangeExtension(destinationFilename, ArtworkFile.ExtensionFromUrl(imageUrl));
            try
            {
                byte[] bytes = await images.GetByteArrayAsync(imageUrl);
                ArtworkFiles.DeleteStaleSiblings(destination);
                await File.WriteAllBytesAsync(destination, bytes);
            }
            catch (Exception exception)
            {
                log.Error(exception, "Could not save image {Url} to {Destination}", imageUrl, destination);
            }
        }
    }
}
