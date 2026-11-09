using CommunityToolkit.Mvvm.ComponentModel;
using UWPHook.Core.Configuration;

namespace UWPHook.Presentation.Settings;

/// <summary>
/// The Settings fields as the window edits them: combo boxes work with indexes, and nothing picked (-1) keeps the
/// stored value. The language override is not here any more (R37; Task 13 removes it), so saving leaves it as it is.
/// </summary>
public sealed class SettingsForm : ObservableObject
{
    /// <summary>"1 seconds" to "9 seconds": the range <see cref="AppSettings.Seconds"/> is clamped to.</summary>
    public static IReadOnlyList<string> SecondsChoices { get; } =
        [.. Enumerable.Range(AppSettings.MinSeconds, AppSettings.MaxSeconds - AppSettings.MinSeconds + 1).Select(i => i + " seconds")];

    private int secondsIndex;
    private bool streamMode;
    private int logLevelIndex;
    private string steamGridDbApiKey = "";
    private int styleIndex;
    private int typeIndex;
    private int nsfwIndex;
    private int humorIndex;
    private string tags = "";

    public static SettingsForm From(AppSettings settings) => new()
    {
        SecondsIndex = settings.Seconds - AppSettings.MinSeconds,
        StreamMode = settings.StreamMode,
        LogLevelIndex = (int)settings.LogLevel,
        SteamGridDbApiKey = settings.SteamGridDbApiKey,
        StyleIndex = ArtworkChoices.Styles.IndexOf(settings.ArtworkStyle),
        TypeIndex = ArtworkChoices.Types.IndexOf(settings.ArtworkType),
        NsfwIndex = ArtworkChoices.Nsfw.IndexOf(settings.ArtworkNsfw),
        HumorIndex = ArtworkChoices.Humor.IndexOf(settings.ArtworkHumor),
        Tags = settings.Tags,
    };

    public int SecondsIndex { get => secondsIndex; set => SetProperty(ref secondsIndex, value); }

    public bool StreamMode { get => streamMode; set => SetProperty(ref streamMode, value); }

    /// <summary>The items are ERROR, DEBUG, TRACE, in the order of <see cref="AppLogLevel"/>.</summary>
    public int LogLevelIndex { get => logLevelIndex; set => SetProperty(ref logLevelIndex, value); }

    public string SteamGridDbApiKey { get => steamGridDbApiKey; set => SetProperty(ref steamGridDbApiKey, value ?? ""); }

    public int StyleIndex { get => styleIndex; set => SetProperty(ref styleIndex, value); }

    public int TypeIndex { get => typeIndex; set => SetProperty(ref typeIndex, value); }

    public int NsfwIndex { get => nsfwIndex; set => SetProperty(ref nsfwIndex, value); }

    public int HumorIndex { get => humorIndex; set => SetProperty(ref humorIndex, value); }

    public string Tags { get => tags; set => SetProperty(ref tags, value ?? ""); }

    /// <summary>The stored settings with this form's edits.</summary>
    public AppSettings ApplyTo(AppSettings settings) => settings with
    {
        Seconds = SecondsIndex >= 0 ? SecondsIndex + AppSettings.MinSeconds : settings.Seconds,
        StreamMode = StreamMode,
        LogLevel = Enum.IsDefined((AppLogLevel)LogLevelIndex) ? (AppLogLevel)LogLevelIndex : settings.LogLevel,
        SteamGridDbApiKey = SteamGridDbApiKey.Trim('\r', '\n'),
        ArtworkStyle = ArtworkChoices.Styles.ValueAt(StyleIndex, settings.ArtworkStyle),
        ArtworkType = ArtworkChoices.Types.ValueAt(TypeIndex, settings.ArtworkType),
        ArtworkNsfw = ArtworkChoices.Nsfw.ValueAt(NsfwIndex, settings.ArtworkNsfw),
        ArtworkHumor = ArtworkChoices.Humor.ValueAt(HumorIndex, settings.ArtworkHumor),
        Tags = Tags,
    };
}
