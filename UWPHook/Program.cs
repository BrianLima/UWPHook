using Velopack;

namespace UWPHook
{
    /// <summary>
    /// UWPHook's entry point (<c>&lt;StartupObject&gt;</c> in the csproj), replacing the Main that WPF generates for App.xaml.
    /// </summary>
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // Must run first: when Velopack starts UWPHook with an install/update/uninstall hook
            // (--veloapp-*), it handles it and exits the process right here, before any window opens.
            // On a normal launch (or when not installed by Velopack) it returns at once.
            VelopackApp.Build().Run();

            // Same as the generated Main. App.OnStartup reads the launch request from
            // Environment.GetCommandLineArgs(), so args need no forwarding.
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
    }
}
