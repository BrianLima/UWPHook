using System.IO;
using Serilog;
using UWPHook.Core.Configuration;

namespace UWPHook.Infrastructure
{
    /// <summary>
    /// The app's settings, in <c>%AppData%\Briano\UWPHook\settings.json</c>: a thin static facade over one
    /// <see cref="SettingsService"/> (load once, import 2.14 once), until Task 10c-2's composition root owns it.
    /// </summary>
    internal static class UserSettings
    {
        public static string FilePath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Briano", "UWPHook", "settings.json");

        // One store for the app's lifetime: it remembers a key it could not decrypt, so saving does not erase it.
        private static readonly Lazy<SettingsService> service = new Lazy<SettingsService>(() => new SettingsService(
            NewStore(),
            () => LegacySettingsImport.From(LegacySettingsImport.DefaultBrianoFolder),
            SerilogLog.Instance));

        public static AppSettings Current => service.Value.Current;

        /// <summary>Changes the settings and saves them. Throws when saving fails (the change is then not kept).</summary>
        public static void Update(Func<AppSettings, AppSettings> change) => service.Value.Update(change);

        private static SettingsStore NewStore() => new SettingsStore(FilePath, new DpapiSecretProtector())
        {
            OnUnreadableFile = (e, movedTo) =>
            {
                if (movedTo is null)
                    Log.Error(e, "settings.json could not be read, so the defaults are used for now; the file was left as it is");
                else
                    Log.Error(e, "settings.json is damaged, so the defaults are used; it was moved to {MovedTo}", movedTo);
            },
            OnUndecryptableKey = e => Log.Warning(e, "The SteamGridDB key in settings.json was saved by another Windows user or PC and cannot be read; enter it again in Settings"),
        };
    }
}
