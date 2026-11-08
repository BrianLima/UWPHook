using System.Net.Http;
using UWPHook.Core.Artwork.SteamGridDb;

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
            new BearerKeyHandler(() => UserSettings.Current.SteamGridDbApiKey) { InnerHandler = new SocketsHttpHandler() })
        {
            BaseAddress = BaseAddress,
            Timeout = TimeSpan.FromSeconds(30),
        };

        public static SteamGridDbClient Client { get; } = new SteamGridDbClient(http);

        public const string InvalidKeyMessage =
            "Your SteamGridDB API key was rejected; artwork was skipped. Check it in Settings.";

        /// <summary>The style, type, nsfw and humor chosen in Settings.</summary>
        public static ArtworkFilter FilterFromSettings() => UserSettings.Current.ArtworkFilter();
    }
}
