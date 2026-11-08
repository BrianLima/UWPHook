using System.Net;
using UWPHook.Core.Artwork.SteamGridDb;
using Xunit;

namespace UWPHook.Core.Tests.Artwork.SteamGridDb;

public class SteamGridDbClientTests
{
    private const string Api = "https://www.steamgriddb.com/api/v2/";
    private static readonly ArtworkFilter Defaults = new("any", "any", "false", "false");

    private const string TwoGames =
        """{"success":true,"data":[{"id":5248,"name":"Halo: The Master Chief Collection","types":["steam"],"verified":true},{"id":1,"name":"Halo"}]}""";

    private const string TwoImages =
        """{"success":true,"page":0,"total":2,"limit":50,"data":[{"id":1,"score":0,"style":"alternate","url":"https://cdn2.steamgriddb.com/grid/a.png","thumb":"t"},{"id":2,"url":"https://cdn2.steamgriddb.com/grid/b.webp"}]}""";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SteamGridDbClient ClientFor(FakeHandler handler, TimeSpan? timeout = null) =>
        new(new HttpClient(handler) { BaseAddress = new Uri(Api), Timeout = timeout ?? TimeSpan.FromSeconds(30) });

    // ---- Search ----

    [Fact]
    public async Task Search_EscapesTheName_SoSlashQuestionHashAmpersandPercentSpaceAndNonAsciiWork()
    {
        var handler = FakeHandler.Json(TwoGames);

        await ClientFor(handler).SearchAsync("AC/DC? #1 & 100% Café", Ct);

        Assert.Equal(Api + "search/autocomplete/AC%2FDC%3F%20%231%20%26%20100%25%20Caf%C3%A9", handler.LastUri.AbsoluteUri);
    }

    [Fact]
    public async Task Search_EscapesAColon()
    {
        var handler = FakeHandler.Json(TwoGames);

        await ClientFor(handler).SearchAsync("Halo: MCC", Ct);

        Assert.Equal(Api + "search/autocomplete/Halo%3A%20MCC", handler.LastUri.AbsoluteUri);
    }

    [Fact]
    public async Task Search_ReturnsTheGamesInTheApiOrder()
    {
        var result = await ClientFor(FakeHandler.Json(TwoGames)).SearchAsync("Halo", Ct);

        Assert.Equal(SgdbStatus.Ok, result.Status);
        Assert.Equal(new[] { new SgdbGame(5248, "Halo: The Master Chief Collection"), new SgdbGame(1, "Halo") }, result.Value);
    }

