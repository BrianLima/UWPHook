using System.Collections.Specialized;
using System.Net.Http;
using UWPHook.Core.Artwork.SteamGridDb;
using UWPHook.Properties;

namespace UWPHook.Infrastructure
{
    /// <summary>
    /// The app's one SteamGridDB client. Its <see cref="HttpClient"/> lives as long as the app and
    /// reads the API key from Settings on every request, so a key changed in Settings applies at once.
    /// </summary>
    internal static class SteamGridDbService
    {
        private static readonly Uri BaseAddress = new Uri("https://www.steamgriddb.com/api/v2/");

        private static readonly HttpClient http = new HttpClient(
            new BearerKeyHandler(() => Settings.Default.SteamGridDbApiKey) { InnerHandler = new SocketsHttpHandler() })
        {
            BaseAddress = BaseAddress,
            Timeout = TimeSpan.FromSeconds(30),
        };

        public static SteamGridDbClient Client { get; } = new SteamGridDbClient(http);

        public const string InvalidKeyMessage =
            "Your SteamGridDB API key was rejected; artwork was skipped. Check it in Settings.";

        /// <summary>The style, type, nsfw and humor chosen in Settings.</summary>
        public static ArtworkFilter FilterFromSettings()
        {
            var settings = Settings.Default;
            return new ArtworkFilter(
                Style: Chosen(settings.SteamGridDB_Style, settings.SelectedSteamGridDB_Style),
                Type: Chosen(settings.SteamGridDB_Type, settings.SelectedSteamGridDB_Type),
                Nsfw: Chosen(settings.SteamGridDB_nfsw, settings.SelectedSteamGridDB_nfsw),
                Humor: Chosen(settings.SteamGridDB_Humor, settings.SelectedSteamGridDB_Humor));
        }

        // An index outside the list (a hand-edited user.config) means "any" instead of a crash.
        private static string Chosen(StringCollection? values, int index) =>
            values != null && index >= 0 && index < values.Count ? values[index] ?? "any" : "any";
    }
}
