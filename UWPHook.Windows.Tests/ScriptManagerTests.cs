using System.IO;
using Xunit;

namespace UWPHook.Windows.Tests;

// Runs real scripts through the real powershell.exe on this machine.
public class ScriptManagerTests
{
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Fact]
    public void RunScript_ReturnsTheScriptOutput() =>
        Assert.Contains("hello", ScriptManager.RunScript("Write-Output 'hello'", Timeout));

    [Fact]
    public void RunScript_ReturnsTheLastValueWhenTheScriptEndsWithASemicolon() =>
        Assert.Equal("a|b;c|d;", ScriptManager.RunScript("$list = @()\r\n$list += 'a|b;'\r\n$list += 'c|d;'\r\n$list -join '';", Timeout).Trim());

    [Fact]
    public void RunScript_RunsUnderWindowsPowerShell51() =>
        Assert.Equal("5", ScriptManager.RunScript("$PSVersionTable.PSVersion.Major", Timeout).Trim());

    [Fact]
    public void RunScript_ReadsTheOutputAsUtf8() =>
        Assert.Equal("Café 太鼓", ScriptManager.RunScript("Write-Output 'Café 太鼓'", Timeout).Trim());

    [Fact]
    public void RunScript_ThrowsScriptFailedExceptionWithTheExitCodeAndStderr()
    {
        var failure = Assert.Throws<ScriptFailedException>(
            () => ScriptManager.RunScript("[Console]::Error.WriteLine('boom')\r\nexit 3", Timeout));

        Assert.Equal(3, failure.ExitCode);
        Assert.Contains("boom", failure.Stderr);
    }

    [Fact]
    public void RunScript_ThrowsTimeoutExceptionAndRemovesTheTempScript()
    {
        var before = TempScripts();
        var started = DateTime.UtcNow;

        Assert.Throws<TimeoutException>(() => ScriptManager.RunScript("Start-Sleep -Seconds 60", TimeSpan.FromSeconds(3)));

        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(30), "RunScript should give up at the timeout, not wait for the script");
        Assert.Empty(TempScripts().Except(before));
    }

    [Fact]
    public void RunScript_RemovesTheTempScriptAfterSuccess()
    {
        var before = TempScripts();

        ScriptManager.RunScript("Write-Output 'done'", Timeout);

        Assert.Empty(TempScripts().Except(before));
    }

    static string[] TempScripts() => Directory.GetFiles(Path.GetTempPath(), "UWPHook_*.ps1");
}
