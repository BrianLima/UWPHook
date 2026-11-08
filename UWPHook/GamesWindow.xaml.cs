using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using UWPHook.Core.Launch;
using UWPHook.Core.Steam;
using UWPHook.Infrastructure;
using UWPHook.Core.Artwork.SteamGridDb;

namespace UWPHook
{
    /// <summary>
    /// Interaction logic for GamesWindow.xaml
    /// </summary>
    public partial class GamesWindow : Window
    {
        AppEntryModel Apps;
        BackgroundWorker bwrLoad;

        public GamesWindow()
        {
            InitializeComponent();
            Log.Debug("Init GamesWindow");
            Apps = new AppEntryModel();

            //auto refresh on load
            LoadButton_Click(null, null);
        }

        /// <summary>
        /// Exports the selected apps with the UI locked, then shows what really happened
        /// (see <see cref="ExportSummary"/>): a success message only when every Steam account was written.
        /// </summary>
        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            grid.IsEnabled = false;
            progressBar.Visibility = Visibility.Visible;

            ExportSummary? summary = null;

            try
            {
                summary = await ExportGames();
                Log.Information("Export finished: {Summary}", summary);
            }
            catch (Exception exception)
            {
                Infrastructure.ErrorReporter.Report(exception, "Exporting your games failed.");
            }
            finally
            {
                grid.IsEnabled = true;
                progressBar.Visibility = Visibility.Collapsed;
            }

            if (summary != null)
            {
                MessageBox.Show(summary.Message, "UWPHook", MessageBoxButton.OK, ImageFor(summary.Severity));
            }
        }

        private static MessageBoxImage ImageFor(MessageSeverity severity) => severity switch
        {
            MessageSeverity.Error => MessageBoxImage.Error,
            MessageSeverity.Warning => MessageBoxImage.Warning,
            _ => MessageBoxImage.Information,
        };

        /// <summary>
        /// Stops and starts the Steam that <see cref="SteamManager"/> found. Steam may write its own copy of
        /// shortcuts.vdf when it exits, so shortcuts are written while it is closed (stop, write, start), never
        /// before a restart.
        /// </summary>
        private static SteamRestarter NewSteamRestarter() =>
            new SteamRestarter(new Infrastructure.WindowsSteamProcess(), TimeProvider.System,
                timeout: TimeSpan.FromSeconds(15), poll: TimeSpan.FromMilliseconds(500));

        /// <summary>At most this many apps download SteamGridDB artwork at once (each one makes up to 8 requests).</summary>
        private const int MaxConcurrentArtworkDownloads = 4;

        private static readonly System.Net.Http.HttpClient imageClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        /// <summary>
        /// Downloads the image at the url and saves its bytes untouched (animated images stay animated).
        /// The extension of <paramref name="destinationFilename"/> is replaced with the one from the url.
        /// A failed download is logged and skipped: artwork is optional.
        /// </summary>
        /// <param name="imageUrl">The url for the image</param>
        /// <param name="destinationFilename">Path to store the image, ending in .png</param>
        /// <returns></returns>
        private async Task SaveImage(string imageUrl, string destinationFilename)
        {
            string destination = Path.ChangeExtension(destinationFilename, Core.Artwork.ArtworkFile.ExtensionFromUrl(imageUrl));

            try
            {
                byte[] bytes = await imageClient.GetByteArrayAsync(imageUrl);
                DeleteStaleArtworkSiblings(destination);
                await File.WriteAllBytesAsync(destination, bytes);
            }
            catch (Exception exception)
            {
                Log.Error(exception, "Could not save image {Url} to {Destination}", imageUrl, destination);
            }
        }

