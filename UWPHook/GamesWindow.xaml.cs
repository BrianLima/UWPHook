using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using VDFParser;
using VDFParser.Models;
using SharpSteam;
using System.IO;

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
            this.Title = string.Join(";", Environment.GetCommandLineArgs());
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
        }
    }
}
