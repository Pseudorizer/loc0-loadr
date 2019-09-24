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
using File = System.IO.File;

namespace loc0Loadr
{
    internal class DeezerHttp : IDisposable
    {
        // As per usual with your first attempt, this class sucks, do you have any idea what goes on in downloadtrack???
        // Why is a class with http in the title handling the tag collection???
        // Why is a class with http in the title handling the iteration of songs in album???
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

                    apiRequestJson.DisplayDeezerErrors("API Token");

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

                    searchJson.DisplayDeezerErrors("Search");

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

            Console.WriteLine("\nFetching info");
            
            if (trackInfo == null)
            {
                trackInfo = await HitUnofficialApi("deezer.pageTrack", new JObject
                {
                    ["SNG_ID"] = id
                });

                if (trackInfo == null)
                {
                    return false;
                }

                if (trackInfo["results"]["LYRICS"] != null)
                {
                    trackInfo["results"]["DATA"]["LYRICS"] = trackInfo["results"]["LYRICS"];
                }

                trackInfo = trackInfo["results"]["DATA"];
            }

            trackInfo.DisplayDeezerErrors("Track");

            ChosenAudioQuality chosenAudioQuality = _deezerFunctions.GetAudioQuality(trackInfo, audioQuality);

            if (chosenAudioQuality == null)
            {
                Helpers.RedMessage("Was not able to find a valid quality");
                return false;
            }

            trackInfo["QUALITY"] = JToken.FromObject(chosenAudioQuality);

            if (trackInfo["LYRICS"] == null)
            {
                JObject lyrics = await HitUnofficialApi("song.getLyrics", new JObject
                {
                    ["sng_id"] = id
                });
                
                lyrics.DisplayDeezerErrors("Lyrics");

                if (lyrics["error"].Type == JTokenType.Array)
                {
                    trackInfo["LYRICS"] = lyrics["results"];
                }
                else if (lyrics["error"]["DATA_ERROR"] == null)
                {
                    trackInfo["LYRICS"] = lyrics["results"];
                }
            }

            JObject officialTrackInfo = await HitOfficialApi("track", id);

            if (officialTrackInfo != null)
            {
                trackInfo = _deezerFunctions.AddOfficialTrackInfo(officialTrackInfo, trackInfo);
            }

            // album info
            if (trackInfo["ALB_ID"] == null || trackInfo["ALB_ID"].Value<int>() == 0 && albumInfo == null)
            {
                return await BeginDownload(trackInfo);
            }

            var albumId = trackInfo["ALB_ID"].Value<string>();

            if (albumInfo == null)
            {
                albumInfo = await HitUnofficialApi("deezer.pageAlbum", new JObject
                {
                    ["ALB_ID"] = albumId,
                    ["lang"] = "us",
                    ["tab"] = 0
                });
                
                if (albumInfo == null)
                {
                    return false;
                }
            }

            albumInfo.DisplayDeezerErrors("Album");

            JToken albumData = albumInfo["results"];

            if (albumData == null)
            {
                return await BeginDownload(trackInfo);
            }

            trackInfo = _deezerFunctions.AddAlbumInfo(albumData, trackInfo);

            JObject officialAlbumInfo = await HitOfficialApi("album", albumId);

            if (officialAlbumInfo != null)
            {
                trackInfo = _deezerFunctions.AddOfficialAlbumInfo(officialAlbumInfo, trackInfo);
            }

            return await BeginDownload(trackInfo);
        }

