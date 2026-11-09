using UWPHook.Core.Platform;

namespace UWPHook.Infrastructure
{
    /// <summary>The version of the Windows this process runs on.</summary>
    internal sealed class WindowsOsVersion : IOsVersion
    {
        public int Major => 0;

        public int Minor => 0;

        public int Build => 0;

        /// <summary>The capabilities of this machine, worked out once.</summary>
        public static PlatformCapabilities Capabilities { get; } = PlatformCapabilities.Of(new WindowsOsVersion());
    }
}
