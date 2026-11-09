using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using UWPHook.Core.Launch;
using UWPHook.Core.Steam;
using UWPHook.Infrastructure;

namespace UWPHook
{
    /// <summary>
    /// Interaction logic for GamesWindow.xaml
    /// </summary>
    public partial class GamesWindow : Window
    {
        AppEntryModel Apps;
        BackgroundWorker bwrLoad;

        public GamesWindow()
        {
            InitializeComponent();
            Log.Debug("Init GamesWindow");
            Apps = new AppEntryModel();

            //auto refresh on load
            LoadButton_Click(null, null);
        }

        /// <summary>
        /// Exports the selected apps with the UI locked, then shows what really happened
        /// (see <see cref="ExportSummary"/>): a success message only when every Steam account was written.
        /// </summary>
        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            grid.IsEnabled = false;
            progressBar.Visibility = Visibility.Visible;

            ExportSummary? summary = null;
            var selected = Apps.Entries.Where(app => app.Selected).ToList();
            string tags = UserSettings.Current.Tags;

            try
            {
                // Off the UI thread: reading icons, downloads and file writes. The request is built there too,
                // because finding each app's widest icon reads its files.
                summary = await Task.Run(() => SteamServices.Exporter(ShowInvalidKeyMessage)
                    .ExportAsync(new ExportRequest(selected.Select(ToExportApp).ToList(), tags, StableExePath())));
                Log.Information("Export finished: {Summary}", summary);
            }
            catch (Exception exception)
            {
                Infrastructure.ErrorReporter.Report(exception, "Exporting your games failed.");
            }
            finally
            {
                grid.IsEnabled = true;
                progressBar.Visibility = Visibility.Collapsed;
            }

            if (summary != null)
            {
                MessageBox.Show(summary.Message, "UWPHook", MessageBoxButton.OK, ImageFor(summary.Severity));
            }
        }

        private static ExportApp ToExportApp(AppEntry app) => new ExportApp(app.Name, app.Aumid, app.Executable, app.widestSquareIcon());

        /// <summary>The UWPHook.exe path that survives updates (the stable launcher path).</summary>
        private static string StableExePath() =>
            UWPHook.Core.Install.InstallLayout.StableExePath(Environment.ProcessPath ?? throw new InvalidOperationException("Cannot find the path of UWPHook.exe"));

        /// <summary>Once per export, before Steam is touched; the key is kept so the user can fix it in Settings.</summary>
        private static void ShowInvalidKeyMessage() =>
            Application.Current.Dispatcher.Invoke(() =>
                MessageBox.Show(Infrastructure.SteamGridDbService.InvalidKeyMessage, "UWPHook", MessageBoxButton.OK, MessageBoxImage.Warning));

        private static MessageBoxImage ImageFor(MessageSeverity severity) => severity switch
        {
            MessageSeverity.Error => MessageBoxImage.Error,
            MessageSeverity.Warning => MessageBoxImage.Warning,
            _ => MessageBoxImage.Information,
        };

        /// <summary>
        /// Removes the shortcuts UWPHook created from every Steam user (see <see cref="ShortcutRemover"/>)
        /// and shows what happened.
        /// </summary>
        public static async Task RemoveUwpHookShortcuts()
        {
            RemoveSummary summary = await Task.Run(() => SteamServices.Remover().RemoveAsync());
            Log.Information("Remove finished: {Summary}", summary);
            MessageBox.Show(summary.Message, "UWPHook", MessageBoxButton.OK, ImageFor(summary.Severity));
        }

        /// <summary>
        /// Fires the Bwr_DoWork, to load the apps installed at the machine
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            bwrLoad = new BackgroundWorker();
            bwrLoad.DoWork += Bwr_DoWork;
            bwrLoad.RunWorkerCompleted += Bwr_RunWorkerCompleted;

            grid.IsEnabled = false;
            label.Content = "Loading your installed apps";

            progressBar.Visibility = Visibility.Visible;
            Apps.Entries = new System.Collections.ObjectModel.ObservableCollection<AppEntry>();

            bwrLoad.RunWorkerAsync();
        }

        /// <summary>
        /// Callback for restoring the grid list interactivity
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Bwr_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            listGames.ItemsSource = Apps.Entries;

            listGames.Columns[2].IsReadOnly = true;
            listGames.Columns[3].IsReadOnly = true;

            grid.IsEnabled = true;
            progressBar.Visibility = Visibility.Collapsed;
            label.Content = "Installed Apps";
        }

        /// <summary>
        /// Worker responsible for loading the apps installed in the machine
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Bwr_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                // The 2.14 script listed every non-framework package, system apps included, and the UI has no
                // "hide system apps" option, so keep listing them all.
                Core.Catalog.IAppCatalog catalog = new Infrastructure.PackageManagerCatalog();
                var installedApps = catalog.GetInstalledAppsAsync(includeSystem: true, CancellationToken.None).GetAwaiter().GetResult();

                foreach (var app in installedApps.OrderBy(a => a.DisplayName, StringComparer.CurrentCultureIgnoreCase))
                {
                    AppEntry entry = Infrastructure.XboxAppEntries.ToAppEntry(app);
                    Application.Current.Dispatcher.BeginInvoke((Action)delegate ()
                    {
                        Apps.Entries.Add(entry);
                    });
                }
            }
            catch (Exception ex)
            {
                // Logs the whole exception and shows the box on the UI thread, not this worker's
                Infrastructure.ErrorReporter.Report(ex, "Loading your installed apps failed.");
            }
        }

        private void textBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (Apps.Entries != null)
            {
                if (!String.IsNullOrEmpty(textBox.Text) && Apps.Entries.Count > 0)
                {
                    listGames.Items.Filter = new Predicate<object>(Contains);
                }
                else
                {
                    listGames.Items.Filter = null;
                }
            }
        }

        public bool Contains(object o)
        {
            AppEntry appEntry = o as AppEntry;
            return (appEntry.Aumid.ToLower().Contains(textBox.Text.ToLower()) || appEntry.Name.ToLower().Contains(textBox.Text.ToLower()));
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow window = new SettingsWindow();
            window.ShowDialog();
        }

        /// <summary>
        /// Function that executes when the Games Window is loaded
        /// Will inform the user of the possibility of using the SteamGridDB API
        /// redirecting him to the settings page if he wishes to use the functionality
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!UserSettings.Current.OfferedSteamGridDb)
            {
                // Only offer: a key the user already entered is kept.
                try
                {
                    UserSettings.Update(settings => settings with { OfferedSteamGridDb = true });
                }
                catch (Exception exception)
                {
                    Log.Error(exception, "Saving that SteamGridDB was offered failed");
                }

                var boxResult = MessageBox.Show("Do you want to automatically import grid images for imported games?", "UWPHook", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (boxResult == MessageBoxResult.Yes)
                {
                    SettingsButton_Click(this, null);
                }
            }
        }

        /// <summary>Applies the log level picked in Settings.</summary>
        public static void SetLogLevel() => Infrastructure.AppLog.ApplySelectedLevel();
    }
}
