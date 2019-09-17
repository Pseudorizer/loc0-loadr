using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace loc0Loadr
{
    internal class DeezerActions
    {
        private readonly HttpClient _httpClient;
        private string _apiToken;

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
            using (var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("api_version", "1.0"),
                new KeyValuePair<string, string>("api_token", ""),
                new KeyValuePair<string, string>("input", "3"),
                new KeyValuePair<string, string>("method", "deezer.getUserData"),
                new KeyValuePair<string, string>("cid", Guid.NewGuid().ToString())
            }))
            {
                using (HttpResponseMessage apiRequest = await _httpClient.PostAsync("https://www.deezer.com/ajax/gw-light.php", formContent))
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
    }
}