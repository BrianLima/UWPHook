using Force.Crc32;
using Serilog;
using Serilog.Core;
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
        static LoggingLevelSwitch levelSwitch = new LoggingLevelSwitch();

        public GamesWindow()
        {
            InitializeComponent();
            Log.Debug("Init GamesWindow");
            Apps = new AppEntryModel();
            var args = Environment.GetCommandLineArgs();

            // Init log file to AppData\Roaming\Briano\UWPHook directory with size rotation on 10Mb with max 5 files
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
            string loggerFilePath = String.Join("\\", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), fvi.CompanyName, fvi.ProductName, "application.log");

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
                await ExportGames();
                await RestartSteam(restartSteam);

                msg = "Your apps were successfuly exported!";
                if (!restartSteam)
                {
                    msg += " Please restart Steam in order to see them.";
                }
                else if (result)
                {
                    msg += " Steam has been restarted.";
                }

            }
            catch (TaskCanceledException exception)
            {
                Log.Error(exception.Message);
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
                catch (Exception exception)
                {
                    Log.Error(exception.Message);
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
            GameResponse[] games = null;

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
                UInt64 gameId = GenerateSteamGridAppId(appName, appTarget);

                if (!Directory.Exists(tmpGridDirectory))
                {
                    Directory.CreateDirectory(tmpGridDirectory);
                }

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
        private async Task<bool> ExportGames()
        {
            string[] tags = Settings.Default.Tags.Split(',');
            string steam_folder = SteamManager.GetSteamFolder();

            if (Directory.Exists(steam_folder))
            {
                var users = SteamManager.GetUsers(steam_folder);
                var selected_apps = Apps.Entries.Where(app => app.Selected);
                var exePath = @"""" + System.Reflection.Assembly.GetExecutingAssembly().Location + @"""";
                var exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

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

                        gridImagesDownloadTasks.Add(DownloadTempGridImages(app.Name, exePath));
                    }
                }

                await Task.WhenAll(gridImagesDownloadTasks);

                // Export the selected apps and the downloaded images to each user
                // in the steam folder by modifying it's VDF file
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
                                Log.Error("Error: Program failed to load existing Steam shortcuts." + Environment.NewLine + ex.Message);
                                throw new Exception("Error: Program failed to load existing Steam shortcuts." + Environment.NewLine + ex.Message);
                            }
                        }

                        if (shortcuts != null)
                        {
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
                                        string tmpGridDirectory = Path.GetTempPath() + "UWPHook\\tmp_grid\\";
                                        string[] images = Directory.GetFiles(tmpGridDirectory);

                                        UInt64 gameId = GenerateSteamGridAppId(app.Name, exePath);
                                        app.Icon = PersistAppIcon(app, tmpGridDirectory + gameId + "_logo.png");
                                    });
                                }

                                VDFEntry newApp = new VDFEntry()
                                {
                                    AppName = app.Name,
                                    Exe = exePath,
                                    StartDir = exeDir,
                                    LaunchOptions = app.Aumid + " " + app.Executable,
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
                                    Log.Verbose(shortcuts[i].ToString());


                                    if (shortcuts[i].AppName == app.Name)
                                    {
                                        isFound = true;
                                        Log.Verbose(app.Name + " already added to Steam. Updating existing shortcut.");
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
                                Log.Error("Error: Program failed while trying to write your Steam shortcuts" + Environment.NewLine + ex.Message);
                                throw new Exception("Error: Program failed while trying to write your Steam shortcuts" + Environment.NewLine + ex.Message);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Error: Program failed exporting your games:" + Environment.NewLine + ex.Message + ex.StackTrace);
                        MessageBox.Show("Error: Program failed exporting your games:" + Environment.NewLine + ex.Message + ex.StackTrace);
                    }
                }

                if (gridImagesDownloadTasks.Count > 0)
                {
                    await Task.WhenAll(gridImagesDownloadTasks);

                    await Task.Run(() =>
                    {
                        foreach (var user in users)
                        {
                            CopyTempGridImagesToSteamUser(user);
                        }

                        RemoveTempGridImages();
                    });
                }
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
        /// Restarts the Steam.exe process
        /// </summary>
        /// <param name="restartSteam"></param>
        /// <returns></returns>
        private async Task<bool> RestartSteam(bool restartSteam)
        {
            Func<Process> getSteam = () => Process.GetProcessesByName("steam").SingleOrDefault();
            Process steam = getSteam();

            if (steam != null)
            {
                string steamExe = steam.MainModule.FileName;

                //we always ask politely
                Log.Debug("Requesting Steam shutdown");
                Process.Start(steamExe, "-exitsteam");

                bool restarted = false;
                Stopwatch watch = new Stopwatch();
                watch.Start();

                //give it N seconds to sort itself out
                int waitSeconds = 8;
                while (!restarted || watch.Elapsed.TotalSeconds < waitSeconds)
                {
                    await Task.Delay(TimeSpan.FromSeconds(0.5f));
                    if (getSteam() == null)
                    {
                        Log.Debug("Restarting Steam");
                        Process.Start(steamExe);
                        restarted = true;
                        break;
                    }
                }

                if (!restarted)
                {
                    Log.Debug("Steam instance not restarted");
                    MessageBox.Show("Failed to restart Steam, please launch it manually", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }
            else
            {
                Log.Debug("Steam instance not found to be restarted");
            }

            return true;
        }

        public static void ClearAllShortcuts()
        {
            Log.Debug("Clearing all elements in shortcuts.vdf");
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
                            Log.Error("Error: Program failed while trying to write your Steam shortcuts" + Environment.NewLine + ex.Message);
                            throw new Exception("Error: Program failed while trying to write your Steam shortcuts" + Environment.NewLine + ex.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Error: Program failed while trying to clear your Steam shortcuts:" + Environment.NewLine + ex.Message + ex.StackTrace);
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
                            Apps.Entries.Add(new AppEntry() { Name = values[0], Executable = values[3], IconPath = logosPath, Aumid = values[2], Selected = false });
                        });
                    }
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
            switch (Settings.Default.SelectedLogLevel)
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
