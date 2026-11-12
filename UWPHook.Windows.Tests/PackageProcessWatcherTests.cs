using System.Diagnostics;
using UWPHook.Core.Launch;
using UWPHook.Infrastructure;
using Xunit;

namespace UWPHook.Windows.Tests;

public class PackageProcessWatcherTests
{
    // Windows Settings: a packaged app present on Windows 10/11 and Windows Server with Desktop Experience.
    private const string SettingsFamily = "windows.immersivecontrolpanel_cw5n1h2txyewy";
    private const string SettingsAumid = SettingsFamily + "!microsoft.windows.immersivecontrolpanel";

    private static PackageProcessWatcher Watcher() => new(new WindowsPackageLocations(WindowsOsVersion.Capabilities));

    [Fact]
    public void AnyAlive_UnknownFamily_IsFalse()
    {
        // Enumerating every process also opens System, csrss, etc., which deny access: that must not throw.
        Assert.False(Watcher().AnyAlive("Bogus.Package_0000000000000"));
    }

    [Fact]
    public void AnyAlive_RepeatedWhileAccessDeniedProcessesExist_DoesNotThrow()
    {
        var watcher = Watcher();

        for (var i = 0; i < 3; i++)
        {
            watcher.AnyAlive(SettingsFamily);
            watcher.AnyAlive("Bogus.Package_0000000000000");
        }
    }

    [Fact]
    public async Task AnyAlive_AfterActivatingAPackagedApp_IsTrueForItsFamily()
    {
        int pid;
        try
        {
            pid = new ComActivator().Activate(new LaunchRequest(SettingsAumid, "", ""));
        }
        catch (InvalidOperationException e)
        {
            Assert.Skip($"Windows Settings could not be activated on this machine (no interactive desktop?): {e.Message}");
            return;
        }

        try
        {
            var watcher = Watcher();
            var alive = false;
            for (var i = 0; i < 30 && !alive; i++)
            {
                alive = watcher.AnyAlive(SettingsFamily);
                if (!alive) await Task.Delay(500, TestContext.Current.CancellationToken);
            }

            Assert.True(alive, $"no process of {SettingsFamily} seen after activation (pid {pid})");
        }
        finally
        {
            Kill(pid);
        }
    }

    private static void Kill(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            process.Kill();
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Already gone.
        }
    }
}
