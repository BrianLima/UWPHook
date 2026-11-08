using System.Globalization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Serilog;
using UWPHook.Core.Launch;
using UWPHook.Infrastructure;

namespace UWPHook
{
    /// <summary>
    /// What UWPHook does when Steam starts it with a launch request: start the game and stay alive,
    /// with no main window, until the game's package has no process left. Steam counts playtime and
    /// shows "Playing" for as long as this runs.
    /// </summary>
    internal static class GameLauncher
    {
        /// <summary>
        /// How long the game's package may have no process before the session ends. Covers launchers
        /// that exit before the game appears (Halo MCC, Forza Horizon 5, Fallout 76).
        /// </summary>
        public static readonly TimeSpan LauncherGrace = TimeSpan.FromSeconds(10);

        /// <summary>Stream mode: how long the splash stays up so Steam's stream catches up before the game starts.</summary>
        private static readonly TimeSpan StreamSplashDuration = TimeSpan.FromSeconds(10);

        /// <summary>Runs the whole session. Never throws: errors are logged and shown in a message box.</summary>
        public static async Task RunAsync(LaunchRequest request)
        {
            var languageToRestore = (string?)null;

            try
            {
                if (UserSettings.Current.StreamMode) await ShowStreamSplashAsync();

                languageToRestore = LanguageToRestore();
                if (languageToRestore is not null) OverrideLanguage();

                var session = new GameSession(new ComActivator(), new PackageProcessWatcher(), TimeProvider.System)
                {
                    OnWatcherError = e => Log.Warning(e, "Looking for processes of {FamilyName} failed", request.PackageFamilyName),
                };
                LogOutcome(request, await session.RunAsync(request, PollInterval(), LauncherGrace));
            }
            catch (Exception e)
            {
                Log.Error(e, "Launching {Aumid} failed", request.Aumid);
                MessageBox.Show(e.Message, "UWPHook", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                RestoreLanguage(languageToRestore);
            }
        }

        private static void LogOutcome(LaunchRequest request, SessionOutcome outcome)
        {
            switch (outcome)
            {
                case SessionOutcome.NeverStarted:
                    // Error, so it is written at the default (Error) log level: a game that never ran is worth noticing.
                    Log.Error("No process of {FamilyName} was seen after launching {Aumid}; the game may have failed to start",
                        request.PackageFamilyName, request.Aumid);
                    break;
                case SessionOutcome.WatcherFailed:
                    Log.Error("Gave up watching {Aumid} ({FamilyName}): looking for its processes failed {Count} times in a row",
                        request.Aumid, request.PackageFamilyName, GameSession.MaxConsecutiveWatcherFailures);
                    break;
                default:
                    Log.Information("Session for {Aumid} finished: {Outcome}", request.Aumid, outcome);
                    break;
            }
        }

        /// <summary>Shows a broken launch (Steam passed arguments, but no AUMID). Never throws.</summary>
        public static void ReportInvalidArguments(string[] args)
        {
            Log.Error("Invalid launch arguments => " + string.Join(" | ", args));
            MessageBox.Show("Invalid launch arguments", "UWPHook", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        /// <summary>
        /// Settings → "Seconds" is how often to check whether the game is still running.
        /// At least one second, so a 0 does not turn the check into a busy loop.
        /// </summary>
        private static TimeSpan PollInterval() => TimeSpan.FromSeconds(Math.Max(1, UserSettings.Current.Seconds));

        /// <summary>
        /// The splash is the only window a launch may open. It renders in software, set before any window
        /// exists, so it does not hold the GPU and block NVIDIA Advanced Optimus from switching the
        /// display to the dGPU when the game starts (#165).
        /// </summary>
        private static async Task ShowStreamSplashAsync()
        {
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

            var splash = new FullScreenLauncher();
            splash.Show();
            try
            {
                await Task.Delay(StreamSplashDuration);
            }
            finally
            {
                splash.Close();
            }
        }

        /// <summary>
        /// The language to put back after the game, or null when the language override is off.
        /// Read before overriding, so a restore happens even if the override script fails half-way.
        /// </summary>
        private static string? LanguageToRestore() =>
            UserSettings.Current.ChangeLanguage && !string.IsNullOrEmpty(UserSettings.Current.TargetLanguage)
                ? CultureInfo.CurrentCulture.ToString()
                : null;

        /// <summary>Some games follow the Windows UI language, so overriding it can change the game's language.</summary>
        private static void OverrideLanguage() =>
            ScriptManager.RunScript("Set-WinUILanguageOverride " + UserSettings.Current.TargetLanguage);

        /// <summary>Never throws: a failed restore is logged and must not keep UWPHook (and Steam's "Playing") alive.</summary>
        private static void RestoreLanguage(string? language)
        {
            if (language is null) return;

            try
            {
                ScriptManager.RunScript("Set-WinUILanguageOverride " + language);
            }
            catch (Exception e)
            {
                Log.Error(e, "Restoring the Windows UI language to {Language} failed", language);
            }
        }
    }
}
