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

            for (int i = 1; i < 10; i++)
            {
                seconds_comboBox.Items.Add(i + " seconds");
                if (i == Properties.Settings.Default.Seconds)
                {
                    seconds_comboBox.SelectedIndex = i - 1;
                }
            }

            cultures_comboBox.SelectedItem = Properties.Settings.Default.TargetLanguage;
            language_toggle.IsChecked = Properties.Settings.Default.ChangeLanguage;
            streaming_toggle.IsChecked = Properties.Settings.Default.StreamMode;
            steamgriddb_api_key.Text = Properties.Settings.Default.SteamGridDbApiKey;
        }

        private void saveButton_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.ChangeLanguage = (bool)language_toggle.IsChecked;
            Properties.Settings.Default.TargetLanguage = cultures_comboBox.SelectedItem.ToString();
            Properties.Settings.Default.Seconds = Int32.Parse(seconds_comboBox.SelectedItem.ToString().Substring(0, 1));
            Properties.Settings.Default.StreamMode = (bool)streaming_toggle.IsChecked;
            Properties.Settings.Default.SteamGridDbApiKey = steamgriddb_api_key.Text;
            Properties.Settings.Default.Save();
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

        private void update_button_Copy_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/BrianLima/OverFy/releases");
        }
    }
}