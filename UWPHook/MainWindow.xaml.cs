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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace UWPHook
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        GameModel games;
        public MainWindow()
        {
            InitializeComponent();

            if (Environment.GetCommandLineArgs() != null)
            {
            }

           // games = new List<GameModel>();
           // for (int i = 0; i < 10; i++)
           // {
           //     games.Add(new GameModel { game_alias = "sajufhsaduifhuisdsdgbuigduisaguidsguisaguiasguidasg", game_path = "sajufhsaduifhuisdsdgbuigduisaguidsguisaguiasguidasgx" });
           // }
            
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            games = new GameModel();
            listView.ItemsSource = games.games;
            
        }

        private void addButton_Click(object sender, RoutedEventArgs e)
        {
            games.Store();
            games.Add(new Game{ game_alias = alias_textBox.Text, game_path = path_textBox.Text });
        }
    }
}
