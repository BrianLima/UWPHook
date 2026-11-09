using System.Runtime.Versioning;
using Serilog;
using UWPHook.Core.Catalog;
using UWPHook.Core.Platform;
using Windows.ApplicationModel;

namespace UWPHook.Infrastructure
{
    /// <summary>
    /// Reads a package's folders with the newest API this Windows has: the effective folder only where it exists
    /// (1903+), and the plain path strings instead of a <c>StorageFolder</c> where they exist (2004+).
    /// </summary>
    internal sealed class WindowsPackageLocations(PlatformCapabilities capabilities)
    {
        /// <summary>A package whose effective folder cannot be read is still found, through its installed folder.</summary>
        public PackageLocations Of(Package package) =>
            capabilities.EffectivePackageLocation
                ? PackageLocations.Of(InstalledOf(package), EffectiveReader(package), e => LogUnreadable(package, e))
                : new(InstalledOf(package));

        private string InstalledOf(Package package) =>
            capabilities.PackagePathProperties ? package.InstalledPath : package.InstalledLocation.Path;

        [SupportedOSPlatform("windows10.0.18362")]
        private Func<string?> EffectiveReader(Package package) => () => EffectiveOf(package);

        private static void LogUnreadable(Package package, Exception e) =>
            Log.Debug(e, "The effective folder of {FullName} cannot be read; using its installed folder", package.Id.FullName);

        [SupportedOSPlatform("windows10.0.18362")]
        private string EffectiveOf(Package package) =>
            capabilities.PackagePathProperties ? package.EffectivePath : package.EffectiveLocation.Path;
    }
}
