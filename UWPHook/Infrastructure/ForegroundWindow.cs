using System.Diagnostics;
using System.Runtime.InteropServices;
using Serilog;

namespace UWPHook.Infrastructure
{
    /// <summary>Best-effort: brings a process's main window to the front, restoring it if minimized.</summary>
    internal static class ForegroundWindow
    {
        private const int SwShowMaximized = 3;

        /// <summary>Never throws: the process may already have exited, or have no window yet.</summary>
        public static void TryBring(int processId)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                var window = process.MainWindowHandle;
                if (window == IntPtr.Zero) return;

                if (IsIconic(window)) ShowWindowAsync(window, SwShowMaximized);
                SetForegroundWindow(window);
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                Log.Debug("Could not bring process {ProcessId} to the foreground: {Message}", processId, e.Message);
            }
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);
    }
}
