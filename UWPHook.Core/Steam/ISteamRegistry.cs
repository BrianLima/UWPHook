namespace UWPHook.Core.Steam;

/// <summary>What the Windows registry says about where Steam is installed. Reading it never throws.</summary>
public interface ISteamRegistry
{
    /// <summary><c>HKCU\Software\Valve\Steam\SteamPath</c>, which the Steam client rewrites on every start; null when missing.</summary>
    string? SteamPath();

    /// <summary>
    /// The installer's <c>HKLM\SOFTWARE\Valve\Steam\InstallPath</c> in each registry view, 32-bit (WOW6432Node)
    /// first, then 64-bit; an entry is null when that view has none.
    /// </summary>
    IReadOnlyList<string?> InstallPaths();
}
