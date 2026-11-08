using System.IO;
using UWPHook.Core.Configuration;
using UWPHook.Infrastructure;
using Xunit;

namespace UWPHook.Windows.Tests;

// The one-time import of 2.14's user.config, from a %LocalAppData%\Briano folder.
public sealed class LegacySettingsImportTests : IDisposable
{
    private readonly string briano = Path.Combine(Path.GetTempPath(), "uwphook-import-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(briano)) Directory.Delete(briano, recursive: true);
    }

    [Fact]
    public void From_ABrianoFolderWithA214UserConfig_GivesItsSettings()
    {
        var folder = Path.Combine(briano, "UWPHook.exe_Url_0123456789abcdef", "2.14.1.0");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "user.config"), """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
                <userSettings>
                    <UWPHook.Properties.Settings>
                        <setting name="StreamMode" serializeAs="String"><value>True</value></setting>
                        <setting name="SteamGridDbApiKey" serializeAs="String"><value>key-from-214</value></setting>
                    </UWPHook.Properties.Settings>
                </userSettings>
            </configuration>
            """);

        Assert.Equal(AppSettings.Defaults with { StreamMode = true, SteamGridDbApiKey = "key-from-214" },
            LegacySettingsImport.From(briano));
    }

    [Fact]
    public void From_NoBrianoFolder_IsNull() =>
        Assert.Null(LegacySettingsImport.From(briano));
}
