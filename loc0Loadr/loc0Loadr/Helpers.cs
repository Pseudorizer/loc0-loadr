using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FlacLibSharp;
using loc0Loadr.Enums;
using Newtonsoft.Json.Linq;
using TagLib;
using TagLib.Id3v2;
using File = System.IO.File;

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

        public static readonly Dictionary<AudioQuality, string> AudioQualityToOutputString = new Dictionary<AudioQuality, string>
        {
            {AudioQuality.Mp3128, "MP3 @ 128"},
            {AudioQuality.Mp3256, "MP3 @ 256"},
            {AudioQuality.Mp3320, "MP3 @ 320"},
            {AudioQuality.Flac, "FLAC"},
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
            Console.WriteLine($"\n{start} - {messages[0]}");
                
            for (var i = 1; i < messages.Length; i++)
            {
                string message = messages[i];

                Console.WriteLine($"{i + 1} - {message}");
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

        public static void DisplayDeezerErrors(this JToken json, string operation)
        {
            if (json["error"] != null && json["error"].HasValues)
            {
                foreach (JProperty child in json["error"].Children().Select(x => (JProperty) x))
                {
                    RedMessage($"[{operation}] {child.Name} - {child.Value.Value<string>()}");
                }
            }
        }

        public static string SanitseString(this string word)
        {
            word = word.Trim();
            
            var invalidChars = Path.GetInvalidFileNameChars();
            word = string.Join("_", word.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

            word = Regex.Replace(word, @"\s(\s+)", "$1");

            return word;
        }

        public static string PadNumber(this string word)
        {
            return int.Parse(word) < 10 
                ? $"0{word}" 
                : word;
        }

        public static TextInformationFrame BuildTextInformationFrame(string frameType, params string[] text)
        {
            return new TextInformationFrame(new ByteVector(frameType), StringType.UTF8)
            {
                Text = text
            };
        }
        
        public static UserTextInformationFrame BuildUserTextInformationFrame(string description, params string[] text)
        {
            return new UserTextInformationFrame("TXXX", StringType.UTF8)
            {
                Text = text,
                Description = description
            };
        }
        
        public static UserTextInformationFrame BuildUserTextInformationFrame(string frameType, string description, params string[] text)
        {
            return new UserTextInformationFrame(frameType, StringType.UTF8)
            {
                Text = text,
                Description = description
            };
        }

        public static void AddTagIfNotNull(VorbisComment comments, string key, params string[] values)
        {
            if (values == null)
            {
                return;
            }

            comments[key] = new VorbisCommentValues(values.Where(x => x != null));
        }

        public static bool WriteTrackBytes(byte[] fileBytes, string savePath)
        {
            string directoryPath = Path.GetDirectoryName(savePath);

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
                File.WriteAllBytes(savePath, fileBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }

            return true;
        }
    }
}