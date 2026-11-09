using UWPHook.Core.Diagnostics;

namespace UWPHook.Core.Steam;

/// <summary>
/// The Steam install and its accounts. The folder is HKCU <c>SteamPath</c> (kept current by the Steam client),
/// else the installer's HKLM <c>InstallPath</c> from whichever registry view has an existing folder (I5).
/// The accounts are the numeric <c>userdata</c> folders (M6), named from <c>config\loginusers.vdf</c> when it has them.
/// </summary>
/// <param name="folderExists">Whether a folder exists: a registry candidate, or <c>userdata</c> (injectable for tests).</param>
/// <param name="fileExists">Whether a file exists: steam.exe, or <c>loginusers.vdf</c> (injectable for tests).</param>
public sealed class SteamLibrary(
    ISteamRegistry registry,
    ILog log,
    Func<string, bool>? folderExists = null,
    Func<string, bool>? fileExists = null) : ISteamLocator
{
    private readonly Func<string, bool> _folderExists = folderExists ?? Directory.Exists;
    private readonly Func<string, bool> _fileExists = fileExists ?? File.Exists;

    public string? InstallFolder() =>
        SteamInstallChooser.Choose([registry.SteamPath(), .. registry.InstallPaths()], _folderExists);

    public string? SteamExecutable() =>
        InstallFolder() is { } folder && Path.Combine(folder, "steam.exe") is var exe && _fileExists(exe) ? exe : null;

    /// <summary>In account id order. Never throws for a missing or corrupt loginusers.vdf: the folders are listed without names.</summary>
    public IReadOnlyList<SteamUser> Users(string installFolder)
    {
        var userdata = Path.Combine(installFolder, "userdata");
        if (!_folderExists(userdata)) return [];

        var named = NamedUsers(installFolder);
        return [.. Directory.GetDirectories(userdata)
            .Select(folder => Path.GetFileName(folder))
            .Where(SteamUserData.IsAccountFolder)
            .Order(StringComparer.Ordinal)
            .Select(id => named.TryGetValue(id, out var user) ? user : SteamUser.Unnamed(id))];
    }

    private IReadOnlyDictionary<string, SteamUser> NamedUsers(string installFolder)
    {
        var path = Path.Combine(installFolder, "config", "loginusers.vdf");
        if (!_fileExists(path)) return new Dictionary<string, SteamUser>();

        try
        {
            using var stream = File.OpenRead(path);
            return LoginUsers.Parse(stream);
        }
        catch (Exception e)
        {
            // The names are only nice to have: every account folder is still exported to.
            log.Warning(e, "Could not read {Path}; the Steam accounts are listed without their names", path);
            return new Dictionary<string, SteamUser>();
        }
    }
}
