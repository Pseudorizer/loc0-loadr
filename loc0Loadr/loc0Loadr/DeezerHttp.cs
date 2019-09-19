using System;
using System.Collections.Generic;
using System.IO;
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
        // TODO: Potentially replace all startDownload functions with a common interface and inject that into DeezerHttp, move each style of download to its own class that takes that common class
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
            _httpClient.DefaultRequestHeaders.Add("user-agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/76.0.3809.132 Safari/537.36");
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

                    IEnumerable<IGrouping<TrackType, SearchResult>> results = _deezerFunctions
                        .ParseSearchJson(searchJson, type)
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

        public async Task<bool> DownloadTrack(string id, AudioQuality audioQuality, JToken trackInfo = null, JToken albumInfo = null)
        {
            // once this is done, we'll extract these to smaller methods as this does not fit under "start download"
            //track info
            if (trackInfo == null)
            {
                trackInfo = await GetSingleTrackInfo(id);

                if (trackInfo == null)
                {
                    return false;
                }

                trackInfo = trackInfo["results"]["DATA"];
            }

            trackInfo.DisplayDeezerErrors();

            ChosenAudioQuality chosenAudioQuality = _deezerFunctions.GetAudioQuality(trackInfo, audioQuality);

            trackInfo["QUALITY"] = JToken.FromObject(chosenAudioQuality);

            // album info
            if (trackInfo["ALB_ID"].Value<int>() == 0)
            {
                // do something later on
            }

            var albumId = trackInfo["ALB_ID"].Value<string>();

            if (albumInfo == null)
            {
                albumInfo = await GetAlbumInfo(albumId);
                
                if (albumInfo == null)
                {
                    return false;
                }
            }

            albumInfo.DisplayDeezerErrors();

            JToken albumData = albumInfo["results"];

            trackInfo = _deezerFunctions.AddAlbumInfo(albumData, trackInfo);

            JObject officialAlbumInfo = await GetOfficialAlbumInfo(albumId);

            trackInfo = _deezerFunctions.AddOfficialAlbumInfo(officialAlbumInfo, trackInfo);

            var e = JsonConvert.SerializeObject(trackInfo);
            
            return await BeginDownload(trackInfo);
        }

        public async Task<bool> DownloadMultiple(string id, string type, AudioQuality audioQuality)
        {
            switch (type)
            {
                case "album":
                    JObject albumInfo = await GetAlbumInfo(id);
                    albumInfo.DisplayDeezerErrors();

                    JToken albumData = albumInfo["results"]["DATA"];
                    
                    foreach (JToken track in albumInfo["results"]["SONGS"]["data"].Children())
                    {
                        var trackId = track["SNG_ID"].Value<string>();

                        var f = await DownloadTrack(trackId, audioQuality, track, albumInfo);
                    }
                    break;
                case "playlist":
                    JObject playlistInfo = await GetPlaylistInfo(id);

                    var e = JsonConvert.SerializeObject(playlistInfo);
                    
                    break;
            }

            return true;
        }

        private async Task<bool> BeginDownload(JToken trackInfo)
        {
            string downloadPath = _deezerFunctions.BuildSaveLocation(trackInfo);

            string downloadUrl = EncryptionHandler.GetDownloadUrl(trackInfo);

            var title = trackInfo["SNG_TITLE"].Value<string>();
            var artist = trackInfo["ART_NAME"].Value<string>();
            
            Console.WriteLine($"Downloading ${artist} - {title}");

            byte[] encryptedTrack = await DownloadTrack(downloadUrl);
            
            byte[] decryptedTrack = EncryptionHandler.DecryptTrack(encryptedTrack, trackInfo["SNG_ID"].Value<string>());
            
            File.WriteAllBytes("M:\\Music\\DOWNLOADS\\sun.flac", decryptedTrack);

            return true;
        }

        private async Task<JObject> GetSingleTrackInfo(string id)
        {
            string queryString = await Helpers.BuildDeezerApiQueryString(_apiToken, "deezer.pageTrack");
            string url = $"{Helpers.ApiUrl}?{queryString}";

            string bodyData = JsonConvert.SerializeObject(new
            {
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

            string bodyData = JsonConvert.SerializeObject(new
            {
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

        private async Task<JObject> GetPlaylistInfo(string id)
        {
            string queryString = await Helpers.BuildDeezerApiQueryString(_apiToken, "deezer.pagePlaylist");
            string url = $"{Helpers.ApiUrl}?{queryString}";

            string bodyData = JsonConvert.SerializeObject(new
            {
                playlist_id = id,
                lang = "en",
                nb = -1,
                start = 0,
                tab = 0,
                tags = true,
                header = true
            });
            
            var body = new StringContent(bodyData, Encoding.UTF8, "application/json");

            using (HttpResponseMessage playlistInfoResponse = await _httpClient.PostAsync(url, body))
            {
                if (!playlistInfoResponse.IsSuccessStatusCode)
                {
                    return null;
                }

                string playlistBody = await playlistInfoResponse.Content.ReadAsStringAsync();
                
                return JObject.Parse(playlistBody);
            }
        }

        private async Task<JObject> GetOfficialAlbumInfo(string id)
        {
            using (HttpResponseMessage albumResponse = await _httpClient.GetAsync($"https://api.deezer.com/album/{id}"))
            {
                if (!albumResponse.IsSuccessStatusCode)
                {
                    return null;
                }

                string albumResponseContent = await albumResponse.Content.ReadAsStringAsync();

                return JObject.Parse(albumResponseContent);
            }
        }

        private async Task<byte[]> DownloadTrack(string url)
        {
            using (HttpResponseMessage downloadResponse = await _httpClient.GetAsync(url))
            {
                if (!downloadResponse.IsSuccessStatusCode)
                {
                    return null;
                }

                return await downloadResponse.Content.ReadAsByteArrayAsync();
            }
        }
    }
}