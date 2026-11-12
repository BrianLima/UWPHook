using UWPHook.Core.Launch;
using Xunit;

namespace UWPHook.Core.Tests.Launch;

public class LaunchRequestTests
{
    private const string Exe = "C:\\x\\UWPHook.exe";
    private const string Forza = "Microsoft.Forza_8wekyb3d8bbwe!App";
    private const string Fallout = "BethesdaSoftworks.Fallout76-PC_3275kfvn8vcwc!Fallout76";

    [Fact]
    public void Parse_StandardArgs()
    {
        var request = Parse(Exe, Forza, "forza.exe", "-a", "b");

        Assert.Equal(new LaunchRequest(Forza, "forza.exe", "-a b"), request);
    }

    [Fact]
    public void Parse_RecoversAumidFromSplitUnquotedExePath()
    {
        var request = Parse("C:\\Program", "Files", "(x86)\\Briano\\UWPHook\\UWPHook.exe", Forza, "forza.exe");

        Assert.Equal(Forza, request.Aumid);
        Assert.Equal("forza.exe", request.Executable);
        Assert.Equal("", request.ExtraArguments);
    }

    [Fact]
    public void Parse_NormalizesForwardSlashesInExecutable()
    {
        var request = Parse(Exe, Forza, "MCC/Binaries/x.exe");

        Assert.Equal("MCC\\Binaries\\x.exe", request.Executable);
    }

    [Fact]
    public void Parse_MissingExecutable_UsesEmpty()
    {
        var request = Parse(Exe, Forza);

        Assert.Equal(Forza, request.Aumid);
        Assert.Equal("", request.Executable);
        Assert.Equal("", request.ExtraArguments);
    }

    // What Windows hands UWPHook for a shortcut exported without an executable (LaunchOptions = <AUMID> ""):
    // the empty quoted placeholder is one empty argument.
    [Fact]
    public void Parse_AnEmptyExecutablePlaceholder()
    {
        var request = Parse(Exe, Fallout, "");

        Assert.Equal(new LaunchRequest(Fallout, "", ""), request);
        Assert.Equal("BethesdaSoftworks.Fallout76-PC_3275kfvn8vcwc", request.PackageFamilyName);
    }

    // ... and with "-foo" appended by the user in Steam: it stays an extra argument.
    [Fact]
    public void Parse_AnEmptyExecutablePlaceholder_KeepsUserArgumentsAsExtra()
    {
        var request = Parse(Exe, Fallout, "", "-foo");

        Assert.Equal(new LaunchRequest(Fallout, "", "-foo"), request);
    }

    // Windows strips the quotes of -mod "C:\My Mods"; passed on without them the game would get two arguments.
    [Fact]
    public void Parse_KeepsAnExtraArgumentWithSpacesAsOneArgument()
    {
        var request = Parse(Exe, Forza, "forza.exe", "-mod", "C:\\My Mods", "-windowed");

        Assert.Equal("-mod \"C:\\My Mods\" -windowed", request.ExtraArguments);
    }

    [Theory]
    [InlineData(new object[] { new[] { Exe } })]
    [InlineData(new object[] { new[] { Exe, "foo" } })]
    public void Parse_NoAumidToken_ReturnsFalse(string[] args)
    {
        Assert.False(LaunchRequest.TryParse(args, out var request));
        Assert.Null(request);
    }

    [Theory]
    [InlineData("Microsoft.Forza_8wekyb3d8bbwe!App", "Microsoft.Forza_8wekyb3d8bbwe")]
    [InlineData("Microsoft.Chelan_8wekyb3d8bbwe!Game", "Microsoft.Chelan_8wekyb3d8bbwe")]
    public void PackageFamilyName_IsTheAumidBeforeTheBang(string aumid, string family) =>
        Assert.Equal(family, new LaunchRequest(aumid, "", "").PackageFamilyName);

    private static LaunchRequest Parse(params string[] args)
    {
        Assert.True(LaunchRequest.TryParse(args, out var request));
        return request!;
    }
}
