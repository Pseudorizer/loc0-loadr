using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using loc0Loadr.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace loc0Loadr
{
    internal class DeezerActions
    {
        private readonly HttpClient _httpClient;
        private string _apiToken;
        private const string ApiUrl = "https://www.deezer.com/ajax/gw-light.php";

        public DeezerActions(string arl)
        {
            var handler = new HttpClientHandler
            {
                CookieContainer = new CookieContainer()
            };

            _httpClient = new HttpClient(handler);
            _httpClient.DefaultRequestHeaders.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/76.0.3809.132 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("cookie", $"arl={arl}");
        }

        public async Task<bool> GetApiToken()
        {
            using (FormUrlEncodedContent formContent = Helpers.BuildDeezerApiContent("", "deezer.getUserData"))
            {
                using (HttpResponseMessage apiRequest = await _httpClient.PostAsync(ApiUrl, formContent))
                {
                    if (!apiRequest.IsSuccessStatusCode)
                    {
                        Helpers.RedMessage("Failed to contact initial API");
                        return false;
                    }

                    string apiRequestBody = await apiRequest.Content.ReadAsStringAsync();

                    JObject apiRequestJson = JObject.Parse(apiRequestBody);

                    if (apiRequestJson["error"].HasValues)
                    {
                        foreach (JToken error in apiRequestJson["error"].Children()) // untested
                        {
                            Helpers.RedMessage($"Error: {error.Value<string>()}");
                        }
                    }

                    if (apiRequestJson["results"]?["USER"]?["USER_ID"].Value<int>() == 0)
                    {
                        Helpers.RedMessage("Invalid credentials");
                        return false;
                    }

                    if (apiRequestJson["results"]?["checkForm"] != null)
                    {
                        _apiToken = apiRequestJson["results"]["checkForm"].Value<string>();
                        return true;
                    }

                    Helpers.RedMessage("Unable to get checkform");
                    return false;
                }
            }
        }

        public async Task<string> Search(string searchItem, TrackType type)
        {
            searchItem = "numbing agent";
            using (FormUrlEncodedContent formData = Helpers.BuildDeezerApiContent(_apiToken, "deezer.pageSearch"))
            {
                string url = $"{ApiUrl}?{await formData.ReadAsStringAsync()}";

                var bodyDataJObject = new JObject
                {
                    ["query"] = searchItem,
                    ["start"] = 0,
                    ["nb"] = 40,
                    ["suggest"] = false,
                    ["artist_suggest"] = false,
                    ["top_tracks"] = false
                };

                string bodyData = JsonConvert.SerializeObject(bodyDataJObject);

                using (var bodyContent = new StringContent(bodyData, Encoding.UTF8, "application/json"))
                {
                    using (HttpResponseMessage searchResponse = await _httpClient.PostAsync(url, bodyContent))
                    {
                        if (!searchResponse.IsSuccessStatusCode)
                        {
                            Helpers.RedMessage("Failed to search API");
                            return string.Empty;
                        }

                        string searchContent = await searchResponse.Content.ReadAsStringAsync();

                        JObject searchJson = JObject.Parse(searchContent);

                        IEnumerable<IGrouping<TrackType, SearchResult>> results = ParseSearchJson(searchJson, type)
                            .GroupBy(x => x.Type);

                        foreach (IGrouping<TrackType, SearchResult> searchResults in results)
                        {
                            Console.WriteLine($"---{searchResults.FirstOrDefault().Type}---");
                            foreach (SearchResult searchResult in searchResults)
                            {
                                string artists = searchResult.Artists == null
                                    ? ""
                                    : string.Join(", ", searchResult.Artists);

                                Console.WriteLine($"{searchResult.Type} - {artists} - {searchResult.Title}");
                            }
                        }
                    }
                }
            }

            return "";
        }

        private IEnumerable<SearchResult> ParseSearchJson(JObject json, TrackType type)
        {
            var searchResults = new List<SearchResult>();

            JToken albums = json["results"]?["ALBUM"];
            JToken artists = json["results"]?["ARTIST"];
            JToken tracks = json["results"]?["TRACK"];

            switch (type)
            {
                case TrackType.Track:
                    if (tracks != null && tracks["count"].Value<int>() > 0)
                    {
                        var data = (JArray)tracks["data"];

                        return data
                                .Take(10)
                                .Select(item => new SearchResult
                                {
                                    Type = TrackType.Track,
                                    Json = item,
                                    Title = item["SNG_TITLE"].Value<string>(),
                                    Id = item["SNG_ID"].Value<int>(),
                                    Artists = item["ARTISTS"]
                                        .Select(r => r["ART_NAME"].Value<string>())
                                });
                    }
                    break;
                case TrackType.Album:
                    if (albums != null && albums["count"].Value<int>() > 0)
                    {
                        var data = (JArray)albums["data"];

                        return data
                                .Take(10)
                                .Select(item => new SearchResult
                                {
                                    Type = TrackType.Album,
                                    Json = item,
                                    Title = item["ALB_TITLE"].Value<string>(),
                                    Id = item["ALB_ID"].Value<int>(),
                                    Artists = item["ARTISTS"]
                                        .Select(artist => artist["ART_NAME"].Value<string>())
                                });
                    }
                    break;
                case TrackType.Artist:
                    if (artists != null && artists["count"].Value<int>() > 0)
                    {
                        var data = (JArray)artists["data"];

                        return data
                                .Take(10)
                                .Select(item => new SearchResult
                                {
                                    Type = TrackType.Artist,
                                    Json = item,
                                    Title = item["ART_NAME"].Value<string>(),
                                    Id = item["ART_ID"].Value<int>()
                                });
                    }
                    break;
            }

            return null;
        }

        public async Task<bool> StartSingleDownload(string id)
        {

            return true;
        }
    }
}