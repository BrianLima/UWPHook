using UWPHook.Core.Platform;

namespace UWPHook.Infrastructure
{
    /// <summary>The version of the Windows this process runs on (.NET reports the real version, not a compatibility one).</summary>
    internal sealed class WindowsOsVersion : IOsVersion
    {
        private readonly Version version = Environment.OSVersion.Version;

        public int Major => version.Major;

        public int Minor => version.Minor;

        public int Build => version.Build;

        /// <summary>The capabilities of this machine, worked out once.</summary>
        public static PlatformCapabilities Capabilities { get; } = PlatformCapabilities.Of(new WindowsOsVersion());
    }
}
