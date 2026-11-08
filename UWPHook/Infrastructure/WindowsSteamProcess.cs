using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using Serilog;
using UWPHook.Core.Steam;

namespace UWPHook.Infrastructure
{
    /// <summary>The real Steam client on this Windows machine.</summary>
    public sealed class WindowsSteamProcess : ISteamProcess
    {
        private const string ProcessName = "steam";

        public bool IsRunning()
        {
            var processes = Process.GetProcessesByName(ProcessName);
            try
            {
                return processes.Any();
            }
            finally
            {
                foreach (var process in processes) process.Dispose();
            }
        }

        public string? ExecutablePath() => PathFromRegistry() ?? PathFromRunningProcess();

        public void RequestExit()
        {
            var exePath = ExecutablePath();
            if (exePath is null) return;

            Log.Debug("Requesting Steam shutdown");
            using var _ = Process.Start(new ProcessStartInfo(exePath, "-shutdown") { UseShellExecute = false });
        }

        public void Start(string exePath)
        {
            Log.Debug("Starting Steam");
            using var _ = Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
        }

        /// <summary>HKCU\Software\Valve\Steam\SteamExe, which Steam writes with forward slashes.</summary>
        private static string? PathFromRegistry()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                if (key?.GetValue("SteamExe") is not string value || string.IsNullOrWhiteSpace(value)) return null;

                var path = Path.GetFullPath(value);
                return File.Exists(path) ? path : null;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not read the Steam path from the registry");
                return null;
            }
        }

        /// <summary>Reading another process's MainModule can fail (access denied, 32/64-bit mismatch).</summary>
        private static string? PathFromRunningProcess()
        {
            var processes = Process.GetProcessesByName(ProcessName);
            try
            {
                foreach (var process in processes)
                {
                    try
                    {
                        var path = process.MainModule?.FileName;
                        if (!string.IsNullOrEmpty(path)) return path;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Could not read the path of the running Steam process");
                    }
                }
                return null;
            }
            finally
            {
                foreach (var process in processes) process.Dispose();
            }
        }
    }
}
