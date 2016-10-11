using SharpSteam;
using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
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
        BackgroundWorker bwr;

        public GamesWindow()
        {
            InitializeComponent();
            Apps = new AppEntryModel();
            listGames.ItemsSource = Apps.Entries;

            //If null or 1, the app was launched normally
            if (Environment.GetCommandLineArgs() != null)
            {
                //When length is 1, the only argument is the path where the app is installed
                if (Environment.GetCommandLineArgs().Length > 1)
                {
                    Launcher();
                }
            }
        }

        private void Launcher()
        {
            this.Title = "UWPHook: Playing a game";
            //Hide the window so the app is launched seamless making UWPHook run in the background without bothering the user
            this.Hide();
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
                    ScriptManager.RunScript("Set - WinUILanguageOverride " + currentLanguage);
                }

                //The user has probably finished using the app, so let's close UWPHook to keep the experience clean 
                this.Close();
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            string steam_folder = SteamManager.GetSteamFolder();
            if (!String.IsNullOrEmpty(steam_folder))
            {
                var users = SteamManager.GetUsers(steam_folder);
                var selected_apps = Apps.Entries.Where(app => app.Selected);
                foreach (var user in users)
                {
                    VDFEntry[] shortcuts;
                    try
                    {
                        shortcuts = SteamManager.ReadShortcuts(user);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Error trying to load existing Steam shortcuts." + Environment.NewLine + ex.Message);
                    }

                    //TODO: Figure out what to do when user has no shortcuts whatsoever
                    if (shortcuts != null )
                    {
                        foreach (var app in selected_apps)
                        {
                            VDFEntry newApp = new VDFEntry()
                            {
                                AppName = app.Name,
                                Exe = @"""" + System.Reflection.Assembly.GetExecutingAssembly().Location + @""" " + app.Aumid,
                                StartDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                                AllowDesktopConfig = 1,
                                Icon = "",
                                Index = shortcuts.Length,
                                IsHidden = 0,
                                OpenVR = 0,
                                ShortcutPath = "",
                                Tags = new string[0]
                            };

                            //Resize this array so it fits the new entries
                            Array.Resize(ref shortcuts, shortcuts.Length + 1);
                            shortcuts[shortcuts.Length - 1] = newApp;
                        }

                        try
                        {  
                            //Write the file with all the shortcuts
                            File.WriteAllBytes(user + @"\\config\\shortcuts.vdf", VDFSerializer.Serialize(shortcuts));
                        }
                        catch (Exception ex)
                        {

                            throw new Exception("Error while trying to write your Steam shortcuts" + Environment.NewLine + ex.Message);
                        }
                    }
                }
            }

            MessageBox.Show("Your apps were successfuly exported, please restart Steam in order to see your apps in it.", "UWPHook", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            bwr = new BackgroundWorker();
            bwr.DoWork += Bwr_DoWork;
            bwr.RunWorkerCompleted += Bwr_RunWorkerCompleted;

            grid.IsEnabled = false;
            progressBar.Visibility = Visibility.Visible;
            bwr.RunWorkerAsync();
        }

        private void Bwr_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            grid.IsEnabled = true;
            listGames.Columns[2].IsReadOnly = true;
            progressBar.Visibility = Visibility.Collapsed;
            label.Content = "Installed Apps";
        }

        private void Bwr_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                var installedApps = AppManager.GetInstalledApps();
                foreach (var app in installedApps)
                {
                    //Remove end lines from the String and split both values, I split the appname and the AUMID using |
                    //I hope no apps have that in their name. Ever.
                    var valor = app.Replace("\r\n", "").Split('|');
                    if (!String.IsNullOrWhiteSpace(valor[0]) && !valor[0].Contains("ms-resource"))
                    {
                        Application.Current.Dispatcher.BeginInvoke((Action)delegate ()
                        {
                            Apps.Entries.Add(new AppEntry() { Name = valor[0], Aumid = valor[1], Selected = false });
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "UWPHook", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow window = new SettingsWindow();
            window.ShowDialog();
        }
    }
}