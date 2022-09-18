using System;
using System.Globalization;
using System.Windows;

namespace UWPHook
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();

            this.Title = "UWPHook version " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

            foreach (var culture in CultureInfo.GetCultures(CultureTypes.AllCultures))
            {
                cultures_comboBox.Items.Add(culture.TextInfo.CultureName);
            }
            
            for (int i = 0; i < 10; i++)
            {
                seconds_comboBox.Items.Add(i + " seconds");
                if (i == Properties.Settings.Default.Seconds)
                {
                    seconds_comboBox.SelectedIndex = i;
                }
            }

            cultures_comboBox.SelectedItem = Properties.Settings.Default.TargetLanguage;
            language_toggle.IsChecked = Properties.Settings.Default.ChangeLanguage;
            streaming_toggle.IsChecked = Properties.Settings.Default.StreamMode;
            logLevel_comboBox.SelectedIndex = Properties.Settings.Default.SelectedLogLevel;
            steamgriddb_api_key.Text = Properties.Settings.Default.SteamGridDbApiKey;
            style_comboBox.SelectedIndex = Properties.Settings.Default.SelectedSteamGridDB_Style;
            type_comboBox.SelectedIndex = Properties.Settings.Default.SelectedSteamGridDB_Type;
            nfsw_comboBox.SelectedIndex = Properties.Settings.Default.SelectedSteamGridDB_nfsw;
            humor_comboBox.SelectedIndex = Properties.Settings.Default.SelectedSteamGridDB_Humor;
            tags_textBox.Text = Properties.Settings.Default.Tags;
        }

        private void saveButton_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.ChangeLanguage = (bool)language_toggle.IsChecked;
            Properties.Settings.Default.TargetLanguage = cultures_comboBox.SelectedItem.ToString();
            Properties.Settings.Default.Seconds = Int32.Parse(seconds_comboBox.SelectedItem.ToString().Substring(0, 1));
            Properties.Settings.Default.StreamMode = (bool)streaming_toggle.IsChecked;
            Properties.Settings.Default.SelectedLogLevel = logLevel_comboBox.SelectedIndex;
            Properties.Settings.Default.SteamGridDbApiKey = steamgriddb_api_key.Text.Trim('\r', '\n');
            Properties.Settings.Default.SelectedSteamGridDB_Style = style_comboBox.SelectedIndex;
            Properties.Settings.Default.SelectedSteamGridDB_Type = type_comboBox.SelectedIndex;
            Properties.Settings.Default.SelectedSteamGridDB_nfsw = nfsw_comboBox.SelectedIndex;
            Properties.Settings.Default.SelectedSteamGridDB_Humor = humor_comboBox.SelectedIndex;
            Properties.Settings.Default.Tags = tags_textBox.Text;
            Properties.Settings.Default.Save();
            GamesWindow.SetLogLevel();
            this.Close();
        }

        private void Chip_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("http://twitter.com/brianostorm");
        }

        private void Chip1_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("http://github.com/brianlima");
        }

        private void Chip2_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=9YPV3FHEFRAUQ");
        }

        private void update_button_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/BrianLima/UWPHook/releases");
        }

        private void help_button_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://reddit.com/r/UWPHook/");
        }

        private void clearAll_button_Click(object sender, RoutedEventArgs e)
        {
            GamesWindow.ClearAllShortcuts();
        }

        private void key_Button_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(messageBoxText: "You are being redirected to SteamGridDB website!\r\n" +
                "Log-in, or create your account, go to your profile preferences and click 'Generate API Key', then paste the key back on UWPHook.", "Attention!", MessageBoxButton.OK, MessageBoxImage.Information );
            System.Diagnostics.Process.Start("https://www.steamgriddb.com/profile/preferences/api");
        }
    }
}