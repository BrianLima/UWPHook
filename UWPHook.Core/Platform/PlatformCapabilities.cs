using System.Runtime.Versioning;

namespace UWPHook.Core.Platform;

/// <summary>
/// The features whose Windows API is newer than the oldest Windows UWPHook supports (Windows 10 1809, build 17763).
/// App code asks for a capability, never for the build number. A missing capability means: skip or fall back, never fail.
/// </summary>
public sealed record PlatformCapabilities
{
    /// <summary><c>Package.EffectiveLocation</c>, Windows 10 1903 (build 18362).</summary>
    [SupportedOSPlatformGuard("windows10.0.18362")]
    public bool EffectivePackageLocation { get; init; }

    /// <summary><c>Package.InstalledPath</c> and <c>Package.EffectivePath</c> as plain strings, Windows 10 2004 (build 19041).</summary>
    [SupportedOSPlatformGuard("windows10.0.19041")]
    public bool PackagePathProperties { get; init; }

    public static PlatformCapabilities Of(IOsVersion os) => new();
}
