namespace UWPHook.Core.Artwork.SteamGridDb;

/// <summary>
/// The artwork the user asked for in Settings, with the values SteamGridDB's API takes
/// (see https://www.steamgriddb.com/api/v2). "any" leaves that parameter out.
/// </summary>
public sealed record ArtworkFilter(string Style, string Type, string Nsfw, string Humor)
{
    private const string AnyValue = "any";

    public static ArtworkFilter Any { get; } = new(AnyValue, AnyValue, AnyValue, AnyValue);

    /// <summary>
    /// The query string (without "?") for an image request: dimensions, types, styles, nsfw, humor,
    /// in that order. Values are trimmed; the stored "no_logo " style has a trailing space.
    /// </summary>
    /// <param name="dimensions">Comma separated resolutions such as "460x215,920x430", or null for any.</param>
    public string ToQueryString(string? dimensions)
    {
        (string Name, string? Value)[] parameters =
        [
            ("dimensions", string.IsNullOrWhiteSpace(dimensions) ? null : dimensions.Trim()),
            ("types", Chosen(Type)),
            ("styles", Chosen(Style)),
            ("nsfw", Chosen(Nsfw)),
            ("humor", Chosen(Humor)),
        ];

        return string.Join("&", parameters
            .Where(parameter => parameter.Value is not null)
            .Select(parameter => $"{parameter.Name}={parameter.Value}"));
    }

    private static string? Chosen(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length == 0 || trimmed.Equals(AnyValue, StringComparison.OrdinalIgnoreCase) ? null : trimmed;
    }
}
