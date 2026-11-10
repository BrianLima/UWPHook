using System.ComponentModel;
using System.Windows;
using UWPHook.Presentation.Settings;

namespace UWPHook
{
    /// <summary>Settings. Its behaviour is <see cref="SettingsViewModel"/>; this only wires the view.</summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow(SettingsViewModel settings)
        {
            InitializeComponent();
            DataContext = settings;
            settings.CloseRequested += (_, _) => Close();

            // The ViewModel decides whether the window may close (not while removing) and tells the user why not.
            Closing += (_, e) => e.Cancel = !settings.ConfirmClose();
        }
    }
}
