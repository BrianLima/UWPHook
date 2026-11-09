using System.Net.Http;
using UWPHook.Core.Artwork.SteamGridDb;
using UWPHook.Core.Configuration;

namespace UWPHook.Infrastructure
{
    /// <summary>
    /// A SteamGridDB client with the key and artwork filter from the settings. Its <see cref="HttpClient"/> lives as
    /// long as the service and reads the API key on every request, so a key changed in Settings applies at once.
    /// </summary>
    internal sealed class SteamGridDbService
    {
        private static readonly Uri BaseAddress = new Uri("https://www.steamgriddb.com/api/v2/");

        /// <summary>The app's one service, over <see cref="UserSettings"/>, until Task 10c-2's composition root owns it.</summary>
        public static SteamGridDbService Shared { get; } = new SteamGridDbService(() => UserSettings.Current);

        public const string InvalidKeyMessage =
            "Your SteamGridDB API key was rejected; artwork was skipped. Check it in Settings.";

        private readonly Func<AppSettings> _settings;

        public SteamGridDbService(Func<AppSettings> settings)
        {
            _settings = settings;
            var http = new HttpClient(new BearerKeyHandler(() => _settings().SteamGridDbApiKey) { InnerHandler = new SocketsHttpHandler() })
            {
                BaseAddress = BaseAddress,
                Timeout = TimeSpan.FromSeconds(30),
            };
            Client = new SteamGridDbClient(http);
        }

        public SteamGridDbClient Client { get; }

        public bool HasApiKey => _settings().HasSteamGridDbApiKey;

        /// <summary>The style, type, nsfw and humor chosen in Settings.</summary>
        public ArtworkFilter Filter() => _settings().ArtworkFilter();
    }
}
