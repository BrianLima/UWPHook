using System;
using System.Diagnostics;
using System.Globalization;
using System.Security.Policy;
using System.Windows;
using UWPHook.Core.Configuration;
using UWPHook.Infrastructure;
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

            var settings = UserSettings.Current;
            cultures_comboBox.ItemsSource = CultureInfo.GetCultures(CultureTypes.AllCultures).Select(c => c.TextInfo.CultureName);
            cultures_comboBox.SelectedItem = string.IsNullOrEmpty(settings.TargetLanguage) ? CultureInfo.CurrentCulture.TextInfo.CultureName : settings.TargetLanguage;

            // Item i reads "i seconds".
            seconds_comboBox.ItemsSource = Enumerable.Range(0, 10).Select(i => i + " seconds");
            seconds_comboBox.SelectedIndex = settings.Seconds;

            language_toggle.IsChecked = settings.ChangeLanguage;
            streaming_toggle.IsChecked = settings.StreamMode;
            // Items: ERROR, DEBUG, TRACE, in the order of AppLogLevel.
            logLevel_comboBox.SelectedIndex = (int)settings.LogLevel;
            steamgriddb_api_key.Text = settings.SteamGridDbApiKey;
            style_comboBox.SelectedIndex = ArtworkChoices.Styles.IndexOf(settings.ArtworkStyle);
            type_comboBox.SelectedIndex = ArtworkChoices.Types.IndexOf(settings.ArtworkType);
            nfsw_comboBox.SelectedIndex = ArtworkChoices.Nsfw.IndexOf(settings.ArtworkNsfw);
            humor_comboBox.SelectedIndex = ArtworkChoices.Humor.IndexOf(settings.ArtworkHumor);
            tags_textBox.Text = settings.Tags;
        }

        private void saveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UserSettings.Update(Edited);
            }
            catch (Exception exception)
            {
                Infrastructure.ErrorReporter.Report(exception, "Saving your settings failed.");
                return;
            }
            GamesWindow.SetLogLevel();
            this.Close();
        }

        /// <summary>The settings as edited in this window. Nothing selected keeps the stored value.</summary>
        private AppSettings Edited(AppSettings settings) => settings with
        {
            ChangeLanguage = language_toggle.IsChecked == true,
            TargetLanguage = cultures_comboBox.SelectedItem as string ?? settings.TargetLanguage,
            Seconds = seconds_comboBox.SelectedIndex >= 0 ? seconds_comboBox.SelectedIndex : settings.Seconds,
            StreamMode = streaming_toggle.IsChecked == true,
            LogLevel = Enum.IsDefined((AppLogLevel)logLevel_comboBox.SelectedIndex) ? (AppLogLevel)logLevel_comboBox.SelectedIndex : settings.LogLevel,
            SteamGridDbApiKey = steamgriddb_api_key.Text.Trim('\r', '\n'),
            ArtworkStyle = ArtworkChoices.Styles.ValueAt(style_comboBox.SelectedIndex, settings.ArtworkStyle),
            ArtworkType = ArtworkChoices.Types.ValueAt(type_comboBox.SelectedIndex, settings.ArtworkType),
            ArtworkNsfw = ArtworkChoices.Nsfw.ValueAt(nfsw_comboBox.SelectedIndex, settings.ArtworkNsfw),
            ArtworkHumor = ArtworkChoices.Humor.ValueAt(humor_comboBox.SelectedIndex, settings.ArtworkHumor),
            Tags = tags_textBox.Text,
        };

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

        private async void clearAll_button_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show("This action will remove every shortcut UWPHook added to Steam. Non-Steam games added by Steam or other tools are kept, and shortcuts.vdf is backed up first." + Environment.NewLine + "Are you sure you want to continue?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            clearAll_button.IsEnabled = false;
            try
            {
                await GamesWindow.RemoveUwpHookShortcuts();
            }
            catch (Exception exception)
            {
                Infrastructure.ErrorReporter.Report(exception, "Removing UWPHook's shortcuts failed.");
            }
            finally
            {
                clearAll_button.IsEnabled = true;
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