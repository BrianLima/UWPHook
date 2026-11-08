using System.IO;
using UWPHook.Core.Catalog;

namespace UWPHook.Infrastructure
{
    /// <summary>Turns catalog apps into the grid's <see cref="AppEntry"/> rows.</summary>
    internal static class XboxAppEntries
    {
        /// <summary>
        /// Maps an app to a row. <c>KnownApps.json</c> still overrides the name, and <see cref="AppEntry.IconPath"/>
        /// is the logo's folder, where <see cref="AppEntry.widestSquareIcon"/> looks for the largest square image.
        /// </summary>
        public static AppEntry ToAppEntry(this XboxApp app) => new AppEntry
        {
            Name = AppManager.IsKnownApp(app.Aumid, out string knownName) ? knownName : app.DisplayName,
            Executable = app.Executable,
            Aumid = app.Aumid,
            IconPath = (app.LogoPath is null ? null : Path.GetDirectoryName(app.LogoPath)) ?? app.InstallLocation,
            Selected = false,
        };
    }
}
