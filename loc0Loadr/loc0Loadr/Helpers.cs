using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using loc0Loadr.Enums;
using Newtonsoft.Json.Linq;

namespace loc0Loadr
{
    internal static class Helpers
    {
        public const string ApiUrl = "https://www.deezer.com/ajax/gw-light.php";
        
        public static readonly Dictionary<string, AudioQuality> InputToAudioQuality = new Dictionary<string, AudioQuality>
        {
            {"1", AudioQuality.Mp3128},
            {"2", AudioQuality.Mp3256},
            {"3", AudioQuality.Mp3320},
            {"4", AudioQuality.Flac},
        };
        
        public static readonly Dictionary<string, AudioQuality> KeyToAudioQuality = new Dictionary<string, AudioQuality>
        {
            {"FILESIZE_MP3_128", AudioQuality.Mp3128},
            {"FILESIZE_MP3_256", AudioQuality.Mp3256},
            {"FILESIZE_MP3_320", AudioQuality.Mp3320},
            {"FILESIZE_FLAC", AudioQuality.Flac},
        };
        
        public static void RedMessage(string message)
        {
            ConsoleColor original = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine($"\n{message}");
            Console.ForegroundColor = original;
        }
        
        public static void GreenMessage(string message)
        {
            ConsoleColor original = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n{message}");
            Console.ForegroundColor = original;
        }

        public static string TakeInput(string message)
        {
            Console.Write($"\n{message}");
            string input = Console.ReadLine()?.Trim();

            while (string.IsNullOrWhiteSpace(input))
            {
                RedMessage("Invalid Input");
                Console.Write($"\n{message}");
                input = Console.ReadLine()?.Trim();
            }

            return input;
        }

        public static string TakeInput(int start, int count, params string[] messages)
        {
            Console.WriteLine($"\n{messages[0]}");
            foreach (string message in messages.Skip(1))
            {
                Console.WriteLine(message);
            }
            Console.Write("\nEnter choice: ");
            string input = Console.ReadLine()?.Trim();

            while (!int.TryParse(input, out int number)
            || !Enumerable.Range(start, count).Contains(number))
            {
                RedMessage("Invalid Input");
                Console.Write("\nEnter choice: ");
                input = Console.ReadLine()?.Trim();
            }

            return input;
        }

        public static string GetCid()
        {
            string cid = string.Empty;

            for (var i = 0; i < 9; i++)
            {
                cid += new Random().Next(1, 9);
            }

            return cid;
        }

        public static FormUrlEncodedContent BuildDeezerApiContent(string apiToken, string method)
        {
            return new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("api_version", "1.0"),
                new KeyValuePair<string, string>("api_token", apiToken),
                new KeyValuePair<string, string>("input", "3"),
                new KeyValuePair<string, string>("method", method),
                new KeyValuePair<string, string>("cid", GetCid())
            });
        }

        public static async Task<string> BuildDeezerApiQueryString(string apiToken, string method)
        {
            using (FormUrlEncodedContent content = BuildDeezerApiContent(apiToken, method))
            {
                return await content.ReadAsStringAsync();
            }
        }

        public static void DisplayDeezerErrors(this JObject json)
        {
            if (json["error"] != null && json["error"].HasValues)
            {
                foreach (JProperty child in json["error"].Children().Select(x => (JProperty) x))
                {
                    RedMessage($"{child.Name} - {child.Value.Value<string>()}");
                }
            }
        }

        public static AudioQuality GetNextAudioLevelDown(AudioQuality audioQuality)
        {
            var intId = (int) audioQuality;

            if (intId <= 0)
            {
                return default;
            }

            return (AudioQuality) intId - 1;
        }

        public static T TryAddTokenValue<T>(JToken tokenToSearch, string jPath)
        {
            JToken selectedToken = tokenToSearch.SelectToken(jPath);

            return selectedToken == null
                ? default
                : selectedToken.Value<T>();
        }
    }
}