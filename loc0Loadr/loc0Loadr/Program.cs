using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace loc0Loadr
{
    internal static class Program
    { // https://www.deezer.com/ajax/gw-light.php
        private static async Task Main()
        {
            HttpClientHandler h = new HttpClientHandler
            {
                CookieContainer = new CookieContainer()
            };
            var x = new HttpClient(h);
            x.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; rv:68.0) Gecko/20100101 Firefox/68.0");
            x.DefaultRequestHeaders.Add("Accept-Langauge", "en-GB,en;q=0.5");
            x.DefaultRequestHeaders.Add("Cache-Control", "max-age=0");
            x.DefaultRequestHeaders.Add("Cookie", "arl=");
            
            var e = new FormUrlEncodedContent(new []
            {
                new KeyValuePair<string, string>("api_version", "1.0"), 
                new KeyValuePair<string, string>("api_token", ""), 
                new KeyValuePair<string, string>("input", "3"), 
                new KeyValuePair<string, string>("method", "deezer.getUserData"), 
                new KeyValuePair<string, string>("cid", Guid.NewGuid().ToString()), 
            });

            var w = await e.ReadAsStringAsync();

            var r = await x.PostAsync("https://www.deezer.com/ajax/gw-light.php", e);

            var g = await r.Content.ReadAsStringAsync();
            
            JObject j = JObject.Parse(g);
            var k = j["results"]["USER"]["USER_ID"].Value<string>();
            var t = JsonConvert.SerializeObject(j);
        }
    }

    internal static class Configuration
    {
        private static JObject _configFile;
        
        public static ConfigFile GetConfig()
        {
            string configFilePath = Path.Join(AppContext.BaseDirectory, "config.json");

            if (!File.Exists(configFilePath))
            {
                Helpers.RedMessage("Failed to find config.json file");
                return null;
            }

            string configFileContent = File.ReadAllText(configFilePath);

            ConfigFile configFile;

            try
            {
                configFile = JsonConvert.DeserializeObject<ConfigFile>(configFileContent);
                _configFile = JObject.Parse(configFilePath);
            }
            catch (JsonReaderException ex)
            {
                Helpers.RedMessage(ex.Message);
                return null;
            }

            return configFile;
        }

        public static bool UpdateConfig(string keyToUpdate, string newValue) // change type to config file
        {
            try
            {
                _configFile[keyToUpdate] = newValue;
            }
            catch (KeyNotFoundException ex)
            {
                Helpers.RedMessage(ex.Message);
                return false;
            }

            string configFilePath = Path.Join(AppContext.BaseDirectory, "config.json");

            string configFileSerialized = JsonConvert.SerializeObject(_configFile);
            
            File.WriteAllText(configFilePath, configFileSerialized);
            return true;
        }
    }

    internal class ConfigFile
    {
        [JsonProperty("arl")]
        public string Arl { get; set; }

        [JsonProperty("downloadLocation")]
        public string DownloadLocation { get; set; }
    }

    internal static class Helpers
    {
        public static void RedMessage(string message)
        {
            ConsoleColor original = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine($"\n{message}");
            Console.ForegroundColor = original;
        }
    }
}