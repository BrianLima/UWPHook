using System.IO;
using UWPHook.Core.Diagnostics;
using UWPHook.Core.Steam;

namespace UWPHook.Infrastructure
{
    /// <summary>
    /// Each shortcut's icon, copied to <c>%AppData%\Briano\UWPHook\icons</c> under one stable name per app (some apps
    /// move their files when they update, which "loses" the icon).
    /// </summary>
    /// <param name="iconIn">The icon to use from an app's logo folder ("" when it has none), e.g. <see cref="SquareIcons.WidestIn"/>.</param>
    internal sealed class IconPersister(string iconsFolder, TempGridFolder temp, Func<string, string> iconIn, ILog log)
    {
        /// <summary>
        /// The app's widest square logo, else the SteamGridDB logo downloaded for it, copied to the icons folder.
        /// When neither can be copied, the app's own icon path (possibly empty) is kept.
        /// </summary>
        public string IconFor(ExportApp app)
        {
            string own = iconIn(app.IconFolder);
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
