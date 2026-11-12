using System.Diagnostics;
using System.Reflection;
using UWPHook.Core.Configuration;
using UWPHook.Presentation;

namespace UWPHook.Infrastructure
{
    /// <summary>The running app as Settings sees it: its version, the browser, and the log level.</summary>
    internal sealed class AppHost : IAppHost
    {
        public string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";

        public void OpenUrl(string url) => Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });

        public void ApplyLogLevel(AppLogLevel level) => AppLog.ApplyLevel(level);
    }
}