        /// <summary>
        /// Copies the temporary images of each app to the given user, renaming them to the
        /// appid that app's shortcut has in this user's shortcuts.vdf
        /// </summary>
        /// <param name="user">The user path to copy images to</param>
        /// <param name="gridIdsByKey">The grid id (unsigned appid) of each app's shortcut, by grid key</param>
        private void CopyTempGridImagesToSteamUser(string user, IReadOnlyDictionary<string, uint> gridIdsByKey)
        {
            string userGridDirectory = user + "\\config\\grid\\";

            foreach (var (gridKey, gridId) in gridIdsByKey)
            {
                string appGridDirectory = TempGridDirectoryFor(gridKey);

                // No images were downloaded, maybe the key is invalid or the app had no image
                if (!Directory.Exists(appGridDirectory))
                {
                    continue;
                }

                if (!Directory.Exists(userGridDirectory))
                {
                    Directory.CreateDirectory(userGridDirectory);
                }

                foreach (string image in Directory.GetFiles(appGridDirectory, GridIdPlaceholder + "*"))
                {
                    // e.g. "appid_hero.png" -> "3144859588_hero.png", keeping whatever extension was saved
                    string destFile = userGridDirectory + gridId + Path.GetFileName(image).Substring(GridIdPlaceholder.Length);
                    try
                    {
                        // Copy first, so a failed copy never leaves the user without the old art
                        File.Copy(image, destFile, true);
                        DeleteStaleArtworkSiblings(destFile);
                    }
                    catch (Exception exception)
                    {
                        // One locked file must not stop the remaining apps and users
                        Log.Error(exception, "Could not copy artwork {Image} to {Destination}", image, destFile);
                    }
                }
            }
        }

        /// <summary>
        /// Deletes the files next to <paramref name="artworkPath"/> that have its base name and another
        /// supported extension, so a re-export can't leave e.g. "123p.png" beside a new "123p.webp"
        /// </summary>
        private static void DeleteStaleArtworkSiblings(string artworkPath)
        {
            string directory = Path.GetDirectoryName(artworkPath) ?? "";
            foreach (string sibling in Core.Artwork.ArtworkFile.SiblingNames(Path.GetFileName(artworkPath)))
            {
                File.Delete(Path.Combine(directory, sibling));
            }
        }

        /// <summary>
        /// Temporary grid images are named with this placeholder instead of the appid,
        /// because the appid is only known per user, after merging into shortcuts.vdf
        /// </summary>
        private const string GridIdPlaceholder = "appid";

        /// <summary>
        /// A stable, file-name-safe key for an app's temporary grid images: its AUMID
        /// </summary>
        private static string GridKey(string aumid)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            return new string(aumid.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        }

        private static string TempGridDirectoryFor(string gridKey) =>
            Path.GetTempPath() + "UWPHook\\tmp_grid\\" + gridKey + "\\";

        /// <summary>
        /// The grid id (unsigned appid) of every shortcut UWPHook wrote, by grid key
        /// </summary>
        private static Dictionary<string, uint> GridIdsByKey(MergeResult merge)
        {
            var gridIds = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
            foreach (var merged in merge.Shortcuts.Where(merged => merged.Spec != null))
            {
                gridIds[GridKey(merged.Spec!.Aumid)] = unchecked((uint)merged.AppId);
            }

            return gridIds;
        }

        private void RemoveTempGridImages()
        {
            string tmpGridDirectory = Path.GetTempPath() + "UWPHook\\tmp_grid\\";
            try
            {
                if (Directory.Exists(tmpGridDirectory))
                {
                    Directory.Delete(tmpGridDirectory, true);
                }
            }
            catch (Exception exception)
            {
                // Runs in a finally: a leftover temp folder must not hide the export's own outcome
                Log.Warning(exception, "Could not remove the temporary grid images in {Directory}", tmpGridDirectory);
            }
        }

