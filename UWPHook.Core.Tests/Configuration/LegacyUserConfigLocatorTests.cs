using UWPHook.Core.Configuration;
using Xunit;

namespace UWPHook.Core.Tests.Configuration;

// 2.14's user.config: %LocalAppData%\Briano\UWPHook.exe_Url_<hash>\<version>\user.config.
// Each exe path (and so each install folder) has its own hash folder.
public sealed class LegacyUserConfigLocatorTests : IDisposable
{
    private readonly string briano = Path.Combine(Path.GetTempPath(), "uwphook-briano-" + Guid.NewGuid().ToString("N"));

    public LegacyUserConfigLocatorTests() => Directory.CreateDirectory(briano);

    public void Dispose()
    {
        if (Directory.Exists(briano)) Directory.Delete(briano, recursive: true);
    }

    private string UserConfig(string appFolder, string version, DateTime? writtenAt = null)
    {
        var folder = Path.Combine(briano, appFolder, version);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "user.config");
        File.WriteAllText(path, "<configuration />");
        if (writtenAt is not null) File.SetLastWriteTimeUtc(path, writtenAt.Value);
        return path;
    }

    [Fact]
    public void Find_WithoutTheBrianoFolder_IsNull() =>
        Assert.Null(LegacyUserConfigLocator.Find(Path.Combine(briano, "missing")));

    [Fact]
    public void Find_WithoutAnyUserConfig_IsNull()
    {
        Directory.CreateDirectory(Path.Combine(briano, "UWPHook.exe_Url_abc", "2.14.1.0"));

        Assert.Null(LegacyUserConfigLocator.Find(briano));
    }

    [Fact]
    public void Find_TheOnlyUserConfig()
    {
        var path = UserConfig("UWPHook.exe_Url_abc", "2.14.1.0");

        Assert.Equal(path, LegacyUserConfigLocator.Find(briano));
    }

    [Fact]
    public void Find_TheHighestVersion_AcrossInstallFolders_ComparedAsNumbers()
    {
        UserConfig("UWPHook.exe_Url_aaa", "2.9.0.0", writtenAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var newest = UserConfig("UWPHook.exe_Url_bbb", "2.14.1.0", writtenAt: new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        UserConfig("UWPHook.exe_Url_bbb", "2.10.0.0", writtenAt: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(newest, LegacyUserConfigLocator.Find(briano));
    }

    [Fact]
    public void Find_TheSameVersionInTwoInstallFolders_TheNewestWrite()
    {
        UserConfig("UWPHook.exe_Url_aaa", "2.14.1.0", writtenAt: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var newest = UserConfig("UWPHook.exe_Url_bbb", "2.14.1.0", writtenAt: new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(newest, LegacyUserConfigLocator.Find(briano));
    }

    [Fact]
    public void Find_IgnoresFoldersThatAreNotUwpHooksOrNotVersions()
    {
        var path = UserConfig("UWPHook.exe_Url_abc", "2.12.0.0");
        UserConfig("OtherApp.exe_Url_abc", "9.0.0.0");
        UserConfig("UWPHook.exe_Url_abc", "backup");

        Assert.Equal(path, LegacyUserConfigLocator.Find(briano));
    }
}