        public async Task<bool> DownloadMultiple(string id, string type, AudioQuality audioQuality)
        {
            switch (type)
            {
                case "album":
                    JObject albumInfo = await HitUnofficialApi("deezer.pageAlbum", new JObject
                    {
                        ["ALB_ID"] = id,
                        ["lang"] = "us",
                        ["tab"] = 0
                    });
                    
                    albumInfo.DisplayDeezerErrors("Album");

                    var tracks = albumInfo["results"]["SONGS"]["data"].Children().ToArray();

                    for (var i = 0; i < tracks.Length; i++)
                    {
                        Console.WriteLine($"{i + 1}/{tracks.Length}");
                        JToken track = tracks[i];
                        
                        var trackId = track["SNG_ID"].Value<string>();

                        var f = await DownloadTrack(trackId, audioQuality, track, albumInfo);
                    }
                    break;
                case "playlist":
                    JObject playlistInfo = await HitUnofficialApi("deezer.pagePlaylist", new JObject
                    {
                        ["playlist_id"] = id,
                        ["lang"] = "en",
                        ["nb"] = -1,
                        ["start"] = 0,
                        ["tab"] = 0,
                        ["tags"] = true,
                        ["header"] = true
                    });

                    break;
                case "artist":
                    JObject artistInfo = await HitUnofficialApi("artist.getData", new JObject
                    {
                        ["art_id"] = id,
                        ["filter_role_id"] = 0,
                        ["lang"] = "us",
                        ["tab"] = 0,
                        ["nb"] = -1,
                        ["start"] = 0
                    });
                    
                    JObject discographyInfo = await HitUnofficialApi("album.getDiscography", new JObject
                    {
                        ["art_id"] = id,
                        ["filter_role_id"] = 0,
                        ["lang"] = "us",
                        ["nb"] = 500,
                        ["nb_songs"] = -1,
                        ["start"] = 0
                    });
                    
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
            var quality = trackInfo["QUALITY"]["QualityForOutput"].Value<string>();
            
            Console.WriteLine($"Downloading {artist} - {title} | Quality: {quality}");

            byte[] encryptedTrack = await DownloadTrack(downloadUrl);
            
            byte[] decryptedTrack = EncryptionHandler.DecryptTrack(encryptedTrack, trackInfo["SNG_ID"].Value<string>());

            string directoryPath = Path.GetDirectoryName(downloadPath);

            if (!Directory.Exists(directoryPath))
            {
                try
                {
                    Directory.CreateDirectory(directoryPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return false;
                }
            }

            try
            {
                File.WriteAllBytes(downloadPath, decryptedTrack);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
            
            string savePath = Path.Combine(directoryPath, "cover.jpg");

            byte[] albumArt;

            if (!File.Exists(savePath))
            {
                albumArt = await DownloadAlbumArt(trackInfo["ALB_PICTURE"].Value<string>(), savePath);
            }
            else
            {
                albumArt = File.ReadAllBytes(savePath);
            }

            string trackInfoSerialized = JsonConvert.SerializeObject(trackInfo);

            var metadata = JsonConvert.DeserializeObject<Metadata>(trackInfoSerialized);

            var metadataWriter = new MetadataWriter(metadata, downloadPath, albumArt);

            bool y = metadataWriter.WriteMetaData();

            return true;
        }

        public async Task<JObject> HitUnofficialApi(string method, JObject data, int retries = 3)
        {
            string queryString = await Helpers.BuildDeezerApiQueryString(_apiToken, method);
            string url = $"{Helpers.ApiUrl}?{queryString}";

            string bodyData = JsonConvert.SerializeObject(data);

            var body = new StringContent(bodyData, Encoding.UTF8, "application/json");

            var attempts = 1;

            while (attempts <= retries)
            {
                try
                {
                    using (HttpResponseMessage apiResponse = await _httpClient.PostAsync(url, body))
                    {
                        if (apiResponse.IsSuccessStatusCode)
                        {
                            string bodyContent = await apiResponse.Content.ReadAsStringAsync();

                            if (!string.IsNullOrWhiteSpace(bodyContent))
                            {
                                try
                                {
                                    return JObject.Parse(bodyContent);
                                }
                                catch (JsonReaderException ex)
                                {
                                    Console.WriteLine(ex.Message);
                                }
                            }
                        }
                    }
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine(ex.Message);
                }
                
                attempts++;
                Helpers.RedMessage("Request failed, waiting 5s...");
                await Task.Delay(5000);
            }

            return null;
        }

        public async Task<JObject> HitOfficialApi(string path, string id, int retires = 3)
        {
            var attempts = 1;

            while (attempts <= retires)
            {
                try
                {
                    using (HttpResponseMessage albumResponse =
                        await _httpClient.GetAsync($"https://api.deezer.com/{path}/{id}"))
                    {
                        if (albumResponse.IsSuccessStatusCode)
                        {
                            string albumResponseContent = await albumResponse.Content.ReadAsStringAsync();

                            if (!string.IsNullOrWhiteSpace(albumResponseContent))
                            {
                                try
                                {
                                    return JObject.Parse(albumResponseContent);
                                }
                                catch (JsonReaderException ex)
                                {
                                    Console.WriteLine(ex.Message);
                                }
                            }
                        }
                    }
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine(ex.Message);
                }

                attempts++;
                Helpers.RedMessage("Request failed, waiting 5s...");
                await Task.Delay(5000);
            }

            return null;
        }

        public async Task<byte[]> DownloadTrack(string url, int retries = 3)
        {
            var attempts = 1;

            while (attempts <= retries)
            {
                try
                {
                    using (HttpResponseMessage downloadResponse = await _httpClient.GetAsync(url))
                    {
                        if (downloadResponse.IsSuccessStatusCode)
                        {
                            return await downloadResponse.Content.ReadAsByteArrayAsync();
                        }
                    }
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine(ex.Message);
                }

                attempts++;
                Helpers.RedMessage("Request failed, waiting 5s...");
                await Task.Delay(5000);
            }

            return null;
        }

        private async Task<byte[]> DownloadAlbumArt(string albumPictureId, string savePath)
        {
            string url = $"https://e-cdns-images.dzcdn.net/images/cover/{albumPictureId}/1400x1400-000000-94-0-0.jpg";

            using (HttpResponseMessage albumCoverResponse = await _httpClient.GetAsync(url))
            {
                if (!albumCoverResponse.IsSuccessStatusCode)
                {
                    return new byte[0];
                }

                using (Stream coverStream = await albumCoverResponse.Content.ReadAsStreamAsync())
                {
                    using (FileStream fileStream = File.Create(savePath))
                    {
                        coverStream.Seek(0, SeekOrigin.Begin);
                        coverStream.CopyTo(fileStream);
                    }
                }

                return await albumCoverResponse.Content.ReadAsByteArrayAsync();
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}