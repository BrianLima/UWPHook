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
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            string steam_folder = SteamManager.GetSteamFolder();
            var users = SteamManager.GetUsers(steam_folder);
            foreach (var user in users)
            {
                var shortcuts = SteamManager.ReadShortcuts(user);
                if (shortcuts != null)
                {
                    //foreach (var item in Apps.Entries.Select<)
                    //{
                    //
                    //}
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
