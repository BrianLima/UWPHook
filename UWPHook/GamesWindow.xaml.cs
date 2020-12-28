using Force.Crc32;
using SharpSteam;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        BackgroundWorker bwrLoad, bwrSave;

        public GamesWindow()
        {
            InitializeComponent();
            Apps = new AppEntryModel();

            //If null or 1, the app was launched normally
            if (Environment.GetCommandLineArgs() != null)
            {
                //When length is 1, the only argument is the path where the app is installed
                if (Environment.GetCommandLineArgs().Length > 1)
                {
                    LauncherAsync();
                }
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

        private async Task LauncherAsync()
        {
            FullScreenLauncher launcher = null;
            //So, for some reason, Steam is now stopping in-home streaming if the launched app is minimized, so not hiding UWPHook's window is doing the trick for now
            if (Properties.Settings.Default.StreamMode)
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
                if (Properties.Settings.Default.ChangeLanguage && !String.IsNullOrEmpty(Properties.Settings.Default.TargetLanguage))
                {
                    ScriptManager.RunScript("Set-WinUILanguageOverride " + Properties.Settings.Default.TargetLanguage);
                }

                //The only other parameter Steam will send is the app AUMID
                AppManager.LaunchUWPApp(Environment.GetCommandLineArgs()[1]);

                //While the current launched app is running, sleep for n seconds and then check again
                while (AppManager.IsRunning())
                {
                    Thread.Sleep(Properties.Settings.Default.Seconds * 1000);
                }
            }
            catch (Exception e)
            {
                this.Show();
                MessageBox.Show(e.Message, "UWPHook", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                if (Properties.Settings.Default.ChangeLanguage && !String.IsNullOrEmpty(Properties.Settings.Default.TargetLanguage))
                {
                    ScriptManager.RunScript("Set-WinUILanguageOverride " + currentLanguage);
                }

                //The user has probably finished using the app, so let's close UWPHook to keep the experience clean 
                this.Close();
            }
        }

        private UInt64 GenerateSteamGridAppId(string appName, string appTarget)
        {
            byte[] nameTargetBytes = Encoding.UTF8.GetBytes(appTarget + appName + "");
            UInt64 crc = Crc32Algorithm.Compute(nameTargetBytes);
            UInt64 gameId = crc | 0x80000000;

            return gameId;
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            grid.IsEnabled = false;
            progressBar.Visibility = Visibility.Visible;

            await ExportGames();

            grid.IsEnabled = true;
            progressBar.Visibility = Visibility.Collapsed;
            MessageBox.Show("Your apps were successfuly exported, please restart Steam in order to see your apps.", "UWPHook", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task SaveImage(string imageUrl, string destinationFilename, ImageFormat format)
        {
            await Task.Run(() =>
            {
                WebClient client = new WebClient();
                Stream stream = client.OpenRead(imageUrl);
                Bitmap bitmap; bitmap = new Bitmap(stream);

                if (bitmap != null)
                {
                    bitmap.Save(destinationFilename, format);
                }

                stream.Flush();
                stream.Close();
                client.Dispose();
            });            
        }

        private void CopyTempGridImagesToSteamUser(string user)
        {            
            string tmpGridDirectory = Path.GetTempPath() + "UWPHook\\tmp_grid\\";
            string userGridDirectory = user + "\\config\\grid\\";
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
            Directory.Delete(tmpGridDirectory, true);
        }

        private async Task DownloadTempGridImages(string appName, string appTarget)
        {
            SteamGridDbApi api = new SteamGridDbApi(Properties.Settings.Default.SteamGridDbApiKey);
            string tmpGridDirectory = Path.GetTempPath() + "UWPHook\\tmp_grid\\";

            var games = await api.SearchGame(appName);

            if (games != null)
            {
                var game = games[0];
                UInt64 gameId = GenerateSteamGridAppId(appName, appTarget);

                if (!Directory.Exists(tmpGridDirectory))
                {
                    Directory.CreateDirectory(tmpGridDirectory);
                }

                var gameGridsVertical = api.GetGameGrids(game.Id, "600x900", "static");
                var gameGridsHorizontal = api.GetGameGrids(game.Id, "460x215", "static");
                var gameHeroes = api.GetGameHeroes(game.Id, "static");
                var gameLogos = api.GetGameLogos(game.Id, "static");

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

        private async Task ExportGames()
        {
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
                foreach (var app in selected_apps)
                {
                    app.Icon = app.widestSquareIcon();

                    if (downloadGridImages)
                    {
                        gridImagesDownloadTasks.Add(DownloadTempGridImages(app.Name, exePath));
                    }
                }

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
                                    Tags = new string[0],
                                    Devkit = 0,
                                    DevkitGameID = "",
                                    LastPlayTime = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                                };

                                //Resize this array so it fits the new entries
                                Array.Resize(ref shortcuts, shortcuts.Length + 1);
                                shortcuts[shortcuts.Length - 1] = newApp;
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
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            bwrLoad = new BackgroundWorker();
            bwrLoad.DoWork += Bwr_DoWork;
            bwrLoad.RunWorkerCompleted += Bwr_RunWorkerCompleted;

            grid.IsEnabled = false;
            progressBar.Visibility = Visibility.Visible;
            Apps.Entries = new System.Collections.ObjectModel.ObservableCollection<AppEntry>();

            bwrLoad.RunWorkerAsync();
        }

        private void Bwr_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            listGames.ItemsSource = Apps.Entries;

            listGames.Columns[2].IsReadOnly = true;
            listGames.Columns[3].IsReadOnly = true;

            grid.IsEnabled = true;
            progressBar.Visibility = Visibility.Collapsed;
            label.Content = "Installed Apps";
        }

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
                    if (!String.IsNullOrWhiteSpace(values[0]))
                    {
                        //We get the default square tile to find where the app stores it's icons, then we resolve which one is the widest
                        string logosPath = Path.GetDirectoryName(values[1]);
                        Application.Current.Dispatcher.BeginInvoke((Action)delegate ()
                        {
                            Apps.Entries.Add(new AppEntry() { Name = values[0], IconPath = logosPath, Aumid = values[2], Selected = false });
                        });
                    }
                    if (values.Length > 2)
                    {
                        if (values[2].Contains("Microsoft.SeaofThieves"))
                        {
                            values[0] = "Sea of Thieves";
                        }
                        else if (values[2].Contains("Microsoft.DeltaPC"))
                        {
                            values[0] = "Gears of War: Ultimate Edition";
                        }
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
            //Return members whose Orders have not been filled
            return (appEntry.Aumid.ToLower().Contains(textBox.Text.ToLower()));
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow window = new SettingsWindow();
            window.ShowDialog();
        }
    }
}
