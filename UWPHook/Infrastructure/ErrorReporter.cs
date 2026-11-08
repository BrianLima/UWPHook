using System.Windows;
using Serilog;

namespace UWPHook.Infrastructure
{
    /// <summary>
    /// The single place errors go to: the log, then a message box on the UI thread.
    /// Never throws, so it is safe to call from any handler.
    /// </summary>
    public static class ErrorReporter
    {
        public static void Report(Exception ex, string userMessage)
        {
            try
            {
                Log.Error(ex, "{UserMessage}", userMessage);
            }
            catch
            {
                // Logging must not turn one error into two.
            }

            try
            {
                ShowOnUiThread(userMessage + Environment.NewLine + Environment.NewLine + ex.Message);
            }
            catch
            {
                // No UI to show it on (shutting down, or no dispatcher); the log has it.
            }
        }

        private static void ShowOnUiThread(string message)
        {
            void Show() => MessageBox.Show(message, "UWPHook", MessageBoxButton.OK, MessageBoxImage.Error);

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess()) Show();
            else dispatcher.Invoke(Show);
        }
    }
}
