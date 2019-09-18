using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using loc0Loadr.Enums;
using loc0Loadr.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;
using StringBuilder = System.Text.StringBuilder;

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

        public async Task<bool> StartSingleDownload(string id, AudioQuality audioQuality)
        {
            // once this is done, we'll extract these to smaller methods as this does not fit under "start download"
            //track info
            JObject trackInfo = await GetSingleTrackInfo(id);

            if (trackInfo == null)
            {
                return false;
            }

            trackInfo.DisplayDeezerErrors();

            JToken trackData = trackInfo["results"]["DATA"];

            ChosenAudioQuality chosenAudioQuality = _deezerFunctions.GetAudioQuality(trackData, audioQuality);

            trackData["QUALITY"] = JToken.FromObject(chosenAudioQuality);

            // album info
            if (trackData["ALB_ID"].Value<int>() == 0)
            {
                // do something later on
            }

            var albumId = trackData["ALB_ID"].Value<string>();

            JObject albumInfo = await GetAlbumInfo(albumId);

            if (albumInfo == null)
            {
                return false;
            }

            albumInfo.DisplayDeezerErrors();

            JToken albumData = albumInfo["results"];

            trackData = _deezerFunctions.AddAlbumInfo(albumData, trackData);

            JObject officialAlbumInfo = await GetOfficialAlbumInfo(albumId);

            trackData = _deezerFunctions.AddOfficialAlbumInfo(officialAlbumInfo, trackData);

            string downloadPath = _deezerFunctions.BuildSaveLocation(trackData);

            string downloadUrl = EncryptionHandler.GetDownloadUrl(trackData);

            byte[] j = await DownloadTrack(downloadUrl);
            
            EncryptionHandler.DecryptTrack(j, trackData);

            var e = JsonConvert.SerializeObject(trackData);

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

    internal static class EncryptionHandler
    {
        public static string GetDownloadUrl(JToken trackInfo)
        {
            char cdn = trackInfo["MD5_ORIGIN"].Value<string>()[0];
            string encryptedFilename = GetEncryptedFilename(trackInfo);

            return $"https://e-cdns-proxy-{cdn}.dzcdn.net/mobile/1/{encryptedFilename}";
        }

        public static void DecryptTrack(byte[] downloadBytes, JToken trackInfo)
        {
            string blowfishKey = GetBlowfishKey(trackInfo);
            byte[] keyBytes = Encoding.UTF8.GetBytes(blowfishKey);
            long streamLength = downloadBytes.Length;

            byte[] decryptedBuffer = new byte[streamLength];
            var chunkSize = 2048;
            var progress = 0;

            while (progress < streamLength)
            {
                if (streamLength - progress < 2048)
                {
                    chunkSize = (int) streamLength - progress;
                }

                byte[] encryptedChunk = new byte[chunkSize];
                Buffer.BlockCopy(downloadBytes, progress, encryptedChunk, 0, chunkSize);

                if (progress % (chunkSize * 3) == 0 && chunkSize == 2048)
                {
                    var blowfishEngine = new BlowfishEngine();
                    var cipher = new PaddedBufferedBlockCipher(new CbcBlockCipher(blowfishEngine), new ZeroBytePadding());
                    var keyParameter = new KeyParameter(keyBytes);
                    //var parameters = new ParametersWithIV(keyParameter, new byte [] {0, 1, 2, 3, 4, 5, 6, 7});
                    cipher.Init(false, keyParameter);

                    byte[] output = new byte[cipher.GetOutputSize(encryptedChunk.Length)];
                    int len = cipher.ProcessBytes(encryptedChunk, 0, encryptedChunk.Length, output, 0);
                    cipher.DoFinal(output);
                    Buffer.BlockCopy(output, 0, encryptedChunk, progress, output.Length);
                }
                Buffer.BlockCopy(encryptedChunk, 0, decryptedBuffer, progress, encryptedChunk.Length);

                progress += chunkSize;
            }
        }

        private static string GetEncryptedFilename(JToken trackInfo)
        {
            var md5Origin = trackInfo["MD5_ORIGIN"].Value<string>();
            var qualityId = trackInfo["QUALITY"]["AudioEnumId"].Value<string>();
            var sngId = trackInfo["SNG_ID"].Value<string>();
            var mediaVersion = trackInfo["MEDIA_VERSION"].Value<string>();

            string itemsJoined = string.Join("¤", md5Origin, qualityId, sngId, mediaVersion);
            string newHash = string.Empty;

            using (MD5 md5 = MD5.Create())
            {
                byte[] itemsJoinedBytes = Encoding.ASCII.GetBytes(itemsJoined);
                itemsJoinedBytes = FixStarCharBytes(itemsJoinedBytes);

                byte[] itemsJoinedHashed = md5.ComputeHash(itemsJoinedBytes);

                var hexBuilder = new StringBuilder(itemsJoinedHashed.Length * 2);

                foreach (byte b in itemsJoinedHashed)
                {
                    hexBuilder.Append(b.ToString("x2"));
                }

                hexBuilder.Append("¤")
                    .Append(itemsJoined)
                    .Append("¤");

                newHash = hexBuilder.ToString();
            }

            while (newHash.Length % 16 != 0)
            {
                newHash += " ";
            }

            return AesEncryptHash(newHash);
        }

        private static string AesEncryptHash(string hash)
        {
            byte[] keyBytes = Encoding.ASCII.GetBytes("jo6aey6haid2Teih");
            var iV = new byte[] {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0};

            var aes = new AesManaged
            {
                KeySize = 128,
                BlockSize = 128,
                Mode = CipherMode.ECB,
                Key = keyBytes,
                IV = iV
            };

            ICryptoTransform aesEncryptor = aes.CreateEncryptor(keyBytes, iV);

            var newHashBytes = Encoding.ASCII.GetBytes(hash);
            newHashBytes = FixStarCharBytes(newHashBytes);

            var encryptedHash = aesEncryptor.TransformFinalBlock(newHashBytes, 0, newHashBytes.Length);

            var encryptedHashBuilder = new StringBuilder();

            foreach (byte b in encryptedHash)
            {
                encryptedHashBuilder.Append(b.ToString("x2"));
            }

            string finalHash = encryptedHashBuilder.ToString();

            finalHash = finalHash.Substring(0, finalHash.Length - 32); // not sure why these extra 32 chars appear, maybe the IV?

            return finalHash;
        }

        private static byte[] FixStarCharBytes(byte[] bytes) // replacing EVERY 63 may cause issues but is fine for now
        {
            for (var index = 0; index < bytes.Length; index++)
            {
                byte itemsJoinedByte = bytes[index];

                if (itemsJoinedByte == 63)
                {
                    bytes[index] = 164;
                }

            }

            return bytes;
        }

        private static string GetBlowfishKey(JToken trackInfo)
        {
            const string secret = "g4el58wc0zvf9na1";
            string idHashedHex;
            
            using (MD5 md5 = MD5.Create())
            {
                var id = trackInfo["SNG_ID"].Value<string>();

                byte[] idBytes = Encoding.ASCII.GetBytes(id);
                byte[] idHashed = md5.ComputeHash(idBytes);
                
                var hexBuilder = new StringBuilder();
                
                foreach (byte b in idHashed)
                {
                    hexBuilder.Append(b.ToString("x2"));
                }

                idHashedHex = hexBuilder.ToString();
            }

            string blowfishKey = string.Empty;

            for (var i = 0; i < 16; i++)
            { // some things don't get nice names :)
                int b = idHashedHex[i];
                int n = idHashedHex[i + 16];
                int m = secret[i];
                char a = Convert.ToChar(b ^ n ^ m);
                string s = a.ToString();
                blowfishKey += s;
            }

            return blowfishKey;
        }
    }
}