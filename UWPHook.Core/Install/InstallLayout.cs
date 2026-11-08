namespace UWPHook.Core.Install;

/// <summary>
/// Which UWPHook.exe path Steam shortcuts should point at. A Velopack install (Setup.exe, or Velopack's
/// Portable.zip) looks like <c>&lt;root&gt;\current\UWPHook.exe</c> plus a launcher stub at <c>&lt;root&gt;\UWPHook.exe</c>.
/// Shortcuts deliberately target the exe inside <c>current\</c>, NOT the stub: the stub starts
/// <c>current\UWPHook.exe</c> and exits right away, so Steam would think the game stopped and lose
/// playtime and streaming tracking, which is UWPHook's whole purpose. Velopack replaces the contents of
/// <c>current\</c> on update but keeps the folder's path, so <c>current\UWPHook.exe</c> stays valid.
/// </summary>
public static class InstallLayout
{
    /// <summary>
    /// The UWPHook.exe path to store in Steam shortcuts. Today this is always <paramref name="processPath"/>
    /// itself: the Velopack <c>current\</c> exe (see the class remarks for why not the root stub), or a plain
    /// portable folder. It stays the single seam for this decision.
    /// </summary>
    public static string StableExePath(string processPath) => processPath;
}
