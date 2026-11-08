using UWPHook.Core.Diagnostics;

namespace UWPHook.Core.Configuration;

public sealed class SettingsService
{
    public SettingsService(SettingsStore store, Func<AppSettings?> importLegacy, ILog log)
    {
    }

    public AppSettings Current => throw new NotImplementedException();

    public void Update(Func<AppSettings, AppSettings> change) => throw new NotImplementedException();
}
