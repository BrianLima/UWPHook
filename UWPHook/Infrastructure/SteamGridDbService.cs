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

        public const string InvalidKeyMessage =
            "Your SteamGridDB API key was rejected; artwork was skipped. Check it in Settings.";

        private readonly Func<AppSettings> _settings;

        /// <param name="inner">Sends the requests; the network by default (tests pass a fake).</param>
        public SteamGridDbService(Func<AppSettings> settings, HttpMessageHandler? inner = null)
        {
            _settings = settings;
            var http = new HttpClient(new BearerKeyHandler(() => _settings().SteamGridDbApiKey) { InnerHandler = inner ?? new SocketsHttpHandler() })
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