        /// <summary>
        /// Task responsible for downloading grid images to a temporary location,
        /// in a folder per app named after <paramref name="gridKey"/>, with <see cref="GridIdPlaceholder"/>
        /// in place of the appid (renamed when copied to each user).
        /// Takes the first search result and the first image of each kind. SteamGridDB errors are
        /// logged and leave the app without artwork; they never stop the export.
        /// </summary>
        /// <param name="appName">The name of the app</param>
        /// <param name="gridKey">The app's grid key, see <see cref="GridKey"/></param>
        /// <returns>True when SteamGridDB rejected the API key</returns>
        private async Task<bool> DownloadTempGridImages(string appName, string gridKey)
        {
            SteamGridDbClient api = Infrastructure.SteamGridDbService.Client;
            ArtworkFilter filter = Infrastructure.SteamGridDbService.FilterFromSettings();
            CancellationToken ct = CancellationToken.None;
            string tmpGridDirectory = TempGridDirectoryFor(gridKey);

            // Start from an empty folder, so images left by an earlier, interrupted export are never used
            if (Directory.Exists(tmpGridDirectory))
            {
                Directory.Delete(tmpGridDirectory, true);
            }

            var search = await api.SearchAsync(appName, ct);
            if (search.Status == SgdbStatus.InvalidApiKey)
            {
                return true;
            }
            if (!search.IsOk || search.Value.Count == 0)
            {
                Log.Information("No SteamGridDB game found for {App} ({Result})", appName, search);
                return false;
            }

            var game = search.Value[0];
            Log.Verbose("Detected game {Name} (SteamGridDB id {Id}) for {App}", game.Name, game.Id, appName);
            string gameId = GridIdPlaceholder;

            var results = await Task.WhenAll(
                api.GetGridsAsync(game.Id, filter, "460x215,920x430", ct),
                api.GetGridsAsync(game.Id, filter, "600x900,342x482,660x930", ct),
                api.GetHeroesAsync(game.Id, filter, null, ct),
                api.GetLogosAsync(game.Id, filter, null, ct));

            string[] fileNames = { $"{gameId}.png", $"{gameId}p.png", $"{gameId}_hero.png", $"{gameId}_logo.png" };

            SafellyCreateTmpGridDirectory(tmpGridDirectory);

            List<Task> saveImagesTasks = new List<Task>();
            for (int i = 0; i < results.Length; i++)
            {
                if (results[i].IsOk && results[i].Value.Count > 0)
                {
                    saveImagesTasks.Add(SaveImage(results[i].Value[0].Url, $"{tmpGridDirectory}\\{fileNames[i]}"));
                }
                else if (!results[i].IsOk)
                {
                    Log.Warning("SteamGridDB has no {File} for {App}: {Result}", fileNames[i], appName, results[i]);
                }
            }

            await Task.WhenAll(saveImagesTasks);

            return results.Any(result => result.Status == SgdbStatus.InvalidApiKey);
        }

        private void SafellyCreateTmpGridDirectory(string tmpGridDirectory)
        {
            if (!Directory.Exists(tmpGridDirectory))
            {
                try
                {
                    Directory.CreateDirectory(tmpGridDirectory);
                    Log.Information("Created directory: " + tmpGridDirectory);
                }
                catch (Exception exception)
                {
                    Log.Error(exception.Message);
                    Log.Error(exception.StackTrace);
                    throw new Exception("UWPHook was not able to create the tmp directory for some reason." + Environment.NewLine +
                        "You may solve this by manually creating the following folder:" + Environment.NewLine +
                        tmpGridDirectory);
                }
            }
        }

        /// <summary>
        /// Main Task to export the selected games to steam. The temporary grid images are removed
        /// afterwards even when the export fails.
        /// </summary>
        private async Task<ExportSummary> ExportGames()
        {
            try
            {
                return await ExportSelectedApps();
            }
            finally
            {
                await Task.Run(() => RemoveTempGridImages());
            }
        }

        private static string BackupFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Briano", "UWPHook", "backups");

        private static string IconsFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Briano", "UWPHook", "icons");

