using System.IO;
using UWPHook.Core.Artwork;
using UWPHook.Core.Diagnostics;
using UWPHook.Core.Steam;

namespace UWPHook.Infrastructure
{
    /// <summary>
    /// Each shortcut's icon, copied to <c>%AppData%\Briano\UWPHook\icons</c> under one stable name per app (some apps
    /// move their files when they update, which "loses" the icon).
    /// </summary>
    /// <param name="icons">Picks the icon in an app's logo folder, by the same rule as the games list.</param>
    internal sealed class IconPersister(string iconsFolder, TempGridFolder temp, IIconPicker icons, ILog log)
    {
        /// <summary>
        /// The icon the games list picked and shows, else the app's widest square logo picked now (also when the list's
        /// file is gone, e.g. the app updated since), else the SteamGridDB logo downloaded for it, copied to the icons
        /// folder. When none can be copied, the app's own icon path (possibly empty) is kept.
        /// </summary>
        public string IconFor(ExportApp app)
        {
            if (TryPersist(app.Aumid, app.Icon) is { } shown) return shown;

            string own = icons.Pick(app.IconFolder) ?? "";
            return TryPersist(app.Aumid, own) ?? TryPersist(app.Aumid, temp.DownloadedLogo(app.Aumid)) ?? own;
        }

        /// <summary>Copies <paramref name="source"/> to <c>icons\&lt;key&gt;&lt;ext&gt;</c>, overwritten on every export. Null when it can't.</summary>
        private string? TryPersist(string aumid, string? source)
        {
            if (string.IsNullOrEmpty(source) || !File.Exists(source)) return null;

            string destination = Path.Combine(iconsFolder, temp.KeyOf(aumid) + Path.GetExtension(source));
            try
            {
                Directory.CreateDirectory(iconsFolder);
                File.Copy(source, destination, overwrite: true);
                return destination;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                log.Warning(e, "Could not copy icon {Icon} to {Destination}", source, destination);
                return null;
            }
        }
    }
}
