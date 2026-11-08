using System.Globalization;
using System.Xml.Linq;
using UWPHook.Core.Catalog;

namespace UWPHook.Core.Configuration;

/// <summary>
/// Reads 2.14's settings from its .NET <c>user.config</c>
/// (<c>configuration/userSettings/UWPHook.Properties.Settings/setting[@name]/value</c>).
/// A missing or invalid setting keeps its default.
/// </summary>
public static class LegacyUserConfig
{
    /// <summary>The settings in <paramref name="xml"/>, or null when it is not a user.config.</summary>
    public static AppSettings? TryParse(string xml)
    {
        var root = SafeXml.TryParseRoot(xml, "configuration");
        if (root is null) return null;

        var values = new LegacyValues(root.Children("userSettings").Children("UWPHook.Properties.Settings").Children("setting"));
        var defaults = AppSettings.Defaults;
        return new AppSettings
        {
            ChangeLanguage = values.Bool("ChangeLanguage") ?? defaults.ChangeLanguage,
            TargetLanguage = values.Text("TargetLanguage") ?? defaults.TargetLanguage,
            Seconds = values.Int("Seconds") ?? defaults.Seconds,
            StreamMode = values.Bool("StreamMode") ?? defaults.StreamMode,
            SteamGridDbApiKey = values.Text("SteamGridDbApiKey") ?? defaults.SteamGridDbApiKey,
            ArtworkStyle = values.Chosen("SteamGridDB_Style", "SelectedSteamGridDB_Style", ArtworkChoices.Styles) ?? defaults.ArtworkStyle,
            ArtworkType = values.Chosen("SteamGridDB_Type", "SelectedSteamGridDB_Type", ArtworkChoices.Types) ?? defaults.ArtworkType,
            ArtworkNsfw = values.Chosen("SteamGridDB_nfsw", "SelectedSteamGridDB_nfsw", ArtworkChoices.Nsfw) ?? defaults.ArtworkNsfw,
            ArtworkHumor = values.Chosen("SteamGridDB_Humor", "SelectedSteamGridDB_Humor", ArtworkChoices.Humor) ?? defaults.ArtworkHumor,
            OfferedSteamGridDb = values.Bool("OfferedSteamGridDB") ?? defaults.OfferedSteamGridDb,
            Tags = values.Text("Tags") ?? defaults.Tags,
            LogLevel = LogLevel(values.Int("SelectedLogLevel")),
        };
    }

    // 2.14 stored the index of ERROR, DEBUG, TRACE as text; anything else logged at ERROR.
    private static AppLogLevel LogLevel(int? index) => index switch
    {
        1 => AppLogLevel.Debug,
        2 => AppLogLevel.Trace,
        _ => AppLogLevel.Error,
    };

    /// <summary>The <c>&lt;value&gt;</c> of each setting, by name.</summary>
    private sealed class LegacyValues(IEnumerable<XElement> settings)
    {
        private readonly Dictionary<string, XElement> byName = settings
            .Where(setting => setting.AttributeValue("name") is not null)
            .GroupBy(setting => setting.AttributeValue("name")!)
            .ToDictionary(group => group.Key, group => group.Last());

        public string? Text(string name) =>
            byName.TryGetValue(name, out var setting) ? setting.Children("value").FirstOrDefault()?.Value : null;

        public bool? Bool(string name) => bool.TryParse(Text(name)?.Trim(), out var value) ? value : null;

        public int? Int(string name) =>
            int.TryParse(Text(name)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

        /// <summary>
        /// 2.14 stored an index into a list of API values (a StringCollection). The list in the file wins;
        /// without one, the list 2.14 shipped. An index outside the list is "any", as 2.14 treated it.
        /// </summary>
        public string? Chosen(string listName, string indexName, ChoiceList shipped)
        {
            var index = Int(indexName);
            if (index is null) return null;

            var stored = List(listName);
            var choices = stored is { Length: > 0 } ? new ChoiceList(stored) : shipped;
            return choices.ValueAtOrAny(index.Value).Trim();
        }

        private string[]? List(string name) =>
            byName.TryGetValue(name, out var setting)
                ? setting.Children("value").Children("ArrayOfString").Children("string").Select(item => item.Value).ToArray()
                : null;
    }
}
