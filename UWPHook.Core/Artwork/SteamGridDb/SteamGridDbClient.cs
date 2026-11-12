using System.Net;
using System.Text.Json;

namespace UWPHook.Core.Artwork.SteamGridDb;

/// <summary>
/// Reads games and artwork from SteamGridDB's v2 API. The <see cref="HttpClient"/> it is given owns
/// the base address (https://www.steamgriddb.com/api/v2/) and the API key. HTTP, network and JSON
/// errors come back as <see cref="SgdbResult{T}"/> values; only the caller's own cancellation throws.
/// </summary>
public sealed class SteamGridDbClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The games whose name matches <paramref name="gameName"/>, best match first.</summary>
    public async Task<SgdbResult<IReadOnlyList<SgdbGame>>> SearchAsync(string gameName, CancellationToken ct)
    {
        var result = await GetAsync<GameDto>($"search/autocomplete/{Uri.EscapeDataString(gameName)}", ct);
        return Map(result, game => new SgdbGame(game.Id, game.Name ?? string.Empty));
    }

    public Task<SgdbResult<IReadOnlyList<SgdbImage>>> GetGridsAsync(int gameId, ArtworkFilter filter, string? dimensions, CancellationToken ct) =>
        GetImagesAsync("grids", gameId, filter, dimensions, ct);

    public Task<SgdbResult<IReadOnlyList<SgdbImage>>> GetHeroesAsync(int gameId, ArtworkFilter filter, string? dimensions, CancellationToken ct) =>
        GetImagesAsync("heroes", gameId, filter, dimensions, ct);

    public Task<SgdbResult<IReadOnlyList<SgdbImage>>> GetLogosAsync(int gameId, ArtworkFilter filter, string? dimensions, CancellationToken ct) =>
        GetImagesAsync("logos", gameId, filter, dimensions, ct);

    private async Task<SgdbResult<IReadOnlyList<SgdbImage>>> GetImagesAsync(
        string kind, int gameId, ArtworkFilter filter, string? dimensions, CancellationToken ct)
    {
        var query = filter.ToQueryString(dimensions);
        var path = query.Length == 0 ? $"{kind}/game/{gameId}" : $"{kind}/game/{gameId}?{query}";

        var result = await GetAsync<ImageDto>(path, ct);
        if (!result.IsOk) return Map(result, ToImage);

        var images = result.Value.Where(image => !string.IsNullOrEmpty(image.Url)).Select(ToImage).ToArray();
        return SgdbResult<IReadOnlyList<SgdbImage>>.Ok(images);
    }

    private static SgdbImage ToImage(ImageDto image) => new(image.Url!);

    private async Task<SgdbResult<IReadOnlyList<T>>> GetAsync<T>(string path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            using var response = await http.GetAsync(new Uri(path, UriKind.Relative), ct);
            return response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => SgdbResult<IReadOnlyList<T>>.InvalidApiKey(),
                HttpStatusCode.NotFound => SgdbResult<IReadOnlyList<T>>.NotFound(),
                _ when !response.IsSuccessStatusCode =>
                    SgdbResult<IReadOnlyList<T>>.Failed($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}".TrimEnd()),
                _ => await ReadAsync<T>(response, ct),
            };
        }
        catch (OperationCanceledException exception) when (!ct.IsCancellationRequested)
        {
            return SgdbResult<IReadOnlyList<T>>.Failed($"The request timed out: {exception.Message}");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or IOException or NotSupportedException)
        {
            return SgdbResult<IReadOnlyList<T>>.Failed(exception.Message);
        }
    }

    private static async Task<SgdbResult<IReadOnlyList<T>>> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        await using var body = await response.Content.ReadAsStreamAsync(ct);
        var envelope = await JsonSerializer.DeserializeAsync<Envelope<T>>(body, Json, ct);

        if (envelope is null) return SgdbResult<IReadOnlyList<T>>.Failed("SteamGridDB sent an empty response.");
        if (!envelope.Success)
        {
            var errors = envelope.Errors is { Length: > 0 } ? string.Join("; ", envelope.Errors) : "no reason given";
            return SgdbResult<IReadOnlyList<T>>.Failed($"SteamGridDB reported a failure: {errors}");
        }

        return SgdbResult<IReadOnlyList<T>>.Ok(envelope.Data ?? []);
    }

    private static SgdbResult<IReadOnlyList<TOut>> Map<TIn, TOut>(SgdbResult<IReadOnlyList<TIn>> result, Func<TIn, TOut> map) =>
        result.Status switch
        {
            SgdbStatus.Ok => SgdbResult<IReadOnlyList<TOut>>.Ok(result.Value.Select(map).ToArray()),
            SgdbStatus.InvalidApiKey => SgdbResult<IReadOnlyList<TOut>>.InvalidApiKey(),
            SgdbStatus.NotFound => SgdbResult<IReadOnlyList<TOut>>.NotFound(),
            _ => SgdbResult<IReadOnlyList<TOut>>.Failed(result.Error ?? "Unknown error"),
        };

    private sealed class Envelope<T>
    {
        public bool Success { get; set; }
        public T[]? Data { get; set; }
        public string[]? Errors { get; set; }
    }

    private sealed class GameDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    private sealed class ImageDto
    {
        public string? Url { get; set; }
    }
}
