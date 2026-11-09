using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using UWPHook.Core.Launch;
using UWPHook.Infrastructure;

namespace UWPHook
{
    /// <summary>
    /// Starts UWPHook: the global error handlers, the log, then either a game session for Steam
    /// (no window) or the games list.
    /// </summary>
    public partial class App : Application
    {
        private const string UnexpectedError = "UWPHook ran into an unexpected error.";

        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

            // Decided before anything else can fail: a launch from Steam has no window to close, so it
            // must shut down explicitly, also when startup itself throws.
            var args = Environment.GetCommandLineArgs();
            var isLaunch = HasLaunchArguments(args);
            if (isLaunch) ShutdownMode = ShutdownMode.OnExplicitShutdown;

            try
            {
                base.OnStartup(e);
                var services = new AppServices(); // the composition root, built once
                AppLog.Configure(() => services.Settings.Current.LogLevel);

                if (LaunchRequest.TryParse(args, out var request))
                    RunGameSession(request!, services.Settings);
                else if (isLaunch)
                    ExitAfter(() => GameLauncher.ReportInvalidArguments(args));
                else
                    ShowGamesWindow(services);
            }
            catch (Exception ex)
            {
                // No window was shown (or none ever will be in a launch), so nothing would ever end
                // the process: report and exit.
                ErrorReporter.Report(ex, UnexpectedError);
                Shutdown();
            }
        }

        /// <summary>
        /// Started by Steam: no main window (it would hold the GPU and get in the way of streaming
        /// and NVIDIA Advanced Optimus). UWPHook exits when the game's session ends.
        /// </summary>
        private async void RunGameSession(LaunchRequest request, Core.Configuration.ISettingsService settings)
        {
            try
            {
                await GameLauncher.RunAsync(request, settings);
            }
            finally
            {
                Shutdown();
            }
        }

        private void ShowGamesWindow(AppServices services)
        {
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            MainWindow = services.GamesWindow(Dispatcher);
            MainWindow.Show();
        }

        private void ExitAfter(Action action)
        {
            try
            {
                action();
            }
            finally
            {
                Shutdown();
            }
        }

        /// <summary>
        /// Arguments other than Velopack's own (--veloapp-*) mean Steam meant to launch something.
        /// Index 0 is UWPHook itself.
        /// </summary>
        private static bool HasLaunchArguments(string[] args) =>
            args.Skip(1).Any(arg => !arg.StartsWith("--veloapp", StringComparison.OrdinalIgnoreCase));

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ErrorReporter.Report(e.Exception, UnexpectedError);
            e.Handled = true; // keep the app open
        }

        private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            // Raised on the finalizer thread: queue the box instead of blocking finalization on the UI.
            ErrorReporter.ReportWithoutWaiting(e.Exception, UnexpectedError);
            e.SetObserved();
        }

        private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString());
            ErrorReporter.Report(ex, UnexpectedError);
        }
    }
}
