using System;
using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Security.Policy;
using System.Windows;
using UWPHook.Properties;
using static System.Net.WebRequestMethods;

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

            Title = "UWPHook version " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

            cultures_comboBox.ItemsSource = CultureInfo.GetCultures(CultureTypes.AllCultures).Select(c => c.TextInfo.CultureName);
            cultures_comboBox.SelectedItem = string.IsNullOrEmpty(Settings.Default.TargetLanguage) ? CultureInfo.CurrentCulture.TextInfo.CultureName : Properties.Settings.Default.TargetLanguage;
            
            seconds_comboBox.ItemsSource = Enumerable.Range(0, 10).Select(i => i + " seconds");
            seconds_comboBox.SelectedIndex = Properties.Settings.Default.Seconds;
            
            resolution_comboBox.ItemsSource = GetResolutions();
            resolution_comboBox.SelectedItem = string.IsNullOrEmpty(Settings.Default.TargetResolution) ? GetCurrentResolution() : Properties.Settings.Default.TargetResolution;

            int logLevel_index = 0;
            int.TryParse(Properties.Settings.Default.SelectedLogLevel, out logLevel_index);

            language_toggle.IsChecked = Properties.Settings.Default.ChangeLanguage;
            streaming_toggle.IsChecked = Properties.Settings.Default.StreamMode;
            change_resolution_toggle.IsChecked = Properties.Settings.Default.ChangeResolution;
            logLevel_comboBox.SelectedIndex = logLevel_index;
            steamgriddb_api_key.Text = Properties.Settings.Default.SteamGridDbApiKey;
            style_comboBox.SelectedIndex = Properties.Settings.Default.SelectedSteamGridDB_Style;
            type_comboBox.SelectedIndex = Properties.Settings.Default.SelectedSteamGridDB_Type;
            nfsw_comboBox.SelectedIndex = Properties.Settings.Default.SelectedSteamGridDB_nfsw;
            humor_comboBox.SelectedIndex = Properties.Settings.Default.SelectedSteamGridDB_Humor;
            tags_textBox.Text = Properties.Settings.Default.Tags;
        }

#pragma warning disable CA1416 // Validate platform compatibility
        private IEnumerable<string> GetResolutions()
        {

            using var searcher = new ManagementObjectSearcher("SELECT * FROM CIM_VideoControllerResolution");
            foreach (var resolution in searcher.Get())
            {
                uint horizontalResolution = (uint)resolution["HorizontalResolution"];
                uint verticalResolution = (uint)resolution["VerticalResolution"];
                yield return horizontalResolution + " x " + verticalResolution;
            }

        }

        private string GetCurrentResolution()
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
            foreach (var mo in searcher.Get())
            {
                var currentHorizontalResolution = mo["CurrentHorizontalResolution"];
                var currentVerticalResolution = mo["CurrentVerticalResolution"];
                if (currentHorizontalResolution != null && currentVerticalResolution != null)
                {
                    return $"{currentHorizontalResolution} x {currentVerticalResolution}";
                }
            }

            return string.Empty;
        }
#pragma warning restore CA1416 // Validate platform compatibility

        private void saveButton_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.ChangeLanguage = (bool)language_toggle.IsChecked;
            Properties.Settings.Default.TargetLanguage = cultures_comboBox.SelectedItem.ToString();
            Properties.Settings.Default.Seconds = Int32.Parse(seconds_comboBox.SelectedItem.ToString().Substring(0, 1));
            Properties.Settings.Default.StreamMode = (bool)streaming_toggle.IsChecked;
            Properties.Settings.Default.ChangeResolution = (bool)change_resolution_toggle.IsChecked;
            Properties.Settings.Default.TargetResolution = resolution_comboBox.SelectedItem.ToString();
            Properties.Settings.Default.SelectedLogLevel = logLevel_comboBox.SelectedIndex.ToString();
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
            OpenUrl("http://twitter.com/brianostorm");
        }

        private void Chip1_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("http://github.com/brianlima");
        }

        private void Chip2_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=9YPV3FHEFRAUQ");
        }

        private void update_button_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://github.com/BrianLima/UWPHook/releases");
        }

        private void help_button_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://reddit.com/r/UWPHook/");
        }

        private void clearAll_button_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show("This action will remove ALL shortcuts from non-Steam games." + Environment.NewLine + " Are you sure you want to continue?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                GamesWindow.ClearAllShortcuts();
            }
        }

        private void key_Button_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(messageBoxText: "You are being redirected to SteamGridDB website!\r\n" +
                "Log-in, or create your account, go to your profile preferences and click 'Generate API Key', then paste the key back on UWPHook.", "Attention!", MessageBoxButton.OK, MessageBoxImage.Information);
            OpenUrl("https://www.steamgriddb.com/profile/preferences/api");
        }

        private void OpenUrl(string url)
        {
            var psi = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
    }
}