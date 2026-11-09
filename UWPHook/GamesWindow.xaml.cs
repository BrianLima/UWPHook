using System.Windows;
using UWPHook.Presentation.Games;

namespace UWPHook
{
    /// <summary>The games list. Its behaviour is <see cref="GamesViewModel"/>; this only wires the view.</summary>
    public partial class GamesWindow : Window
    {
        private readonly GamesViewModel _games;
        private readonly Func<SettingsWindow> _settingsWindow;

        /// <param name="settingsWindow">Makes a new Settings window each time it is opened.</param>
        public GamesWindow(GamesViewModel games, Func<SettingsWindow> settingsWindow)
        {
            InitializeComponent();
            _games = games;
            _settingsWindow = settingsWindow;
            DataContext = games;

            // Auto refresh on open
            games.LoadCommand.Execute(null);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e) => _settingsWindow().ShowDialog();

        /// <summary>The first time: offers to set up SteamGridDB, opening Settings when the user wants to.</summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_games.OfferSteamGridDbOnce()) SettingsButton_Click(this, e);
        }
    }
}
