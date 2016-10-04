using SharpSteam;
using System;
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
            //Hide the window so the app is launched seamless
            this.Hide();
            string currentLanguage = CultureInfo.CurrentCulture.ToString();
            try
            {
                if (Properties.Settings.Default.ChangeLanguage && !String.IsNullOrEmpty(Properties.Settings.Default.TargetLanguage))
                {
                    ScriptManager.RunScript("Set-WinUILanguageOverride " + Properties.Settings.Default.TargetLanguage);
                }

                //The only other parameter Steam will send is the app AUMID
                AppManager.LaunchUWPApp(Environment.GetCommandLineArgs()[1]);

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
                    VDFEntry[] shortcuts = SteamManager.ReadShortcuts(user);

                    //TODO: Figure out what to do when user has no shortcuts whatsoever
                    if (shortcuts != null && shortcuts.Length > 0)
                    {
                        foreach (var app in selected_apps)
                        {
                            VDFEntry newApp = new VDFEntry()
                            {
                                AppName = app.Name,
                                Exe = @"""" + System.Reflection.Assembly.GetExecutingAssembly().Location + @""" " + app.Aumid,
                                StartDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
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

                        File.WriteAllBytes(user + @"\\config\\shortcuts.vdf", VDFSerializer.Serialize(shortcuts));
                    }
                }
            }
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            var installedApps = AppManager.GetInstalledApps();

            foreach (var app in installedApps)
            {
                //Remove end lines from the String and split both values
                var valor = app.Replace("\r\n", "").Split('|');
                if (!String.IsNullOrEmpty(valor[0]))
                {
                    Apps.Entries.Add(new AppEntry() { Name = valor[0], Aumid = valor[1], Selected = false });
                }
            }

            listGames.Columns[2].IsReadOnly = true;
            label.Content = "Installed Apps";
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow window = new SettingsWindow();
            window.ShowDialog();
        }
    }
}
