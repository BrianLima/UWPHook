using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using UWPHook.Properties;
using System.Diagnostics;
using Serilog;

namespace UWPHook.SteamGridDb
{
    class SteamGridDbApi
    {
        private const string BASE_URL = "https://www.steamgriddb.com/api/v2/";

        private HttpClient httpClient;
        private Settings settings;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="apiKey"> An SteamGridDB api key retrieved from https://www.steamgriddb.com/profile/preferences </param>
        public SteamGridDbApi(string apiKey)
        {
            settings = Settings.Default;
            httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(BASE_URL);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        /// <summary>
        /// Search SteamGridDB for a game 
        /// </summary>
        /// <param name="gameName" type="String">Name of the game</param>
        /// <returns>Array of games corresponding to the provided name</returns>
        public async Task<GameResponse[]> SearchGame(string gameName)
        {
            string path = $"search/autocomplete/{gameName}";

            GameResponse[] games = null;
            HttpResponseMessage response = await httpClient.GetAsync(path);
            
            if (response.IsSuccessStatusCode)
            {
                var parsedResponse = await response.Content.ReadAsAsync<ResponseWrapper<GameResponse>>();
                games = parsedResponse.Data;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                Log.Verbose("ERROR RESPONSE: " + response.ToString());

                settings.SteamGridDbApiKey = String.Empty;
                settings.Save();

                Log.Error("Warning: SteamGrid API Key Invalid. Please generate a new key and add it to settings.");
                throw new TaskCanceledException("Warning: SteamGrid API Key Invalid. Please generate a new key and add it to settings.");
            }

            return games;
        }

        /// <summary>
        /// Method responsible for transforming user selected settings
        /// into a suitable parameter list for SteamGridDB requests
        /// </summary>
        /// <param name="dimensions">Comma separated list of resolutions, see https://www.steamgriddb.com/api/v2#tag/GRIDS</param>
        /// <returns>A String with the formatted parameters</returns>
        public string BuildParameters(string dimensions)
        {
            String result = String.Empty;
            var style = settings.SteamGridDB_Style[settings.SelectedSteamGridDB_Style];
            var type = settings.SteamGridDB_Type[settings.SelectedSteamGridDB_Type];
            var nsfw = settings.SteamGridDB_nfsw[settings.SelectedSteamGridDB_nfsw];
            var humor = settings.SteamGridDB_Humor[settings.SelectedSteamGridDB_Humor];

            if (!String.IsNullOrEmpty(dimensions))
                result += $"dimensions={dimensions}&";

            if (type != "any") 
                result += $"types={type}&";

            if (style != "any")
                result += $"styles={style}&";

            if (nsfw != "any")
                result += $"nsfw={nsfw}&";

            if (humor != "any")
                result += $"humor={humor}&";

            return result;
        }

        /// <summary>
        /// Performs a request on a given url
        /// </summary>
        /// <param name="url">The url to perform the request</param>
        /// <returns>An array of ImageResponse with their urls</returns>
        public async Task<ImageResponse[]> getResponse(string url)
        {
            HttpResponseMessage response = await httpClient.GetAsync(url);
            ImageResponse[] images = null;

            if (response.IsSuccessStatusCode)
            {
                var parsedResponse = await response.Content.ReadAsAsync<ResponseWrapper<ImageResponse>>();
                if (parsedResponse != null)
                {
                    if (parsedResponse.Success)
                    {
                        images = parsedResponse.Data;
                    }
                }
            }

            return images;
        }

        public async Task<ImageResponse[]> GetGameGrids(int gameId, string dimensions = null)
        {
            string path = $"grids/game/{gameId}?{BuildParameters(dimensions)}";

            return await getResponse(path);
        }

        public async Task<ImageResponse[]> GetGameHeroes(int gameId, string dimensions = null)
        {
            string path = $"heroes/game/{gameId}?{BuildParameters(dimensions)}";

            return await getResponse(path);
        }

        public async Task<ImageResponse[]> GetGameLogos(int gameId, string dimensions = null)
        {
            string path = $"logos/game/{gameId}?{BuildParameters(dimensions)}";

            return await getResponse(path);
        }

        private class ResponseWrapper<T>
        {
            public bool Success { get; set; }
            public T[] Data { get; set; }
        }
    }
}
