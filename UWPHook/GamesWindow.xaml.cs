using Force.Crc32;
using SharpSteam;
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
using UWPHook.Properties;
using UWPHook.SteamGridDb;
using VDFParser;
using VDFParser.Models;

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
            Debug.WriteLine("Init GamesWindow");
            Apps = new AppEntryModel();
            var args = Environment.GetCommandLineArgs();

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
                //Some apps have their language locked to the UI language of the system, so overriding it might change the language of the game
                //I my self couldn't get this to work on neither Forza Horizon 3 or Halo 5 Forge, @AbGedreht reported it works tho
                if (Settings.Default.ChangeLanguage && !String.IsNullOrEmpty(Settings.Default.TargetLanguage))
                {
                    ScriptManager.RunScript("Set-WinUILanguageOverride " + Properties.Settings.Default.TargetLanguage);
                }

                //The only other parameter Steam will send is the app AUMID
                AppManager.LaunchUWPApp(args);

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
        /// Generates a CRC32 hash expected by Steam to link an image with a game in the library
        /// See https://blog.yo1.dog/calculate-id-for-non-steam-games-js/ for an example
        /// </summary>
        /// <param name="appName">The name of the executable to be displayed</param>
        /// <param name="appTarget">The executable target path</param>
        /// <returns></returns>
        private UInt64 GenerateSteamGridAppId(string appName, string appTarget)
        {
            byte[] nameTargetBytes = Encoding.UTF8.GetBytes(appTarget + appName + "");
            UInt64 crc = Crc32Algorithm.Compute(nameTargetBytes);
            UInt64 gameId = crc | 0x80000000;

            return gameId;
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

            bool result = false, restartSteam = true;
            string msg = String.Empty;

            try
            {
                await ExportGames(restartSteam);

                msg = "Your apps were successfuly exported!";
                if(!restartSteam)
                {
                    msg += " Please restart Steam in order to see them.";
                }
                else if(result)
                {
                    msg += " Steam has been restarted.";
                }

            }
            catch (TaskCanceledException exception)
            {
                msg = exception.Message;
            }

            grid.IsEnabled = true;
            progressBar.Visibility = Visibility.Collapsed;

            MessageBox.Show(msg, "UWPHook", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Downloads the given image in the url to a given path in a given format
        /// </summary>
        /// <param name="imageUrl">The url for the image</param>
        /// <param name="destinationFilename">Path to store the image</param>
        /// <param name="format"></param>
        /// <returns></returns>
        private async Task SaveImage(string imageUrl, string destinationFilename, ImageFormat format)
        {
            await Task.Run(() =>
            {
                WebClient client = new WebClient();
                Stream stream = null;
                try
                {
                    stream = client.OpenRead(imageUrl);
                }
                catch (Exception e)
                {
                    //Image with error?
                    //Skip for now
                }

                if (stream != null)
                {
                    Bitmap bitmap; bitmap = new Bitmap(stream);
                    bitmap.Save(destinationFilename, format);
                    stream.Flush();
                    stream.Close();
                    client.Dispose();
                }
            });
        }

        /// <summary>
        /// Copies all temporary images to the given user
        /// </summary>
        /// <param name="user">The user path to copy images to</param>
        private void CopyTempGridImagesToSteamUser(string user)
        {
            string tmpGridDirectory = Path.GetTempPath() + "UWPHook\\tmp_grid\\";
            string userGridDirectory = user + "\\config\\grid\\";

            // No images were downloaded, maybe the key is invalid or no app had an image
            if (!Directory.Exists(tmpGridDirectory))
            {
                return;
            }

            string[] images = Directory.GetFiles(tmpGridDirectory);

            if (!Directory.Exists(userGridDirectory))
            {
                Directory.CreateDirectory(userGridDirectory);
            }

            foreach (string image in images)
            {
                string destFile = userGridDirectory + Path.GetFileName(image);
                File.Copy(image, destFile, true);
            }
        }

        private void RemoveTempGridImages()
        {
            string tmpGridDirectory = Path.GetTempPath() + "UWPHook\\tmp_grid\\";
            if (Directory.Exists(tmpGridDirectory))
            {
                Directory.Delete(tmpGridDirectory, true);
            }
        }

        /// <summary>
        /// Task responsible for downloading grid images to a temporary location,
        /// generates the steam ID for the game based in the receiving parameters,
        /// Throws TaskCanceledException if cannot communicate with SteamGridDB properly
        /// </summary>
        /// <param name="appName">The name of the app</param>
        /// <param name="appTarget">The target path of the executable</param>
        /// <returns></returns>
        private async Task DownloadTempGridImages(string appName, string appTarget)
        {
            SteamGridDbApi api = new SteamGridDbApi(Properties.Settings.Default.SteamGridDbApiKey);
            string tmpGridDirectory = Path.GetTempPath() + "UWPHook\\tmp_grid\\";
            GameResponse[] games;

            try
            {
                games = await api.SearchGame(appName);
            }
            catch (TaskCanceledException exception)
            {
                throw;
            }

            if (games != null)
            {
                var game = games[0];
                Debug.WriteLine("Detected Game: " + game.ToString());
                UInt64 gameId = GenerateSteamGridAppId(appName, appTarget);

                if (!Directory.Exists(tmpGridDirectory))
                {
                    Directory.CreateDirectory(tmpGridDirectory);
                }

                var gameGridsVertical = api.GetGameGrids(game.Id, "600x900,342x482,660x930");
                var gameGridsHorizontal = api.GetGameGrids(game.Id, "460x215,920x430");
                var gameHeroes = api.GetGameHeroes(game.Id);
                var gameLogos = api.GetGameLogos(game.Id);

                Debug.WriteLine("Game ID: " + game.Id);

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
                    saveImagesTasks.Add(SaveImage(grid.Url, $"{tmpGridDirectory}\\{gameId}.png", ImageFormat.Png));
                }

                if (gridsVertical != null && gridsVertical.Length > 0)
                {
                    var grid = gridsVertical[0];
                    saveImagesTasks.Add(SaveImage(grid.Url, $"{tmpGridDirectory}\\{gameId}p.png", ImageFormat.Png));
                }

                if (heroes != null && heroes.Length > 0)
                {
                    var hero = heroes[0];
                    saveImagesTasks.Add(SaveImage(hero.Url, $"{tmpGridDirectory}\\{gameId}_hero.png", ImageFormat.Png));
                }

                if (logos != null && logos.Length > 0)
                {
                    var logo = logos[0];
                    saveImagesTasks.Add(SaveImage(logo.Url, $"{tmpGridDirectory}\\{gameId}_logo.png", ImageFormat.Png));
                }

                await Task.WhenAll(saveImagesTasks);
            }
        }

        /// <summary>
        /// Main Task to export the selected games to steam
        /// </summary>
        /// <param name="restartSteam"></param>
        /// <returns></returns>
        private async Task<bool> ExportGames(bool restartSteam)
        {
            string[] tags = Settings.Default.Tags.Split(',');
            string steam_folder = SteamManager.GetSteamFolder();

            if (Directory.Exists(steam_folder))
            {
                var users = SteamManager.GetUsers(steam_folder);
                var selected_apps = Apps.Entries.Where(app => app.Selected);
                var exePath = GenerateExePath();
                var exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

                List<Task> gridImagesDownloadTasks = new List<Task>();
                bool downloadGridImages = !String.IsNullOrEmpty(Properties.Settings.Default.SteamGridDbApiKey);
                //To make things faster, decide icons and download grid images before looping users
                Debug.WriteLine("downloadGridImages: " + (downloadGridImages));

                foreach (var app in selected_apps)
                {
                    app.Icon = app.widestSquareIcon();

                    if (downloadGridImages)
                    {
                        Debug.WriteLine("Downloading grid images for app " + app.Name);

                        gridImagesDownloadTasks.Add(DownloadTempGridImages(app.Name, exePath));
                    }
                }

                // Export the selected apps and the downloaded images to each user
                // in the steam folder by modifying it's VDF file
                ExportAppsToSteamShortcuts(tags, users, selected_apps, exePath, exeDir);
                ExportAppsToShieldShortcuts(selected_apps, exePath, exeDir);

                if (gridImagesDownloadTasks.Count > 0)
                {
                    await Task.WhenAll(gridImagesDownloadTasks);

                    await Task.Run(() =>
                    {
                        foreach (var user in users)
                        {
                            CopyTempGridImagesToSteamUser(user);
                            CopyTempGridImagesToShieldStreamingAssets(selected_apps);
                        }

                        RemoveTempGridImages();
                    });
                }
            }

            if (restartSteam)
            {
                Func<Process> getSteam = () => Process.GetProcessesByName("steam").SingleOrDefault();

                Process steam = getSteam();
                if (steam != null)
                {
                    string steamExe = steam.MainModule.FileName;

                    //we always ask politely
                    Debug.WriteLine("Requesting Steam shutdown");
                    Process.Start(steamExe, "-exitsteam");

                    bool restarted = false;
                    Stopwatch watch = new Stopwatch();
                    watch.Start();

                    //give it N seconds to sort itself out
                    int waitSeconds = 8;
                    while (watch.Elapsed.TotalSeconds < waitSeconds)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(0.5f));
                        if (getSteam() == null)
                        {
                            Debug.WriteLine("Restarting Steam");
                            Process.Start(steamExe);
                            restarted = true;
                            break;
                        }
                    }

                    if (!restarted)
                    {
                        Debug.WriteLine("Steam instance not restarted");
                        MessageBox.Show("Failed to restart Steam, please launch it manually", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                }
                else
                {
                    Debug.WriteLine("Steam instance not found to be restarted");
                }
            }

            return true;
        }

        /// <summary>
        /// Extracted method to simplify the complexity of the ExportGames method.
        /// 
        /// This method updates and existing steam vdf file to add or update shortcuts for the selected windows apps.
        /// </summary>
        /// <param name="tags"></param>
        /// <param name="users"></param>
        /// <param name="selected_apps"></param>
        /// <param name="exePath"></param>
        /// <param name="exeDir"></param>
        private static void ExportAppsToSteamShortcuts(string[] tags, string[] users, IEnumerable<AppEntry> selected_apps, string exePath, string exeDir)
        {
            foreach (var user in users)
            {
                try
                {
                    VDFEntry[] shortcuts = new VDFEntry[0];
                    try
                    {
                        shortcuts = SteamManager.ReadShortcuts(user);
                    }
                    catch (Exception ex)
                    {
                        //If it's a short VDF, let's just overwrite it
                        if (ex.GetType() != typeof(VDFTooShortException))
                        {
                            throw new Exception("Error: Program failed to load existing Steam shortcuts." + Environment.NewLine + ex.Message);
                        }
                    }

                    if (shortcuts != null)
                    {
                        foreach (var app in selected_apps)
                        {
                            VDFEntry newApp = new VDFEntry()
                            {
                                AppName = app.Name,
                                Exe = exePath,
                                StartDir = exeDir,
                                LaunchOptions = app.Aumid,
                                AllowDesktopConfig = 1,
                                AllowOverlay = 1,
                                Icon = app.Icon,
                                Index = shortcuts.Length,
                                IsHidden = 0,
                                OpenVR = 0,
                                ShortcutPath = "",
                                Tags = tags,
                                Devkit = 0,
                                DevkitGameID = "",
                                LastPlayTime = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            };
                            Boolean isFound = false;
                            for (int i = 0; i < shortcuts.Length; i++)
                            {
                                Debug.WriteLine(shortcuts[i].ToString());


                                if (shortcuts[i].AppName == app.Name)
                                {
                                    isFound = true;
                                    Debug.WriteLine(app.Name + " already added to Steam. Updating existing shortcut.");
                                    shortcuts[i] = newApp;
                                }
                            }

                            if (!isFound)
                            {
                                //Resize this array so it fits the new entries
                                Array.Resize(ref shortcuts, shortcuts.Length + 1);
                                shortcuts[shortcuts.Length - 1] = newApp;
                            }

                        }

                        try
                        {
                            if (!Directory.Exists(user + @"\\config\\"))
                            {
                                Directory.CreateDirectory(user + @"\\config\\");
                            }
                            //Write the file with all the shortcuts
                            File.WriteAllBytes(user + @"\\config\\shortcuts.vdf", VDFSerializer.Serialize(shortcuts));
                        }
                        catch (Exception ex)
                        {
                            throw new Exception("Error: Program failed while trying to write your Steam shortcuts" + Environment.NewLine + ex.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: Program failed exporting your games:" + Environment.NewLine + ex.Message + ex.StackTrace);
                }
            }
        }

        public static void ClearAllShortcuts()
        {
            Debug.WriteLine("DBG: Clearing all elements in shortcuts.vdf");
            string[] tags = Settings.Default.Tags.Split(',');
            string steam_folder = SteamManager.GetSteamFolder();

            if (Directory.Exists(steam_folder))
            {
                var users = SteamManager.GetUsers(steam_folder);
                var exePath = @"""" + System.Reflection.Assembly.GetExecutingAssembly().Location + @"""";
                var exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

                foreach (var user in users)
                {
                    try
                    {
                        VDFEntry[] shortcuts = new VDFEntry[0];

                        try
                        {
                            if (!Directory.Exists(user + @"\\config\\"))
                            {
                                Directory.CreateDirectory(user + @"\\config\\");
                            }
                            //Write the file with all the shortcuts
                            File.WriteAllBytes(user + @"\\config\\shortcuts.vdf", VDFSerializer.Serialize(shortcuts));
                        }
                        catch (Exception ex)
                        {
                            throw new Exception("Error: Program failed while trying to write your Steam shortcuts" + Environment.NewLine + ex.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error: Program failed while trying to clear your Steam shortcuts:" + Environment.NewLine + ex.Message + ex.StackTrace);
                    }
                }
                MessageBox.Show("All non-Steam shortcuts has been cleared.");
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
                //Get all installed apps on the system excluding frameworks
                List<String> installedApps = AppManager.GetInstalledApps();

                //Alfabetic sort
                installedApps.Sort();

                //Split every app that we couldn't resolve the app name
                var nameNotFound = (from s in installedApps where s.Contains("double click") select s).ToList<String>();

                //Remove them from the original list
                installedApps.RemoveAll(item => item.Contains("double click"));

                //Rejoin them in the original list, but putting them into last
                installedApps = installedApps.Union(nameNotFound).ToList<String>();

                foreach (var app in installedApps)
                {
                    //Remove end lines from the String and split both values, I split the appname and the AUMID using |
                    //I hope no apps have that in their name. Ever.
                    var values = app.Replace("\r\n", "").Split('|');

                    if (values.Length >= 3 && AppManager.IsKnownApp(values[2], out string readableName))
                    {
                        values[0] = readableName;
                    }

                    if (!String.IsNullOrWhiteSpace(values[0]))
                    {
                        //We get the default square tile to find where the app stores it's icons, then we resolve which one is the widest
                        string logosPath = Path.GetDirectoryName(values[1]);
                        Application.Current.Dispatcher.BeginInvoke((Action)delegate ()
                        {
                            Apps.Entries.Add(new AppEntry() { Name = values[0], IconPath = logosPath, Aumid = values[2], Selected = false });
                        });
                    }
                }
            }
            catch (Exception ex)
            {
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
            if (String.IsNullOrEmpty(Settings.Default.SteamGridDbApiKey) && !Settings.Default.OfferedSteamGridDB)
            {
                Settings.Default.OfferedSteamGridDB = true;
                Settings.Default.Save();

                var boxResult = MessageBox.Show("Do you want to automatically import grid images for imported games?", "UWPHook", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (boxResult == MessageBoxResult.Yes)
                {
                    SettingsButton_Click(this, null);
                }
            }
        }

        /// <summary>
        /// Generates the exe path used by Steam and NVIDIA Shield to call UWPHook
        /// </summary>
        /// <returns></returns>
        private static string GenerateExePath()
        {
            return @"""" + System.Reflection.Assembly.GetExecutingAssembly().Location + @"""";
        }

        /// <summary>
        /// Iterates through the selected apps and creates windwos shortcut files for each inside the NVIDIA Shield Apps directory.
        /// Will place an NVIDIA required placeholder box-art.png file inside the StreamingAssets/{app-name} dir.
        /// </summary>
        /// <param name="selected_apps"></param>
        /// <param name="exePath"></param>
        /// <param name="exeDir"></param>
        private static void ExportAppsToShieldShortcuts(IEnumerable<AppEntry> selected_apps, string exePath, string exeDir)
        {
            string shieldAppsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NVIDIA Corporation", "Shield Apps");
            string streamingAssetsDir = Path.Combine(shieldAppsDir, "StreamingAssets");
            if (!Directory.Exists(streamingAssetsDir))
            {
                DirectoryInfo di = Directory.CreateDirectory(streamingAssetsDir);
                di.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
            }

            foreach (AppEntry app in selected_apps)
            {
                string scrubbedAppName = app.Name;
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    scrubbedAppName = scrubbedAppName.Replace(c, '_');
                }

                string shortcutFile = Path.Combine(shieldAppsDir, scrubbedAppName + ".lnk");
                Debug.WriteLine("Creating NVIDIA Shield shortcut: " + shortcutFile);

                IWshRuntimeLibrary.IWshShell3 wsh = new IWshRuntimeLibrary.IWshShell_Class();
                IWshRuntimeLibrary.IWshShortcut shortcut = wsh.CreateShortcut(shortcutFile);
                shortcut.Arguments = app.Aumid;
                shortcut.TargetPath = exePath;
                // not sure about what this is for
                shortcut.WindowStyle = 1;
                shortcut.Description = "test link for " + app.Name;
                shortcut.WorkingDirectory = exeDir;
                //shortcut.IconLocation = "specify icon location";
                shortcut.Save();

                // Copy over placeholder box-art.png (required by NVIDIA)
                string boxArtDir = Path.Combine(streamingAssetsDir, scrubbedAppName);
                string boxArtFile = Path.Combine(streamingAssetsDir, scrubbedAppName, "box-art.png");
                if (!Directory.Exists(boxArtDir))
                {
                    Debug.WriteLine("creating directory for game streaming assets: " + boxArtDir);
                    try
                    {
                        Directory.CreateDirectory(boxArtDir);
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine("unable to create game streaming assets dir: {0}", e);
                    }

                }
                try
                {
                    Debug.WriteLine("copying placeholder box-art to " + boxArtFile);
                    Properties.Resources.box_art.Save(boxArtFile);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine("unable to copy placeholder box-art: {0}", e);
                }

            }

        }

        /// <summary>
        /// Copies all temporary images to the {drive}:/Users/{user}/Appdata/Local/NVIDIA Corporation/Shield Apps/StreamingAssets/{scrubbed game name} dirs
        /// </summary>
        /// <param name="selectedApps">Selected app entrys to copy images for</param>
        private void CopyTempGridImagesToShieldStreamingAssets(IEnumerable<AppEntry> selectedApps)
        {
            string tmpGridDirectory = Path.Combine(Path.GetTempPath(), "UWPHook", "tmp_grid");
            string streamingAssetsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NVIDIA Corporation", "Shield Apps", "StreamingAssets");

            // No images were downloaded, maybe the key is invalid or no app had an image
            if (!Directory.Exists(tmpGridDirectory))
            {
                return;
            }
            if (!Directory.Exists(streamingAssetsDirectory))
            {
                DirectoryInfo di = Directory.CreateDirectory(streamingAssetsDirectory);
                di.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
            }

            foreach (AppEntry app in selectedApps)
            {
                string scrubbedAppName = app.Name;
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    scrubbedAppName = scrubbedAppName.Replace(c, '_');
                }

                ulong gameId = GenerateSteamGridAppId(app.Name, GenerateExePath());
                string srcFile = Path.Combine(tmpGridDirectory, string.Format("{0}p.png", gameId));
                string destDir = Path.Combine(streamingAssetsDirectory, scrubbedAppName);
                string destFile = Path.Combine(destDir, "box-art.png");
                if (!Directory.Exists(destDir))
                {
                    try
                    {
                        Debug.WriteLine("Creating directory: ", destDir);
                        _ = Directory.CreateDirectory(destDir);
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine("unable to create directory: ", e);
                    }
                }
                try
                {
                    Debug.WriteLine("Copying box-art image from {0} to {1}", new object[] { srcFile, destFile });
                    File.Copy(srcFile, destFile, true);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine("unable to copy box-art: ", e);
                }

            }
        }
    }
}
