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
        public static EventsHook eventsHook;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            icon = new TrayIcon();

            eventsHook = new EventsHook();

            eventsHook.StartHooking();

        }

    }
}
