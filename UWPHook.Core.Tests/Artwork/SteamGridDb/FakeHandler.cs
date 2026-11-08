using System.Net;
using System.Text;

namespace UWPHook.Core.Tests.Artwork.SteamGridDb;

/// <summary>Records every request and answers with a canned response.</summary>
internal sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    public Uri LastUri => Requests[^1].RequestUri!;

    public static FakeHandler Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new((_, _) => Task.FromResult(Response(json, status)));

    public static FakeHandler Status(HttpStatusCode status) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status)));

    public static FakeHandler Throws(Exception exception) =>
        new((_, _) => Task.FromException<HttpResponseMessage>(exception));

    /// <summary>Never answers: waits until the request is cancelled or times out.</summary>
    public static FakeHandler Hangs() =>
        new(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

    public static HttpResponseMessage Response(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return respond(request, cancellationToken);
    }
}
