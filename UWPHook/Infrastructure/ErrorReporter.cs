using System.Windows;
using Serilog;
using UWPHook.Core.Diagnostics;

namespace UWPHook.Infrastructure
{
    /// <summary>
    /// The single place errors go to: the log, then a message box on the UI thread.
    /// Only one error box is shown at a time; errors raised while it is open are only logged.
    /// Never throws, so it is safe to call from any handler.
    /// </summary>
    public static class ErrorReporter
    {
        private static readonly ReentrancyGuard dialog = new ReentrancyGuard();

        /// <summary>Logs the error and shows it, waiting until the box is closed.</summary>
        public static void Report(Exception ex, string userMessage) =>
            Report(ex, userMessage, waitForDialog: true);

        /// <summary>
        /// Logs the error and queues the box on the UI thread without waiting for it.
        /// For threads that must not block on the UI, such as the finalizer thread
        /// that raises <see cref="TaskScheduler.UnobservedTaskException"/>.
        /// </summary>
        public static void ReportWithoutWaiting(Exception ex, string userMessage) =>
            Report(ex, userMessage, waitForDialog: false);

        private static void Report(Exception ex, string userMessage, bool waitForDialog)
        {
            TryLog(() => Log.Error(ex, "{UserMessage}", userMessage));

            if (!dialog.TryEnter())
            {
                TryLog(() => Log.Warning("An error dialog is already open; not showing another for: {UserMessage}", userMessage));
                return;
            }

            try
            {
                ShowOnUiThread(userMessage + Environment.NewLine + Environment.NewLine + ex.Message, waitForDialog);
            }
            catch
            {
                // The box never got shown (shutting down, or no dispatcher); the log has it.
                dialog.Exit();
            }
        }

        private static void ShowOnUiThread(string message, bool waitForDialog)
        {
            var dispatcher = Application.Current?.Dispatcher;

            if (dispatcher is null) ShowThenRelease(message);
            else if (dispatcher.HasShutdownStarted) dialog.Exit();
            else if (!waitForDialog) dispatcher.BeginInvoke(() => ShowThenRelease(message));
            else if (dispatcher.CheckAccess()) ShowThenRelease(message);
            else dispatcher.Invoke(() => ShowThenRelease(message));
        }

        /// <summary>Shows the box and frees the guard when it closes. Never throws.</summary>
        private static void ShowThenRelease(string message)
        {
            try
            {
                MessageBox.Show(message, "UWPHook", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                // No UI to show it on; the log has it.
            }
            finally
            {
                dialog.Exit();
            }
        }

        private static void TryLog(Action log)
        {
            try
            {
                log();
            }
            catch
            {
                // Logging must not turn one error into two.
            }
        }
    }
}
