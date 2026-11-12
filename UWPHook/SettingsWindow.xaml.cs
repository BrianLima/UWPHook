using System.Windows;
using System.Windows.Controls;
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
            KeepInStep(apiKey_passwordBox, settings.Form);

            // The ViewModel decides whether the window may close (not while removing) and tells the user why not.
            Closing += (_, e) => e.Cancel = !settings.ConfirmClose();
        }

        /// <summary>
        /// A PasswordBox's password can't be bound (WPF keeps it out of bindings), so the masked key box and
        /// <see cref="SettingsForm.SteamGridDbApiKey"/> are kept in step here. The form lives only as long as this window.
        /// </summary>
        private static void KeepInStep(PasswordBox box, SettingsForm form)
        {
            box.Password = form.SteamGridDbApiKey;
            box.PasswordChanged += (_, _) => form.SteamGridDbApiKey = box.Password;
            form.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SettingsForm.SteamGridDbApiKey) && box.Password != form.SteamGridDbApiKey)
                    box.Password = form.SteamGridDbApiKey;
            };
        }
    }
}
