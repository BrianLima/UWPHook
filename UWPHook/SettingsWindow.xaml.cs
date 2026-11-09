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
        }
    }
}
