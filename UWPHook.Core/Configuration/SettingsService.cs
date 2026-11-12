using UWPHook.Core.Diagnostics;

namespace UWPHook.Core.Configuration;

/// <summary>
/// The app's settings for one run: loaded on first use and kept. When there is no settings file yet, the
/// earlier UWPHook's settings are imported once and saved, unless a damaged settings file was moved aside:
/// then the newer settings were lost, and bringing back 2.14's (and maybe an old key) would be worse than
/// starting from the defaults.
/// </summary>
/// <param name="importLegacy">2.14's settings, or null when there are none.</param>
public sealed class SettingsService(SettingsStore store, Func<AppSettings?> importLegacy, ILog log) : ISettingsService
{
    private readonly Lock gate = new();
    private AppSettings? current;

    public AppSettings Current
    {
        get
        {
            lock (gate) return current ??= LoadOrImport();
        }
    }

    /// <summary>Saves the changed settings first and keeps them only when that worked; otherwise throws and keeps the old ones.</summary>
    public void Update(Func<AppSettings, AppSettings> change)
    {
        lock (gate)
        {
            var changed = change(current ??= LoadOrImport());
            store.Save(changed);
            current = changed;
        }
    }

    private AppSettings LoadOrImport()
    {
        // An existing file is never overwritten here, also when it cannot be read right now.
        if (store.Exists) return store.Load();

        if (store.HasMovedAsideFile)
        {
            log.Warning("{Path} is missing and a damaged copy was kept, so the earlier UWPHook's settings are not imported again; using the defaults", store.FilePath);
            return SavedIfPossible(AppSettings.Defaults);
        }

        return SavedIfPossible(importLegacy() ?? AppSettings.Defaults);
    }

    /// <summary>Still usable for this run when saving fails; then the import is tried again next time.</summary>
    private AppSettings SavedIfPossible(AppSettings settings)
    {
        try
        {
            store.Save(settings);
        }
        catch (Exception e)
        {
            log.Error(e, "Saving {Path} failed", store.FilePath);
        }

        return settings;
    }
}