        /// <summary>
        /// Checks there is something to do before touching Steam, prepares the artwork and icons once,
        /// then writes every Steam account's shortcuts.vdf and grid images while Steam is closed.
        /// </summary>
        private async Task<ExportSummary> ExportSelectedApps()
        {
            // One entry per AUMID, so two downloads never share a temp grid folder
            var selectedApps = Apps.Entries.Where(app => app.Selected)
                .DistinctBy(app => app.Aumid, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (selectedApps.Count == 0)
            {
                return ExportSummary.NothingSelected();
            }

            string? steamFolder = SteamManager.GetSteamFolder();
            if (steamFolder is null)
            {
                Log.Warning("Steam was not found in the registry (HKCU SteamPath, HKLM InstallPath); nothing was exported");
                return ExportSummary.SteamNotFound();
            }

            string[] users = SteamManager.GetUsers(steamFolder);
            if (users.Length == 0)
            {
                Log.Warning("No Steam account folder in {SteamFolder}\\userdata; nothing was exported", steamFolder);
                return ExportSummary.ForUsers(written: 0, failed: 0, StopOutcome.NotRunning);
            }

            string[] tags = UserSettings.Current.Tags.Split(',');
            string exePath = UWPHook.Core.Install.InstallLayout.StableExePath(Environment.ProcessPath ?? throw new InvalidOperationException("Cannot find the path of UWPHook.exe"));

            await DownloadArtwork(selectedApps);

            // The same shortcuts go to every user, so each icon is persisted once, not once per user
            List<ShortcutSpec> wanted = await Task.Run(() => selectedApps
                .Select(app => ShortcutSpec.ForApp(app.Name, app.Aumid, app.Executable, exePath, PersistedIconFor(app), tags))
                .ToList());

            int written = 0;
            int failed = 0;
            var artworkFailedUsers = new List<string>();
            StopOutcome steam = await NewSteamRestarter().WhileStoppedAsync(async () =>
            {
                var gridIdsByUser = new Dictionary<string, Dictionary<string, uint>>();
                foreach (var user in users)
                {
                    try
                    {
                        // Update UWPHook's own shortcuts in place (keeping their appid), never touch anyone else's.
                        // A shortcuts.vdf that can't be read throws here and is left as it is.
                        MergeResult merge = await Task.Run(() => new Infrastructure.SteamUserShortcuts(user, BackupFolder).Export(wanted));
                        gridIdsByUser[user] = GridIdsByKey(merge);
                        written++;
                    }
                    catch (Exception ex)
                    {
                        // Counted in the summary, which points to the log; one box per user would bury it
                        failed++;
                        Log.Error(ex, "Exporting your games to {User} failed", user);
                    }
                }

                await Task.Run(() =>
                {
                    foreach (var (user, gridIdsByKey) in gridIdsByUser)
                    {
                        try
                        {
                            CopyTempGridImagesToSteamUser(user, gridIdsByKey);
                        }
                        catch (Exception ex)
                        {
                            // The shortcuts are already written: a note in the summary, not a failed export
                            artworkFailedUsers.Add(Path.GetFileName(user));
                            Log.Error(ex, "Could not copy the artwork to {User}", user);
                        }
                    }
                });
            });

            if (steam is StopOutcome.StillRunning or StopOutcome.ExecutableUnknown)
            {
                Log.Warning("Steam is still running ({Outcome}); nothing was exported", steam);
                return ExportSummary.SteamNotStopped(steam);
            }

            return ExportSummary.ForUsers(written, failed, steam).WithArtworkFailures(artworkFailedUsers);
        }

        /// <summary>
        /// Downloads each app's SteamGridDB artwork to its temp grid folder, a few apps at a time so a large
        /// selection doesn't hit SteamGridDB with hundreds of requests at once. Does nothing without an API key.
        /// </summary>
        private async Task DownloadArtwork(IReadOnlyList<AppEntry> apps)
        {
            bool downloadGridImages = UserSettings.Current.HasSteamGridDbApiKey;
            Log.Verbose("downloadGridImages: " + (downloadGridImages));
            if (!downloadGridImages)
            {
                return;
            }

            using var throttle = new SemaphoreSlim(MaxConcurrentArtworkDownloads);
            bool[] keyRejected = await Task.WhenAll(apps.Select(async app =>
            {
                await throttle.WaitAsync();
                try
                {
                    Log.Verbose("Downloading grid images for app " + app.Name);
                    return await DownloadTempGridImages(app.Name, GridKey(app.Aumid));
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
                MessageBox.Show(Infrastructure.SteamGridDbService.InvalidKeyMessage, "UWPHook", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// The icon for an app's shortcut, copied to the icons folder because some apps move their files when
        /// they update, which "loses" the icon. The app's widest square logo is used, else the SteamGridDB logo
        /// downloaded for it; when neither can be copied, the app's own path (possibly empty) is kept.
        /// </summary>
        private static string PersistedIconFor(AppEntry app)
        {
            string source = app.widestSquareIcon();
            return TryPersistIcon(app.Aumid, source)
                ?? TryPersistIcon(app.Aumid, DownloadedLogo(app.Aumid))
                ?? source;
        }

        /// <summary>The SteamGridDB logo downloaded for the app in this export, in any supported format.</summary>
        private static string? DownloadedLogo(string aumid)
        {
            string tmpGridDirectory = TempGridDirectoryFor(GridKey(aumid));
            return Core.Artwork.ArtworkFile.NamesFor(GridIdPlaceholder + "_logo")
                .Select(name => tmpGridDirectory + name)
                .FirstOrDefault(File.Exists);
        }

        /// <summary>
        /// Copies <paramref name="source"/> to <c>icons\&lt;grid key&gt;&lt;ext&gt;</c>: one stable name per app,
        /// overwritten on every export. Null when there is nothing to copy or the copy failed.
        /// </summary>
        private static string? TryPersistIcon(string aumid, string? source)
        {
            if (String.IsNullOrEmpty(source) || !File.Exists(source))
            {
                return null;
            }

            string destination = Path.Combine(IconsFolder, GridKey(aumid) + Path.GetExtension(source));
            try
            {
                Directory.CreateDirectory(IconsFolder);
                File.Copy(source, destination, overwrite: true);
                return destination;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Log.Warning(e, "Could not copy icon {Icon} to {Destination}", source, destination);
                return null;
            }
        }

        /// <summary>
        /// Removes the shortcuts UWPHook created from every Steam user, backing up each changed
        /// shortcuts.vdf first. Shortcuts created by Steam, other tools or by hand are kept.
        /// Steam is only closed (and started again) when there is something to remove.
        /// </summary>
        public static async Task RemoveUwpHookShortcuts()
        {
            Log.Debug("Removing UWPHook's shortcuts from shortcuts.vdf");
            string? steamFolder = SteamManager.GetSteamFolder();
            if (steamFolder is null)
            {
                MessageBox.Show("Steam was not found, so nothing was changed.", "UWPHook", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string[] users = SteamManager.GetUsers(steamFolder);
            var (found, unreadable) = await Task.Run(() => CountUwpHookShortcuts(users));
            if (found == 0)
            {
                string nothing = "No UWPHook shortcuts were found, so nothing was changed.";
                if (unreadable > 0)
                {
                    nothing += $" The shortcuts of {unreadable} Steam account(s) could not be read; details are in application.log.";
                }
                MessageBox.Show(nothing, "UWPHook", MessageBoxButton.OK, unreadable > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
                return;
            }

            int removed = 0;
            int failed = 0;
            StopOutcome steam = await NewSteamRestarter().WhileStoppedAsync(async () =>
            {
                foreach (var user in users)
                {
                    try
                    {
                        removed += await Task.Run(() => new Infrastructure.SteamUserShortcuts(user, BackupFolder).RemoveUwpHookShortcuts());
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        Log.Error(ex, "Removing UWPHook's shortcuts from {User} failed", user);
                    }
                }
            });

            if (steam is StopOutcome.StillRunning or StopOutcome.ExecutableUnknown)
            {
                string why = steam == StopOutcome.StillRunning
                    ? "Steam did not close in time"
                    : "Steam is running, but UWPHook could not find steam.exe to close and restart it";
                MessageBox.Show(why + ", so nothing was removed. Close Steam, then try again.", "UWPHook", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (removed == 0)
            {
                MessageBox.Show($"Removing UWPHook's shortcuts failed for {failed} Steam account(s), so nothing was changed. Details are in application.log.", "UWPHook", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string msg = removed + " UWPHook shortcut(s) removed. Shortcuts from Steam and other tools were kept.";
            if (failed > 0)
            {
                msg += $" Removing failed for {failed} Steam account(s); details are in application.log.";
            }
            msg += steam == StopOutcome.Stopped
                ? " Steam has been restarted."
                : " The changes apply the next time you start Steam.";
            MessageBox.Show(msg, "UWPHook", MessageBoxButton.OK, failed > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }

        /// <summary>How many UWPHook shortcuts the users have, and how many users' files could not be read.</summary>
        private static (int Found, int Unreadable) CountUwpHookShortcuts(IEnumerable<string> users)
        {
            int found = 0;
            int unreadable = 0;
            foreach (var user in users)
            {
                try
                {
                    found += new Infrastructure.SteamUserShortcuts(user, BackupFolder).CountUwpHookShortcuts();
                }
                catch (Exception ex)
                {
                    unreadable++;
                    Log.Error(ex, "Could not read the shortcuts of {User}", user);
                }
            }

            return (found, unreadable);
        }
        /// <summary>
        /// Fires the Bwr_DoWork, to load the apps installed at the machine
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            bwrLoad = new BackgroundWorker();
            bwrLoad.DoWork += Bwr_DoWork;
            bwrLoad.RunWorkerCompleted += Bwr_RunWorkerCompleted;

            grid.IsEnabled = false;
            label.Content = "Loading your installed apps";

            progressBar.Visibility = Visibility.Visible;
            Apps.Entries = new System.Collections.ObjectModel.ObservableCollection<AppEntry>();

            bwrLoad.RunWorkerAsync();
        }

        /// <summary>
        /// Callback for restoring the grid list interactivity
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Bwr_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            listGames.ItemsSource = Apps.Entries;

            listGames.Columns[2].IsReadOnly = true;
            listGames.Columns[3].IsReadOnly = true;

            grid.IsEnabled = true;
            progressBar.Visibility = Visibility.Collapsed;
            label.Content = "Installed Apps";
        }

        /// <summary>
        /// Worker responsible for loading the apps installed in the machine
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Bwr_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                // The 2.14 script listed every non-framework package, system apps included, and the UI has no
                // "hide system apps" option, so keep listing them all.
                Core.Catalog.IAppCatalog catalog = new Infrastructure.PackageManagerCatalog();
                var installedApps = catalog.GetInstalledAppsAsync(includeSystem: true, CancellationToken.None).GetAwaiter().GetResult();

                foreach (var app in installedApps.OrderBy(a => a.DisplayName, StringComparer.CurrentCultureIgnoreCase))
                {
                    AppEntry entry = Infrastructure.XboxAppEntries.ToAppEntry(app);
                    Application.Current.Dispatcher.BeginInvoke((Action)delegate ()
                    {
                        Apps.Entries.Add(entry);
                    });
                }
            }
            catch (Exception ex)
            {
                // Logs the whole exception and shows the box on the UI thread, not this worker's
                Infrastructure.ErrorReporter.Report(ex, "Loading your installed apps failed.");
            }
        }

        private void textBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (Apps.Entries != null)
            {
                if (!String.IsNullOrEmpty(textBox.Text) && Apps.Entries.Count > 0)
                {
                    listGames.Items.Filter = new Predicate<object>(Contains);
                }
                else
                {
                    listGames.Items.Filter = null;
                }
            }
        }

        public bool Contains(object o)
        {
            AppEntry appEntry = o as AppEntry;
            return (appEntry.Aumid.ToLower().Contains(textBox.Text.ToLower()) || appEntry.Name.ToLower().Contains(textBox.Text.ToLower()));
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow window = new SettingsWindow();
            window.ShowDialog();
        }

        /// <summary>
        /// Function that executes when the Games Window is loaded
        /// Will inform the user of the possibility of using the SteamGridDB API
        /// redirecting him to the settings page if he wishes to use the functionality
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!UserSettings.Current.OfferedSteamGridDb)
            {
                // Only offer: a key the user already entered is kept.
                try
                {
                    UserSettings.Update(settings => settings with { OfferedSteamGridDb = true });
                }
                catch (Exception exception)
                {
                    Log.Error(exception, "Saving that SteamGridDB was offered failed");
                }

                var boxResult = MessageBox.Show("Do you want to automatically import grid images for imported games?", "UWPHook", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (boxResult == MessageBoxResult.Yes)
                {
                    SettingsButton_Click(this, null);
                }
            }
        }

        /// <summary>Applies the log level picked in Settings.</summary>
        public static void SetLogLevel() => Infrastructure.AppLog.ApplySelectedLevel();
    }
}
