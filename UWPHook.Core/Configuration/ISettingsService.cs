namespace UWPHook.Core.Configuration;

/// <summary>The app's settings for one run; see <see cref="SettingsService"/>.</summary>
public interface ISettingsService
{
    AppSettings Current { get; }

    /// <summary>Saves the changed settings first and keeps them only when that worked; otherwise throws.</summary>
    void Update(Func<AppSettings, AppSettings> change);
}
