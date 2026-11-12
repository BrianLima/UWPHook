using System.IO;
using UWPHook.Core.Steam;
using UWPHook.Infrastructure;
using Xunit;

namespace UWPHook.Windows.Tests;

// The parts of the real Steam process that need no Steam on the machine.
public class WindowsSteamProcessTests
{
    // The shortcuts are written by then: a Steam that will not start must not throw, and the caller is told.
    [Fact]
    public void Start_APathThatDoesNotExist_DoesNotThrow_AndSaysSteamDidNotStart()
    {
        var missing = Path.Combine(Path.GetTempPath(), "UWPHook.Windows.Tests", Guid.NewGuid().ToString("N"), "steam.exe");

        Assert.False(new WindowsSteamProcess(new Locator(null)).Start(missing));
    }

    // Steam is restarted from the install the export wrote to, not from whatever the registry names.
    [Fact]
    public void ExecutablePath_PrefersTheLocatorsSteamExe()
    {
        const string steamExe = @"D:\Games\Steam\steam.exe";

        Assert.Equal(steamExe, new WindowsSteamProcess(new Locator(steamExe)).ExecutablePath());
    }

    private sealed class Locator(string? steamExe) : ISteamLocator
    {
        public string? InstallFolder() => null;

        public string? SteamExecutable() => steamExe;

        public IReadOnlyList<SteamUser> Users(string folder) => [];
    }
}
