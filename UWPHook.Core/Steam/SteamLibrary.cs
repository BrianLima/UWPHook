using UWPHook.Core.Diagnostics;

namespace UWPHook.Core.Steam;

public sealed class SteamLibrary : ISteamLocator
{
    public SteamLibrary(ISteamRegistry registry, ILog log, Func<string, bool>? folderExists = null, Func<string, bool>? fileExists = null)
    {
    }

    public string? InstallFolder() => null;

    public string? SteamExecutable() => null;

    public IReadOnlyList<SteamUser> Users(string installFolder) => [];
}
