using System.IO;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using UWPHook.Properties;

namespace UWPHook.Infrastructure
{
    /// <summary>
    /// UWPHook's log: <c>%AppData%\Briano\UWPHook\application.log</c>, rotated at 10 MB, 5 files kept.
    /// Configured once at startup, so a launch from Steam (which opens no window) logs too.
    /// </summary>
    internal static class AppLog
    {
        private static readonly LoggingLevelSwitch levelSwitch = new LoggingLevelSwitch();

        public static void Configure()
        {
            string loggerFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Briano", "UWPHook", "application.log");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(levelSwitch)
                .WriteTo.File(path: loggerFilePath, rollOnFileSizeLimit: true, fileSizeLimitBytes: 10485760, retainedFileCountLimit: 5)
                .WriteTo.Console()
                .CreateLogger();

            // Switch to Info first so the chosen level is announced in the log, then to the chosen level.
            levelSwitch.MinimumLevel = LogEventLevel.Information;
            ApplySelectedLevel();
        }

        /// <summary>Applies the level picked in Settings (0 = error, 1 = debug, 2 = trace).</summary>
        public static void ApplySelectedLevel()
        {
            int.TryParse(Settings.Default.SelectedLogLevel, out var logLevelIndex);

            switch (logLevelIndex)
            {
                case 1:
                    Log.Information("Init log with DEBUG level.");
                    levelSwitch.MinimumLevel = LogEventLevel.Debug;
                    break;
                case 2:
                    Log.Information("Init log with TRACE level.");
                    levelSwitch.MinimumLevel = LogEventLevel.Verbose;
                    break;
                default:
                    Log.Information("Init log with ERROR level.");
                    levelSwitch.MinimumLevel = LogEventLevel.Error;
                    break;
            }
        }
    }
}
