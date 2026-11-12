using System.Net;
using UWPHook.Core.Artwork.SteamGridDb;
using Xunit;

namespace UWPHook.Core.Tests.Artwork.SteamGridDb;

public class BearerKeyHandlerTests
{
    private static readonly Uri Base = new("https://www.steamgriddb.com/api/v2/");

    [Fact]
    public async Task ReadsTheKeyForEveryRequest_SoAKeyChangedInSettingsIsUsedAtOnce()
    {
        var key = "first";
        var fake = FakeHandler.Status(HttpStatusCode.OK);
        using var http = new HttpClient(new BearerKeyHandler(() => key) { InnerHandler = fake }) { BaseAddress = Base };

        await http.GetAsync("a", TestContext.Current.CancellationToken);
        key = "second";
        await http.GetAsync("b", TestContext.Current.CancellationToken);

        Assert.Equal("Bearer first", fake.Requests[0].Headers.Authorization!.ToString());
        Assert.Equal("Bearer second", fake.Requests[1].Headers.Authorization!.ToString());
    }

    [Fact]
    public async Task TrimsTheKey()
    {
        var fake = FakeHandler.Status(HttpStatusCode.OK);
        using var http = new HttpClient(new BearerKeyHandler(() => " abc\r\n") { InnerHandler = fake }) { BaseAddress = Base };

        await http.GetAsync("a", TestContext.Current.CancellationToken);

        Assert.Equal("Bearer abc", fake.Requests[0].Headers.Authorization!.ToString());
    }
}
