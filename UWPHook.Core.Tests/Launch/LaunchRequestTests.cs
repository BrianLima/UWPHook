using UWPHook.Core.Launch;
using Xunit;

namespace UWPHook.Core.Tests.Launch;

public class LaunchRequestTests
{
    private const string Exe = "C:\\x\\UWPHook.exe";
    private const string Forza = "Microsoft.Forza_8wekyb3d8bbwe!App";

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

    [Theory]
    [InlineData(new object[] { new[] { Exe } })]
    [InlineData(new object[] { new[] { Exe, "foo" } })]
    public void Parse_NoAumidToken_ReturnsFalse(string[] args)
    {
        Assert.False(LaunchRequest.TryParse(args, out var request));
        Assert.Null(request);
    }

    private static LaunchRequest Parse(params string[] args)
    {
        Assert.True(LaunchRequest.TryParse(args, out var request));
        return request!;
    }
}
