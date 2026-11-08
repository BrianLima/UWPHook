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
using UWPHook.Properties;
using UWPHook.SteamGridDb;

namespace UWPHook
{
    /// <summary>
    /// Interaction logic for GamesWindow.xaml
    /// </summary>
    public partial class GamesWindow : Window
    {
        AppEntryModel Apps;
        BackgroundWorker bwrLoad;
        static LoggingLevelSwitch levelSwitch = new LoggingLevelSwitch();

        public GamesWindow()
        {
            InitializeComponent();
            Log.Debug("Init GamesWindow");
            Apps = new AppEntryModel();
            var args = Environment.GetCommandLineArgs();

            // Init log file to AppData\Roaming\Briano\UWPHook directory with size rotation on 10Mb with max 5 files
            string loggerFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Briano", "UWPHook", "application.log");

            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(levelSwitch)
            .WriteTo.File(path: loggerFilePath, rollOnFileSizeLimit: true, fileSizeLimitBytes: 10485760, retainedFileCountLimit: 5)
            .WriteTo.Console()
            .CreateLogger();

            // Switch to Info by default to inform logger level in log file and switch to the correct log level
            levelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Information;
            SetLogLevel();

            // If null or 1, the app was launched normally
            if (args?.Length > 1)
            {
                // When length is 1, the only argument is the path where the app is installed
                _ = LauncherAsync(args); // Launches the requested game
            }
            else
            {
                //auto refresh on load
                LoadButton_Click(null, null);
            }
        }

        /// <summary>
        /// We have to wait a little untill Steam catches up, otherwise it will stream a black screen
        /// </summary>
        /// <returns></returns>
        async Task LaunchDelay()
        {
            await Task.Delay(10000);
        }