    [Fact]
    public async Task Search_WithEmptyData_IsAnEmptyList()
    {
        var result = await ClientFor(FakeHandler.Json("""{"success":true,"data":[]}""")).SearchAsync("nothing", Ct);

        Assert.Equal(SgdbStatus.Ok, result.Status);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task Search_Unauthorized_IsInvalidApiKey()
    {
        var result = await ClientFor(FakeHandler.Json("""{"success":false,"errors":["Unauthorized"]}""", HttpStatusCode.Unauthorized))
            .SearchAsync("Halo", Ct);

        Assert.Equal(SgdbStatus.InvalidApiKey, result.Status);
    }

    [Fact]
    public async Task Search_NotFound_IsNotFound()
    {
        var result = await ClientFor(FakeHandler.Status(HttpStatusCode.NotFound)).SearchAsync("Halo", Ct);

        Assert.Equal(SgdbStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Search_ServerError_IsFailed_WithTheStatus()
    {
        var result = await ClientFor(FakeHandler.Status(HttpStatusCode.InternalServerError)).SearchAsync("Halo", Ct);

        Assert.Equal(SgdbStatus.Failed, result.Status);
        Assert.Contains("500", result.Error);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"success":true,"data":{"id":1}}""")]
    [InlineData("")]
    [InlineData("null")]
    public async Task Search_MalformedJson_IsFailed(string body)
    {
        var result = await ClientFor(FakeHandler.Json(body)).SearchAsync("Halo", Ct);

        Assert.Equal(SgdbStatus.Failed, result.Status);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }

    [Fact]
    public async Task Search_SuccessFalse_IsFailed_WithTheApiErrors()
    {
        var result = await ClientFor(FakeHandler.Json("""{"success":false,"errors":["Game not available"]}""")).SearchAsync("Halo", Ct);

        Assert.Equal(SgdbStatus.Failed, result.Status);
        Assert.Contains("Game not available", result.Error);
    }

    [Fact]
    public async Task Search_NetworkError_IsFailed_WithTheMessage()
    {
        var result = await ClientFor(FakeHandler.Throws(new HttpRequestException("No such host is known")))
            .SearchAsync("Halo", Ct);

        Assert.Equal(SgdbStatus.Failed, result.Status);
        Assert.Contains("No such host is known", result.Error);
    }

    [Fact]
    public async Task Search_Timeout_IsFailed()
    {
        var result = await ClientFor(FakeHandler.Hangs(), TimeSpan.FromMilliseconds(50)).SearchAsync("Halo", Ct);

        Assert.Equal(SgdbStatus.Failed, result.Status);
    }

    [Fact]
    public async Task Search_CancelledByTheCaller_Throws()
    {
        using var cts = new CancellationTokenSource();
        var search = ClientFor(FakeHandler.Hangs()).SearchAsync("Halo", cts.Token);

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => search);
    }

    [Fact]
    public async Task Search_AlreadyCancelled_ThrowsWithoutARequest()
    {
        var handler = FakeHandler.Json(TwoGames);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ClientFor(handler).SearchAsync("Halo", new CancellationToken(canceled: true)));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Search_DoesNotSetItsOwnAuthorization_TheCallerOwnsTheKey()
    {
        var handler = FakeHandler.Json(TwoGames);

        await ClientFor(handler).SearchAsync("Halo", Ct);

        Assert.Null(handler.Requests[0].Headers.Authorization);
    }

    // ---- Images ----

    [Fact]
    public async Task Grids_RequestTheGameWithTheFilterAndDimensions()
    {
        var handler = FakeHandler.Json(TwoImages);

        await ClientFor(handler).GetGridsAsync(5248, Defaults, "600x900,342x482,660x930", Ct);

        Assert.Equal(Api + "grids/game/5248?dimensions=600x900,342x482,660x930&nsfw=false&humor=false", handler.LastUri.AbsoluteUri);
    }

    [Fact]
    public async Task Heroes_RequestTheGameWithTheFilter()
    {
        var handler = FakeHandler.Json(TwoImages);

        await ClientFor(handler).GetHeroesAsync(5248, Defaults, null, Ct);

        Assert.Equal(Api + "heroes/game/5248?nsfw=false&humor=false", handler.LastUri.AbsoluteUri);
    }

    [Fact]
    public async Task Logos_WithAnAnyFilter_HaveNoQueryString()
    {
        var handler = FakeHandler.Json(TwoImages);

        await ClientFor(handler).GetLogosAsync(5248, ArtworkFilter.Any, null, Ct);

        Assert.Equal(Api + "logos/game/5248", handler.LastUri.AbsoluteUri);
    }

    [Fact]
    public async Task Images_ReturnTheUrlsInTheApiOrder()
    {
        var result = await ClientFor(FakeHandler.Json(TwoImages)).GetGridsAsync(1, Defaults, null, Ct);

        Assert.Equal(SgdbStatus.Ok, result.Status);
        Assert.Equal(new[] { "https://cdn2.steamgriddb.com/grid/a.png", "https://cdn2.steamgriddb.com/grid/b.webp" },
            result.Value.Select(image => image.Url));
    }

    [Fact]
    public async Task Images_SkipEntriesWithoutAUrl()
    {
        var result = await ClientFor(FakeHandler.Json("""{"success":true,"data":[{"id":1},{"id":2,"url":""},{"id":3,"url":"https://x/c.png"}]}"""))
            .GetHeroesAsync(1, Defaults, null, Ct);

        Assert.Equal(new[] { "https://x/c.png" }, result.Value.Select(image => image.Url));
    }

    [Fact]
    public async Task Images_WithEmptyData_AreAnEmptyList()
    {
        var result = await ClientFor(FakeHandler.Json("""{"success":true,"data":[]}""")).GetLogosAsync(1, Defaults, null, Ct);

        Assert.Equal(SgdbStatus.Ok, result.Status);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task Images_Unauthorized_IsInvalidApiKey()
    {
        var result = await ClientFor(FakeHandler.Status(HttpStatusCode.Unauthorized)).GetGridsAsync(1, Defaults, null, Ct);

        Assert.Equal(SgdbStatus.InvalidApiKey, result.Status);
    }

    [Fact]
    public async Task Images_NotFound_IsNotFound()
    {
        var result = await ClientFor(FakeHandler.Json("""{"success":false,"errors":["Game not found"]}""", HttpStatusCode.NotFound))
            .GetHeroesAsync(1, Defaults, null, Ct);

        Assert.Equal(SgdbStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Images_MalformedJson_IsFailed()
    {
        var result = await ClientFor(FakeHandler.Json("{\"success\":tru")).GetLogosAsync(1, Defaults, null, Ct);

        Assert.Equal(SgdbStatus.Failed, result.Status);
    }

    [Fact]
    public async Task Images_SuccessFalse_IsFailed()
    {
        var result = await ClientFor(FakeHandler.Json("""{"success":false,"data":[{"url":"https://x/a.png"}]}""")).GetGridsAsync(1, Defaults, null, Ct);

        Assert.Equal(SgdbStatus.Failed, result.Status);
    }

    [Fact]
    public async Task Images_CancelledByTheCaller_Throw()
    {
        using var cts = new CancellationTokenSource();
        var grids = ClientFor(FakeHandler.Hangs()).GetGridsAsync(1, Defaults, null, cts.Token);

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => grids);
    }
}

public class SgdbResultTests
{
    [Fact]
    public void Ok_HoldsTheValue()
    {
        var result = SgdbResult<int>.Ok(3);

        Assert.True(result.IsOk);
        Assert.Equal(3, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Value_OfANonOkResult_Throws() =>
        Assert.Throws<InvalidOperationException>(() => SgdbResult<int>.NotFound().Value);

    [Fact]
    public void Failed_HoldsTheError()
    {
        var result = SgdbResult<int>.Failed("HTTP 500");

        Assert.Equal(SgdbStatus.Failed, result.Status);
        Assert.Equal("HTTP 500", result.Error);
    }

    [Fact]
    public void InvalidApiKey_IsNotOk() =>
        Assert.False(SgdbResult<int>.InvalidApiKey().IsOk);
}
