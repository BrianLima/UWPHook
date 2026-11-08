using System.IO;
using Serilog;
using UWPHook.Core.Configuration;

namespace UWPHook.Infrastructure
{
    /// <summary>
    /// The one-time import of 2.14's settings (.NET user.config). 2.15 runs from another exe path, so
    /// <c>Properties.Settings</c> would not see them: the newest user.config is read directly.
    /// </summary>
    internal static class LegacySettingsImport
    {
        public static string DefaultBrianoFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Briano");

        /// <summary>2.14's settings, or null when there are none (or they cannot be read).</summary>
        public static AppSettings? From(string brianoFolder)
        {
            try
            {
                var path = LegacyUserConfigLocator.Find(brianoFolder);
                if (path is null) return null;

                var settings = LegacyUserConfig.TryParse(File.ReadAllText(path));
                Log.Information("Imported the settings of an earlier UWPHook from {Path}: {Imported}", path, settings is not null);
                return settings;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Log.Warning(e, "Reading the settings of an earlier UWPHook failed; using the defaults");
                return null;
            }
        }
    }
}