        /// <summary>
        /// Main task that launches a game
        /// Usually invoked by steam
        /// </summary>
        /// <param name="args">launch args received from the program execution</param>
        /// <returns></returns>
        private async Task LauncherAsync(string[] args)
        {
            FullScreenLauncher launcher = null;
            //So, for some reason, Steam is now stopping in-home streaming if the launched app is minimized, so not hiding UWPHook's window is doing the trick for now
            if (Settings.Default.StreamMode)
            {
                this.Hide();
                launcher = new FullScreenLauncher();
                launcher.Show();

                await LaunchDelay();

                launcher.Close();
            }
            else
            {
                this.Title = "UWPHook: Playing a game";
                this.Hide();
            }

            //Hide the window so the app is launched seamless making UWPHook run in the background without bothering the user
            string currentLanguage = CultureInfo.CurrentCulture.ToString();

            try
            {
                if (!LaunchRequest.TryParse(args, out var request))
                {
                    Log.Error("Invalid launch arguments => " + String.Join(" | ", args));
                    this.Show();
                    MessageBox.Show("Invalid launch arguments", "UWPHook", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                //Some apps have their language locked to the UI language of the system, so overriding it might change the language of the game
                //I my self couldn't get this to work on neither Forza Horizon 3 or Halo 5 Forge, @AbGedreht reported it works tho
                if (Settings.Default.ChangeLanguage && !String.IsNullOrEmpty(Settings.Default.TargetLanguage))
                {
                    ScriptManager.RunScript("Set-WinUILanguageOverride " + Properties.Settings.Default.TargetLanguage);
                }

                AppManager.LaunchUWPApp(request!);

                //While the current launched app is running, sleep for n seconds and then check again
                while (AppManager.IsRunning())
                {
                    Thread.Sleep(Settings.Default.Seconds * 1000);
                }
            }
            catch (Exception e)
            {
                this.Show();
                MessageBox.Show(e.Message, "UWPHook", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                if (Settings.Default.ChangeLanguage && !String.IsNullOrEmpty(Settings.Default.TargetLanguage))
                {
                    ScriptManager.RunScript("Set-WinUILanguageOverride " + currentLanguage);
                }

                //The user has probably finished using the app, so let's close UWPHook to keep the experience clean 
                this.Close();
            }
        }

        /// <summary>
        /// Task responsible for triggering the export, blocks the UI, and shows a message
        /// once the task is finished, unlocking the UI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            grid.IsEnabled = false;
            progressBar.Visibility = Visibility.Visible;

            string? msg = null;

            try
            {
                await ExportGames();

                msg = "Your apps were successfully exported!" + await RestartSteamAsync(
                    notRunning: " They will show up the next time you start Steam.",
                    timedOut: " Please restart Steam to see your games.");
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

            if (msg != null)
            {
                MessageBox.Show(msg, "UWPHook", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Restarts Steam if it is running, so it rereads shortcuts.vdf instead of writing its own copy back
        /// on exit, and describes the outcome as a sentence to append to a message.
        /// </summary>
        /// <param name="notRunning">The sentence for when Steam wasn't running</param>
        /// <param name="timedOut">The sentence for when Steam didn't exit in time</param>
        internal static async Task<string> RestartSteamAsync(string notRunning, string timedOut)
        {
            var restarter = new SteamRestarter(new Infrastructure.WindowsSteamProcess(), TimeProvider.System,
                timeout: TimeSpan.FromSeconds(15), poll: TimeSpan.FromMilliseconds(500));
            var outcome = await restarter.RestartAsync();
            Log.Information("Steam restart outcome: {Outcome}", outcome);

            return outcome switch
            {
                RestartOutcome.Restarted => " Steam has been restarted.",
                RestartOutcome.NotRunning => notRunning,
                _ => timedOut,
            };
        }

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
        /// in place of the appid (renamed when copied to each user),
        /// Throws TaskCanceledException if cannot communicate with SteamGridDB properly
        /// </summary>
        /// <param name="appName">The name of the app</param>
        /// <param name="gridKey">The app's grid key, see <see cref="GridKey"/></param>
        /// <returns></returns>
        private async Task DownloadTempGridImages(string appName, string gridKey)
        {
            SteamGridDbApi api = new SteamGridDbApi(Properties.Settings.Default.SteamGridDbApiKey);
            string tmpGridDirectory = TempGridDirectoryFor(gridKey);
            GameResponse[] games = null;

            // Start from an empty folder, so images left by an earlier, interrupted export are never used
            if (Directory.Exists(tmpGridDirectory))
            {
                Directory.Delete(tmpGridDirectory, true);
            }

            try
            {
                games = await api.SearchGame(appName);
            }
            catch (TaskCanceledException exception)
            {
                Log.Error(exception.Message);
            }

            if (games != null)
            {
                var game = games[0];
                Log.Verbose("Detected Game: " + game.ToString());
                string gameId = GridIdPlaceholder;

                SafellyCreateTmpGridDirectory(tmpGridDirectory);

                var gameGridsVertical = api.GetGameGrids(game.Id, "600x900,342x482,660x930");
                var gameGridsHorizontal = api.GetGameGrids(game.Id, "460x215,920x430");
                var gameHeroes = api.GetGameHeroes(game.Id);
                var gameLogos = api.GetGameLogos(game.Id);

                Log.Verbose("Game ID: " + game.Id);

                await Task.WhenAll(
                    gameGridsVertical,
                    gameGridsHorizontal,
                    gameHeroes,
                    gameLogos
                );

                var gridsVertical = await gameGridsVertical;
                var gridsHorizontal = await gameGridsHorizontal;
                var heroes = await gameHeroes;
                var logos = await gameLogos;

                List<Task> saveImagesTasks = new List<Task>();

                if (gridsHorizontal != null && gridsHorizontal.Length > 0)
                {
                    var grid = gridsHorizontal[0];
                    saveImagesTasks.Add(SaveImage(grid.Url, $"{tmpGridDirectory}\\{gameId}.png"));
                }

                if (gridsVertical != null && gridsVertical.Length > 0)
                {
                    var grid = gridsVertical[0];
                    saveImagesTasks.Add(SaveImage(grid.Url, $"{tmpGridDirectory}\\{gameId}p.png"));
                }

                if (heroes != null && heroes.Length > 0)
                {
                    var hero = heroes[0];
                    saveImagesTasks.Add(SaveImage(hero.Url, $"{tmpGridDirectory}\\{gameId}_hero.png"));
                }

                if (logos != null && logos.Length > 0)
                {
                    var logo = logos[0];
                    saveImagesTasks.Add(SaveImage(logo.Url, $"{tmpGridDirectory}\\{gameId}_logo.png"));
                }

                await Task.WhenAll(saveImagesTasks);
            }
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
        /// <returns></returns>
        private async Task<bool> ExportGames()
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

        private async Task<bool> ExportSelectedApps()
        {
            string[] tags = Settings.Default.Tags.Split(',');
            string steam_folder = SteamManager.GetSteamFolder();

            if (Directory.Exists(steam_folder))
            {
                var users = SteamManager.GetUsers(steam_folder);
                // One entry per AUMID, so two downloads never share a temp grid folder
                var selected_apps = Apps.Entries.Where(app => app.Selected)
                    .DistinctBy(app => app.Aumid, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                string exePath = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot find the path of UWPHook.exe");
                var gridIdsByUser = new Dictionary<string, Dictionary<string, uint>>();

                List<Task> gridImagesDownloadTasks = new List<Task>();
                bool downloadGridImages = !String.IsNullOrEmpty(Properties.Settings.Default.SteamGridDbApiKey);
                //To make things faster, decide icons and download grid images before looping users
                Log.Verbose("downloadGridImages: " + (downloadGridImages));

                foreach (var app in selected_apps)
                {
                    app.Icon = app.widestSquareIcon();

                    if (downloadGridImages)
                    {
                        Log.Verbose("Downloading grid images for app " + app.Name);

                        gridImagesDownloadTasks.Add(DownloadTempGridImages(app.Name, GridKey(app.Aumid)));
                    }
                }

                await Task.WhenAll(gridImagesDownloadTasks);

                // Export the selected apps and the downloaded images to each user
                // in the steam folder by modifying it's VDF file
                foreach (var user in users)
                {
                    try
                    {
                        var wanted = new List<ShortcutSpec>();
                        foreach (var app in selected_apps)
                        {
                            try
                            {
                                app.Icon = PersistAppIcon(app);
                                Log.Verbose("Defaulting to app.Icon for app " + app.Name);
                            }
                            catch (System.IO.IOException)
                            {
                                Log.Verbose("Using backup icon for app " + app.Name);

                                await Task.Run(() =>
                                {
                                    string tmpGridDirectory = TempGridDirectoryFor(GridKey(app.Aumid));

                                    SafellyCreateTmpGridDirectory(tmpGridDirectory);

                                    // Any supported extension; a missing logo makes PersistAppIcon keep app.Icon
                                    string[] logos = Core.Artwork.ArtworkFile.NamesFor(GridIdPlaceholder + "_logo")
                                        .Select(name => tmpGridDirectory + name).ToArray();
                                    string logo = logos.FirstOrDefault(File.Exists) ?? logos[0];
                                    app.Icon = PersistAppIcon(app, logo);
                                });
                            }

                            wanted.Add(ShortcutSpec.ForApp(app.Name, app.Aumid, app.Executable, exePath, app.Icon, tags));
                        }

                        // Update UWPHook's own shortcuts in place (keeping their appid), never touch anyone else's.
                        // A shortcuts.vdf that can't be read throws here and is left as it is.
                        MergeResult merge = await Task.Run(() => new Infrastructure.SteamUserShortcuts(user, BackupFolder).Export(wanted));
                        gridIdsByUser[user] = GridIdsByKey(merge);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error: Program failed exporting your games to {User}", user);
                        MessageBox.Show("Error: Program failed exporting your games:" + Environment.NewLine + ex.Message + ex.StackTrace);
                    }
                }

                await Task.Run(() =>
                {
                    foreach (var (user, gridIdsByKey) in gridIdsByUser)
                    {
                        CopyTempGridImagesToSteamUser(user, gridIdsByKey);
                    }
                });
            }

            return true;
        }

        /// <summary>
        /// Copies an apps icon to a intermediate location
        /// Due to some apps changing the icon location when they update, which causes icons to be "lost"
        /// </summary>
        /// <param name="app">App to copy the icon to</param>
        /// <param name="forcedIcon">Overwrites the app.icon to be copied</param>
        /// <returns>string, the path to the usable and persisted icon</returns>
        private string PersistAppIcon(AppEntry app, string forcedIcon = "")
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string icons_path = path + @"\Briano\UWPHook\icons\";

            // If we do not have an specific icon to copy, copy app.icon, if we do, copy the specified icon
            string icon_to_copy = String.IsNullOrEmpty(forcedIcon) ? app.Icon : forcedIcon;

            if (!Directory.Exists(icons_path))
            {
                Directory.CreateDirectory(icons_path);
            }

            string dest_file = String.Join(String.Empty, icons_path, app.Aumid + Path.GetFileName(icon_to_copy));
            try
            {
                if (File.Exists(icon_to_copy))
                {
                    File.Copy(icon_to_copy, dest_file, true);
                }
                else
                {
                    dest_file = app.Icon;
                }
            }
            catch (System.IO.IOException e)
            {
                Log.Warning(e, "Could not copy icon " + app.Icon);
                throw e;
            }

            return dest_file;
        }

        /// <summary>
        /// Removes the shortcuts UWPHook created from every Steam user, backing up each changed
        /// shortcuts.vdf first. Shortcuts created by Steam, other tools or by hand are kept.
        /// </summary>
        public static async Task RemoveUwpHookShortcuts()
        {
            Log.Debug("Removing UWPHook's shortcuts from shortcuts.vdf");
            string steam_folder = SteamManager.GetSteamFolder();

            if (Directory.Exists(steam_folder))
            {
                int removed = 0;
                foreach (var user in SteamManager.GetUsers(steam_folder))
                {
                    try
                    {
                        removed += await Task.Run(() => new Infrastructure.SteamUserShortcuts(user, BackupFolder).RemoveUwpHookShortcuts());
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error: Program failed while trying to remove UWPHook's shortcuts from {User}", user);
                        MessageBox.Show("Error: Program failed while trying to remove UWPHook's shortcuts:" + Environment.NewLine + ex.Message + ex.StackTrace);
                    }
                }

                if (removed == 0)
                {
                    MessageBox.Show("No UWPHook shortcuts were found, so nothing was changed.", "UWPHook", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // A running Steam would write the removed shortcuts back when it exits
                string msg = removed + " UWPHook shortcut(s) removed. Shortcuts from Steam and other tools were kept." + await RestartSteamAsync(
                    notRunning: " The changes apply the next time you start Steam.",
                    timedOut: " Please restart Steam now, or it may restore the removed shortcuts.");
                MessageBox.Show(msg, "UWPHook", MessageBoxButton.OK, MessageBoxImage.Information);
            }
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
                Log.Error(ex.Message);
                MessageBox.Show(ex.Message, "UWPHook", MessageBoxButton.OK, MessageBoxImage.Error);
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
            if (!Settings.Default.OfferedSteamGridDB)
            {
                Settings.Default.SteamGridDbApiKey = "";
                Settings.Default.OfferedSteamGridDB = true;
                Settings.Default.Save();

                var boxResult = MessageBox.Show("Do you want to automatically import grid images for imported games?", "UWPHook", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (boxResult == MessageBoxResult.Yes)
                {
                    SettingsButton_Click(this, null);
                }
            }
        }

        public static void SetLogLevel()
        {
            int logLevel_index;
            int.TryParse(Settings.Default.SelectedLogLevel, out logLevel_index);

            switch (logLevel_index)
            {
                case 1:
                    Log.Information("Init log with DEBUG level.");
                    levelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Debug;
                    break;
                case 2:
                    Log.Information("Init log with TRACE level.");
                    levelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Verbose;
                    break;
                default:
                    Log.Information("Init log with ERROR level.");
                    levelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Error;
                    break;
            }
        }
    }
}
