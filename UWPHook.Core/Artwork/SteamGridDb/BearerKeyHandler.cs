using System.Net.Http.Headers;

namespace UWPHook.Core.Artwork.SteamGridDb;

/// <summary>
/// Adds "Authorization: Bearer &lt;key&gt;" to every request, reading the key each time,
/// so one long-lived <see cref="HttpClient"/> follows key changes made in Settings.
/// </summary>
public sealed class BearerKeyHandler(Func<string?> apiKey) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey()?.Trim() ?? string.Empty);
        return base.SendAsync(request, cancellationToken);
    }
}
