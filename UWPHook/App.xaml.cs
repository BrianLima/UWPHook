using System;
using System.Windows;

namespace UWPHook
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static TrayIcon icon;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            icon = new TrayIcon();

            EventsHook eventsHook = new EventsHook();

            eventsHook.StartHooking();

        }

    }
}
