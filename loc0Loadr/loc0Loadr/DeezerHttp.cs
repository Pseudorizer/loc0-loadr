using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ByteSizeLib;
using Konsole;
using loc0Loadr.Enums;
using loc0Loadr.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

        public async Task<byte[]> DownloadTrack(string url, string title, int retries = 3)
        {
            var attempts = 1;

            while (attempts <= retries)
            {
                try
                {
                    using (HttpResponseMessage downloadResponse = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                    {
                        if (downloadResponse.IsSuccessStatusCode && downloadResponse.Content.Headers.ContentLength.HasValue)
                        {
                            return await DownloadWithProgress(downloadResponse, title);
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

        private async Task<byte[]> DownloadWithProgress(HttpResponseMessage response, string title)
        {
            // thanks stackoverflow
            using (Stream fileStream = await response.Content.ReadAsStreamAsync())
            {
                long total = response.Content.Headers.ContentLength.Value;
                double totalMegabytes = ByteSize.FromBytes(total).MegaBytes;
                totalMegabytes = Math.Round(totalMegabytes, 2);
                var finalBytes = new byte[total];
                var totalRead = 0L;
                var buffer = new byte[4096];
                var isMoreToRead = true;
                
                var progressBar = new ProgressBar(PbStyle.SingleLine, 100, 100);

                do
                {
                    int read = await fileStream.ReadAsync(buffer, 0, buffer.Length);

                    if (read == 0)
                    {
                        progressBar.Refresh(100, $"{title} | Download Complete");
                        isMoreToRead = false;
                    }
                    else
                    {
                        var data = new byte[read];
                        buffer.ToList().CopyTo(0, data, 0, read);

                        data.CopyTo(finalBytes, totalRead);
                        
                        totalRead += read;

                        double percent = totalRead * 1d / (total * 1d) * 100;

                        double totalReadMegabytes = ByteSize.FromBytes(totalRead).MegaBytes;
                        totalReadMegabytes = Math.Round(totalReadMegabytes, 2);
                        
                        progressBar.Refresh(Convert.ToInt32(percent), $"{title} | {totalReadMegabytes}MB/{totalMegabytes}MB");
                    }
                                    
                } while (isMoreToRead);

                return finalBytes;

                string Pad(string number, int max)
                {
                    int padding = max - number.Length;
                    return number + new string(' ', padding);
                }
            }
        }

        public async Task<byte[]> GetAlbumArt(string albumPictureId, int retries = 3)
        {
            string url = $"https://e-cdns-images.dzcdn.net/images/cover/{albumPictureId}/1400x1400-000000-94-0-0.jpg";

            var attempts = 1;

            while (attempts <= retries)
            {
                try
                {
                    using (HttpResponseMessage albumCoverResponse = await _httpClient.GetAsync(url))
                    {
                        if (albumCoverResponse.IsSuccessStatusCode)
                        {
                            return await albumCoverResponse.Content.ReadAsByteArrayAsync();
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
            
            return new byte[0];
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}