namespace UWPHook.Core.Steam;

/// <summary>Finds the Steam install that export, "Remove UWPHook shortcuts" and the Steam restart all work on.</summary>
public interface ISteamLocator
{
    /// <summary>Steam's install folder, or null when Steam was not found.</summary>
    string? InstallFolder();

    /// <summary>steam.exe in <see cref="InstallFolder"/>, or null when it isn't there.</summary>
    string? SteamExecutable();

    /// <summary>The Steam accounts that have a folder in <paramref name="installFolder"/>'s <c>userdata</c>.</summary>
    IReadOnlyList<SteamUser> Users(string installFolder);
}
