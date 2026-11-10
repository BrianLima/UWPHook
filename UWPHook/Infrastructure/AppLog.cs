using System.IO;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using UWPHook.Core.Configuration;

namespace UWPHook.Infrastructure
{
    /// <summary>
    /// UWPHook's log: <c>%AppData%\Briano\UWPHook\application.log</c>, rotated at 10 MB, 5 files kept.
    /// Configured once at startup, so a launch from Steam (which opens no window) logs too.
    /// </summary>
    internal static class AppLog
    {
        private static readonly LoggingLevelSwitch levelSwitch = new LoggingLevelSwitch();

        /// <param name="level">The level chosen in Settings; read once the log exists, so loading the settings is logged too.</param>
        public static void Configure(Func<AppLogLevel> level)
        {
            string loggerFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Briano", "UWPHook", "application.log");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(levelSwitch)
                .WriteTo.File(path: loggerFilePath, rollOnFileSizeLimit: true, fileSizeLimitBytes: 10485760, retainedFileCountLimit: 5)
                .CreateLogger();

            // Switch to Info first so the chosen level is announced in the log, then to the chosen level.
            levelSwitch.MinimumLevel = LogEventLevel.Information;
            ApplyLevel(level());
        }

        /// <summary>Applies the level picked in Settings.</summary>
        public static void ApplyLevel(AppLogLevel level)
        {
            switch (level)
            {
                case AppLogLevel.Debug:
                    Log.Information("Init log with DEBUG level.");
                    levelSwitch.MinimumLevel = LogEventLevel.Debug;
                    break;
                case AppLogLevel.Trace:
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
