using System.Windows;
using System.Windows.Threading;
using UWPHook.Core.Steam;
using UWPHook.Presentation;

namespace UWPHook.Infrastructure
{
    /// <summary>
    /// The ViewModels' messages as message boxes, always on the UI thread of <paramref name="dispatcher"/>: callers on a
    /// worker thread (the SteamGridDB key warning during an export) wait until the box is closed.
    /// </summary>
    internal sealed class WpfDialogs(Dispatcher dispatcher) : IDialogService
    {
        public void Show(MessageSeverity severity, string message, string title = "UWPHook") =>
            OnUiThread(() => MessageBox.Show(message, title, MessageBoxButton.OK, ImageFor(severity)));

        public bool Confirm(string message, ConfirmTone tone) =>
            OnUiThread(() => tone == ConfirmTone.Warning
                ? MessageBox.Show(message, "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning)
                : MessageBox.Show(message, "UWPHook", MessageBoxButton.YesNo, MessageBoxImage.Question)) == MessageBoxResult.Yes;

        public void ReportError(Exception exception, string userMessage) => ErrorReporter.Report(exception, userMessage);

        private static MessageBoxImage ImageFor(MessageSeverity severity) => severity switch
        {
            MessageSeverity.Error => MessageBoxImage.Error,
            MessageSeverity.Warning => MessageBoxImage.Warning,
            _ => MessageBoxImage.Information,
        };

        private T OnUiThread<T>(Func<T> show) => dispatcher.CheckAccess() ? show() : dispatcher.Invoke(show);
    }
}
