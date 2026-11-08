using UWPHook.Core.Scripting;
using Xunit;

namespace UWPHook.Core.Tests.Scripting;

public class PowerShellScriptTests
{
    [Fact]
    public void WithOutString_PipesTheLastStatementIntoOutString() =>
        Assert.Equal("Get-Date | Out-String", PowerShellScript.WithOutString("Get-Date"));

    [Fact]
    public void WithOutString_DropsTheTrailingSemicolonOfTheDiscoveryScript() =>
        Assert.Equal("$aumidList | Out-String", PowerShellScript.WithOutString("$aumidList;"));

    [Fact]
    public void WithOutString_KeepsEarlierLinesUntouched() =>
        Assert.Equal(
            "$aumidList = @()\r\n$aumidList += 'a;';\r\n$aumidList | Out-String",
            PowerShellScript.WithOutString("$aumidList = @()\r\n$aumidList += 'a;';\r\n$aumidList;"));

    [Theory]
    [InlineData("Get-Date;\r\n")]
    [InlineData("Get-Date\n\n")]
    [InlineData("Get-Date ;\t\n;  ")]
    [InlineData("Get-Date;;")]
    public void WithOutString_TrimsTrailingSemicolonsAndWhitespace(string script) =>
        Assert.Equal("Get-Date | Out-String", PowerShellScript.WithOutString(script));

    [Fact]
    public void WithOutString_KeepsSemicolonsBetweenStatementsOnTheLastLine() =>
        Assert.Equal("$a = 1; $a | Out-String", PowerShellScript.WithOutString("$a = 1; $a;"));

    [Fact]
    public void Prepare_MakesCmdletErrorsStopTheScript() =>
        Assert.StartsWith("$ErrorActionPreference = 'Stop'" + Environment.NewLine, PowerShellScript.Prepare("Get-Date"));

    [Fact]
    public void Prepare_PipesTheLastStatementIntoOutString() =>
        Assert.EndsWith(Environment.NewLine + "Get-Date | Out-String", PowerShellScript.Prepare("Get-Date;"));
}
