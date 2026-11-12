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

        private readonly ISteamLocator _steam;

        /// <param name="steam">The Steam install that export writes to, so the restart works on the same one.</param>
        public WindowsSteamProcess(ISteamLocator steam)
        {
            _steam = steam;
        }

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

        /// <summary>steam.exe of the install that export writes to (<see cref="ISteamLocator"/>), else the registry's SteamExe, else the running process.</summary>
        public string? ExecutablePath() => _steam.SteamExecutable() ?? PathFromRegistry() ?? PathFromRunningProcess();

        public void RequestExit()
        {
            var exePath = ExecutablePath();
            if (exePath is null) return;

            Log.Debug("Requesting Steam shutdown");
            using var _ = Process.Start(new ProcessStartInfo(exePath, "-shutdown") { UseShellExecute = false });
        }

        /// <summary>
        /// Never throws: the shortcuts are already written, so a Steam that won't start is logged and reported
        /// as false, for the summary to tell the user to start it.
        /// </summary>
        public bool Start(string exePath)
        {
            Log.Debug("Starting Steam");
            try
            {
                using var _ = Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
                return true;
            }
            catch (Exception exception)
            {
                Log.Error(exception, "Could not start Steam from {Path}", exePath);
                return false;
            }
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
