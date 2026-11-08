namespace UWPHook.Core.Artwork.SteamGridDb;

/// <summary>A game found by a SteamGridDB search.</summary>
public sealed record SgdbGame(int Id, string Name);

/// <summary>A grid, hero or logo image on SteamGridDB.</summary>
public sealed record SgdbImage(string Url);
