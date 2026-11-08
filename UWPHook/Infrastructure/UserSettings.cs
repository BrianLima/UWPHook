using System.IO;
using Serilog;
using UWPHook.Core.Configuration;

namespace UWPHook.Infrastructure
{
    /// <summary>
    /// The app's settings, in <c>%AppData%\Briano\UWPHook\settings.json</c>. Loaded on first use; when the
    /// file does not exist yet, 2.14's settings are imported once and saved.
    /// </summary>
    internal static class UserSettings
    {
        private static readonly object gate = new object();
        private static AppSettings? current;

        public static string FilePath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Briano", "UWPHook", "settings.json");

        public static AppSettings Current
        {
            get
            {
                lock (gate) return current ??= LoadOrImport();
            }
        }

        /// <summary>Changes the settings and saves them. Throws when saving fails (the change is then not kept).</summary>
        public static void Update(Func<AppSettings, AppSettings> change)
        {
            lock (gate)
            {
                var changed = change(current ?? LoadOrImport());
                Store().Save(changed);
                current = changed;
            }
        }

        // One store for the app's lifetime: it remembers a key it could not decrypt, so saving does not erase it.
        private static readonly Lazy<SettingsStore> lazyStore = new Lazy<SettingsStore>(() => new SettingsStore(FilePath, new DpapiSecretProtector())
        {
            OnUnreadableFile = (e, movedTo) =>
            {
                if (movedTo is null)
                    Log.Error(e, "settings.json could not be read, so the defaults are used for now; the file was left as it is");
                else
                    Log.Error(e, "settings.json is damaged, so the defaults are used; it was moved to {MovedTo}", movedTo);
            },
            OnUndecryptableKey = e => Log.Warning(e, "The SteamGridDB key in settings.json was saved by another Windows user or PC and cannot be read; enter it again in Settings"),
        });

        private static SettingsStore Store() => lazyStore.Value;

        private static AppSettings LoadOrImport()
        {
            var store = Store();
            // An existing file is never overwritten here, also when it cannot be read right now.
            if (store.Exists) return store.Load();

            var imported = LegacySettingsImport.From(LegacySettingsImport.DefaultBrianoFolder) ?? AppSettings.Defaults;
            try
            {
                store.Save(imported);
            }
            catch (Exception e)
            {
                // Still usable for this run; the import is tried again next time.
                Log.Error(e, "Saving {Path} failed", FilePath);
            }
            return imported;
        }
    }
}
