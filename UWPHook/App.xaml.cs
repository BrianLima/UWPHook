using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using UWPHook.Infrastructure;

namespace UWPHook
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private const string UnexpectedError = "UWPHook ran into an unexpected error.";

        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

            base.OnStartup(e);
        }

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
