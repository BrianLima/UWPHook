using System.IO.Hashing;
using System.Text;

namespace UWPHook.Core.Steam;

/// <summary>
/// The id Steam gives a non-Steam shortcut. Steam derives it from the shortcut's
/// exact <c>Exe</c> string (quotes included) and its name, and uses it both as the
/// <c>appid</c> in shortcuts.vdf and as the file name of its grid images.
/// </summary>
public static class SteamShortcutId
{
    private const uint HighBit = 0x80000000;

    /// <summary>CRC-32 of the UTF-8 bytes of <paramref name="exe"/> + <paramref name="appName"/>, with the high bit set.</summary>
    /// <returns>The id as used in grid image file names (e.g. <c>{id}p.png</c>).</returns>
    public static uint Compute(string exe, string appName) =>
        Crc32.HashToUInt32(Encoding.UTF8.GetBytes(exe + appName)) | HighBit;

    /// <summary>The same id as the signed 32-bit integer stored in shortcuts.vdf.</summary>
    public static int ToVdfAppId(uint id) => unchecked((int)id);
}
