using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using loc0Loadr.Enums;
using loc0Loadr.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace loc0Loadr
{
    internal class DeezerHttp
    {
        private readonly HttpClient _httpClient;
        private readonly DeezerFunctions _deezerFunctions;
        private string _apiToken;

        public DeezerHttp(string arl)
        {
            _deezerFunctions = new DeezerFunctions();
            
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
            Console.WriteLine("Grabbing API token...");
            using (FormUrlEncodedContent formContent = Helpers.BuildDeezerApiContent("", "deezer.getUserData"))
            {
                using (HttpResponseMessage apiRequest = await _httpClient.PostAsync(Helpers.ApiUrl, formContent))
                {
                    if (!apiRequest.IsSuccessStatusCode)
                    {
                        Helpers.RedMessage("Failed to contact initial API");
                        return false;
                    }

                    string apiRequestBody = await apiRequest.Content.ReadAsStringAsync();

                    JObject apiRequestJson = JObject.Parse(apiRequestBody);

                    apiRequestJson.DisplayDeezerErrors();

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
            string queryString = await Helpers.BuildDeezerApiQueryString(_apiToken, "deezer.pageSearch");
            string url = $"{Helpers.ApiUrl}?{queryString}";

            string bodyData = JsonConvert.SerializeObject(new
            {
                query = searchItem,
                start = 0,
                nb = 40,
                suggest = false,
                artist_suggest = false,
                top_tracks = false
            });

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
                    
                    searchJson.DisplayDeezerErrors();

                    IEnumerable<IGrouping<TrackType, SearchResult>> results = _deezerFunctions.ParseSearchJson(searchJson, type)
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

            return "";
        }

        public async Task<bool> StartSingleDownload(string id, AudioQuality audioQuality)
        { // once this is done, we'll extract these to smaller methods as this does not fit under "start download"
            //track info
            JObject trackInfo = await GetSingleTrackInfo(id);

            if (trackInfo == null)
            {
                return false;
            }
            
            trackInfo.DisplayDeezerErrors();

            JToken trackData = trackInfo["results"]["DATA"];

            var downloadQuality = _deezerFunctions.GetAudioQuality(trackData, audioQuality);
            
            // album info
            if (trackData["ALB_ID"].Value<int>() == 0)
            {
                // do something later on
            }

            JObject albumInfo = await GetAlbumInfo(trackData["ALB_ID"].Value<string>());
            
            if (albumInfo == null)
            {
                return false;
            }
            
            albumInfo.DisplayDeezerErrors();

            JToken albumData = albumInfo["results"];

            if (albumData?["DATA"]?["UPC"] != null)
            {
                trackData["UPC"] = albumData["DATA"]["UPC"].Value<string>();
            }

            if (trackData["PHYSICAL_RELEASE_DATE"] == null && albumData?["DATA"]?["PHYSICAL_RELEASE_DATE"] != null)
            {
                trackData["PHYSICAL_RELEASE_DATE"] = albumData["DATA"]["PHYSICAL_RELEASE_DATE"].Value<string>();
            }

            if (albumData?["SONGS"]?["data"].Children().Last()["DISK_NUMBER"] != null)
            {
                trackData["NUMBER_OF_DISKS"] =
                    albumData["SONGS"]["data"].Children().Last()["DISK_NUMBER"].Value<string>();
            }

            if (trackData["ART_NAME"] == null && albumData?["DATA"]?["ART_NAME"] != null)
            {
                trackData["ART_NAME"] = albumData["DATA"]["ART_NAME"].Value<string>();
            }

            var e = JsonConvert.SerializeObject(trackData);
            
            return true;
        }

        private async Task<JObject> GetSingleTrackInfo(string id)
        {
            string queryString = await Helpers.BuildDeezerApiQueryString(_apiToken, "deezer.pageTrack");
            string url = $"{Helpers.ApiUrl}?{queryString}";

            string bodyData = JsonConvert.SerializeObject(new {
                SNG_ID = id
            });
            
            var body = new StringContent(bodyData, Encoding.UTF8, "application/json");

            using (HttpResponseMessage trackInfoResponse = await _httpClient.PostAsync(url, body))
            {
                if (!trackInfoResponse.IsSuccessStatusCode)
                {
                    return null;
                }
                
                string trackBody = await trackInfoResponse.Content.ReadAsStringAsync();
                
                return JObject.Parse(trackBody);
            }
        }

        private async Task<JObject> GetAlbumInfo(string id)
        {
            string queryString = await Helpers.BuildDeezerApiQueryString(_apiToken, "deezer.pageAlbum");
            string url = $"{Helpers.ApiUrl}?{queryString}";
            
            string bodyData = JsonConvert.SerializeObject(new {
                ALB_ID = id,
                lang = "us",
                tab = 0
            });
            
            var body = new StringContent(bodyData, Encoding.UTF8, "application/json");

            using (HttpResponseMessage albumInfoResponse = await _httpClient.PostAsync(url, body))
            {
                if (!albumInfoResponse.IsSuccessStatusCode)
                {
                    return null;
                }
                
                string albumBody = await albumInfoResponse.Content.ReadAsStringAsync();
                
                return JObject.Parse(albumBody);
            }
        }
    }
}