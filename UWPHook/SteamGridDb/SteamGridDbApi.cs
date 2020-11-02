using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace UWPHook.SteamGridDb
{
    class SteamGridDbApi
    {
        private const string BASE_URL = "https://www.steamgriddb.com/api/v2/";

        private HttpClient httpClient;

        public SteamGridDbApi(string apiKey)
        {
            httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(BASE_URL);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        public async Task<GameResponse[]> SearchGame(string gameName)
        {
            string path = $"search/autocomplete/{gameName}";

            GameResponse[] games = null;
            HttpResponseMessage response = await httpClient.GetAsync(path);

            if(response.IsSuccessStatusCode)
            {
                var parsedResponse = await response.Content.ReadAsAsync<ResponseWrapper<GameResponse>>();
                games = parsedResponse.Data;
            }

            return games;
        }

        public async Task<GridResponse[]> GetGameGrids(
            int gameId,  
            string dimensions = null, 
            string types = null, 
            string styles = null,
            string nsfw = "any", 
            string humor = "any") 
        {
            string path = $"grids/game/{gameId}";

            GridResponse[] grids = null;
            HttpResponseMessage response = await httpClient.GetAsync(path);

            if (response.IsSuccessStatusCode)
            {
                var parsedResponse = await response.Content.ReadAsAsync<ResponseWrapper<GridResponse>>();
                grids = parsedResponse.Data;
            }

            return grids;
        }

        public async Task<HeroResponse[]> GetGameHeroes(
            int gameId,
            string types = null,
            string dimensions = null, 
            string styles = null, 
            string nsfw = "any", 
            string humor = "any")
        {
            string path = $"heroes/game/{gameId}";

            HeroResponse[] heroes = null;
            HttpResponseMessage response = await httpClient.GetAsync(path);

            if (response.IsSuccessStatusCode)
            {
                var parsedResponse = await response.Content.ReadAsAsync<ResponseWrapper<HeroResponse>>();
                heroes = parsedResponse.Data;
            }

            return heroes;
        }

        public async Task<LogoResponse[]> GetGameLogos(
            int gameId,  
            string types = null, 
            string styles = null,
            string nsfw = "any", 
            string humor = "any")
        {
            string path = $"logos/game/{gameId}";

            LogoResponse[] logos = null;
            HttpResponseMessage response = await httpClient.GetAsync(path);

            if (response.IsSuccessStatusCode)
            {
                var parsedResponse = await response.Content.ReadAsAsync<ResponseWrapper<LogoResponse>>();
                logos = parsedResponse.Data;
            }

            return logos;
        }

        private class ResponseWrapper<T>
        {
            public bool Success { get; set; }
            public T[] Data { get; set; }
        }
    }
}
