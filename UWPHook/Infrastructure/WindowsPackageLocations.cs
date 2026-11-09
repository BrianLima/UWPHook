using System.Runtime.Versioning;
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
        public PackageLocations Of(Package package) =>
            new(InstalledOf(package), capabilities.EffectivePackageLocation ? EffectiveOf(package) : null);

        private string InstalledOf(Package package) =>
            capabilities.PackagePathProperties ? package.InstalledPath : package.InstalledLocation.Path;

        [SupportedOSPlatform("windows10.0.18362")]
        private string EffectiveOf(Package package) =>
            capabilities.PackagePathProperties ? package.EffectivePath : package.EffectiveLocation.Path;
    }
}
