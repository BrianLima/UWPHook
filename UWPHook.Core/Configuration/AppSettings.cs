using System.Text;
using System.Text.Json.Serialization;
using UWPHook.Core.Artwork.SteamGridDb;

namespace UWPHook.Core.Configuration;

/// <summary>
/// Everything the user can set in UWPHook, with 2.14's defaults. Stored by <see cref="SettingsStore"/>.
/// </summary>
public sealed record AppSettings
{
    public static AppSettings Defaults { get; } = new();

    /// <summary>Override the Windows UI language while a game runs.</summary>
    public bool ChangeLanguage { get; init; }

    /// <summary>The language for <see cref="ChangeLanguage"/>, e.g. "ja-JP"; empty means none chosen.</summary>
    public string TargetLanguage { get; init; } = "";

    /// <summary>The range Settings offers for <see cref="Seconds"/>; a launch needs at least 1.</summary>
    public const int MinSeconds = 1;

    public const int MaxSeconds = 9;

    private readonly int seconds = 5;

    /// <summary>
    /// How often, in seconds, a launch checks whether the game still runs. Clamped to
    /// <see cref="MinSeconds"/>..<see cref="MaxSeconds"/>, so a hand-edited value cannot break Settings.
    /// </summary>
    public int Seconds
    {
        get => seconds;
        init => seconds = Math.Clamp(value, MinSeconds, MaxSeconds);
    }

    /// <summary>Show a splash before the game starts, so Steam's stream catches up.</summary>
    public bool StreamMode { get; init; }

    /// <summary>Plain text in memory only: <see cref="SettingsStore"/> writes it protected.</summary>
    [JsonIgnore]
    public string SteamGridDbApiKey { get; init; } = "";

    public string ArtworkStyle { get; init; } = "any";

    public string ArtworkType { get; init; } = "any";

    public string ArtworkNsfw { get; init; } = "false";

    public string ArtworkHumor { get; init; } = "false";

    /// <summary>The first-run offer to set up SteamGridDB was shown.</summary>
    public bool OfferedSteamGridDb { get; init; }

    /// <summary>Comma separated Steam collections that exported games are added to.</summary>
    public string Tags { get; init; } = "READY TO PLAY,XBOX";

    public AppLogLevel LogLevel { get; init; } = AppLogLevel.Error;

    [JsonIgnore]
    public bool HasSteamGridDbApiKey => SteamGridDbApiKey.Length > 0;

    public ArtworkFilter ArtworkFilter() => new(ArtworkStyle, ArtworkType, ArtworkNsfw, ArtworkHumor);

    // Keeps the API key out of logs and debugger output.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"ChangeLanguage = {ChangeLanguage}, TargetLanguage = {TargetLanguage}, Seconds = {Seconds}, ")
            .Append($"StreamMode = {StreamMode}, SteamGridDbApiKey = {(HasSteamGridDbApiKey ? "(set)" : "(empty)")}, ")
            .Append($"ArtworkStyle = {ArtworkStyle}, ArtworkType = {ArtworkType}, ArtworkNsfw = {ArtworkNsfw}, ArtworkHumor = {ArtworkHumor}, ")
            .Append($"OfferedSteamGridDb = {OfferedSteamGridDb}, Tags = {Tags}, LogLevel = {LogLevel}");
        return true;
    }
}
