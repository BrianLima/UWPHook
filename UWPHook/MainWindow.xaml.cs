using SharpSteam;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using VDFParser;
using VDFParser.Models;

namespace UWPHook
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        GameModel gamesView;

        public MainWindow()
        {
            InitializeComponent();
            gamesView = new GameModel();

            if (Environment.GetCommandLineArgs() != null)
            {
                if (Environment.GetCommandLineArgs().Length > 1)
                {
                    //manager = new AppManager();
                    try
                    {
                        this.Hide();

                        Launch_Game(String.Join(" ", Environment.GetCommandLineArgs()));
                        //while (manager.IsRunning())
                        {
                            Thread.Sleep(5000);
                        }

                        this.Close();
                    }
                    catch (Exception e)
                    {
                        this.Show();
                        MessageBox.Show(e.Message);
                    }
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            listView.ItemsSource = gamesView.games;
        }

        private void Launch_Game(string game_name)
        {
            //Remove startup path from parameters to get the game name sent from startup options from Steam
            game_name = game_name.Remove(0, (System.Reflection.Assembly.GetExecutingAssembly().Location + " ").Length);
            foreach (Game game in gamesView.games)
            {
                if (game.game_alias.ToLower() == game_name.ToLower())
                {
                    try
                    {
                        //manager.LaunchUWPApp(game.game_path);
                    }
                    catch (Exception ex)
                    {
                    }
                    break;
                }
            }
        }

        private void addButton_Click(object sender, RoutedEventArgs e)
        {
            gamesView.Add(new Game { game_alias = alias_textBox.Text, game_path = path_textBox.Text });
            gamesView.Store();
        }

        private void helpButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://www.reddit.com/r/UWPHook/comments/53eaj9/welcome_to_uwphook_link_your_uwp_games_to_steam/");
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            gamesView.games.RemoveAt(listView.SelectedIndex);
            gamesView.Store();
        }
    }
}
