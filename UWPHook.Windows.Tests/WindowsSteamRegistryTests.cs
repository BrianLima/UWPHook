using Microsoft.Win32;
using UWPHook.Infrastructure;
using Xunit;

namespace UWPHook.Windows.Tests;

// The registry side of the Steam locator, against a throwaway HKCU key (HKLM needs admin rights to write).
public sealed class WindowsSteamRegistryTests : IDisposable
{
    private readonly string _testKey = @"Software\UWPHook.Windows.Tests\" + Guid.NewGuid().ToString("N");

    public void Dispose() => Registry.CurrentUser.DeleteSubKeyTree(_testKey, throwOnMissingSubKey: false);

    private string MissingMachineKey => @"SOFTWARE\UWPHook.Windows.Tests.Missing\" + Guid.NewGuid().ToString("N");

    [Fact]
    public void SteamPath_ReadsTheCurrentUsersSteamPath()
    {
        using (var key = Registry.CurrentUser.CreateSubKey(_testKey))
        {
            key.SetValue("SteamPath", "d:/games/steam");
        }

        Assert.Equal("d:/games/steam", new WindowsSteamRegistry(_testKey, MissingMachineKey).SteamPath());
    }

    [Fact]
    public void SteamPath_WithoutTheKey_IsNull() =>
        Assert.Null(new WindowsSteamRegistry(_testKey, MissingMachineKey).SteamPath());

    [Fact]
    public void SteamPath_ThatIsNotAString_IsNull()
    {
        using (var key = Registry.CurrentUser.CreateSubKey(_testKey))
        {
            key.SetValue("SteamPath", 42, RegistryValueKind.DWord);
        }

        Assert.Null(new WindowsSteamRegistry(_testKey, MissingMachineKey).SteamPath());
    }

    [Fact]
    public void InstallPaths_HasOneEntryPerRegistryView_NullWhereThereIsNone() =>
        Assert.Equal(new string?[] { null, null }, new WindowsSteamRegistry(_testKey, MissingMachineKey).InstallPaths());
}
