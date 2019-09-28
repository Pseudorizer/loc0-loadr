using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using loc0Loadr.Enums;
using Newtonsoft.Json.Linq;

namespace loc0Loadr.Deezer
{
    internal static class DeezerHelpers
    {
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

            string GetCid()
            {
                string cid = string.Empty;

                for (var i = 0; i < 9; i++)
                {
                    cid += new Random().Next(1, 9);
                }

                return cid;
            }
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
            if (json?["error"] != null && json["error"].HasValues)
            {
                foreach (JProperty child in json["error"].Children<JProperty>())
                {
                    Helpers.RedMessage($"[{operation}] {child.Name} - {child.Value.Value<string>()}");
                }
            }
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

        public static bool CheckIfQualityIsAvailable(AudioQuality audioQuality, TrackInfo trackInfo)
        {
            switch (audioQuality)
            {
                case AudioQuality.Flac:
                    return trackInfo.TrackTags.Flac != 0;
                case AudioQuality.Mp3320:
                    return trackInfo.TrackTags.Mp3320 != 0;
                case AudioQuality.Mp3256:
                    return trackInfo.TrackTags.Mp3256 != 0;
                case AudioQuality.Mp3128:
                    return trackInfo.TrackTags.Mp3128 != 0;
                default:
                    return false;
            }
        }

        public static string GetTempTrackPath(string saveLocationDirectory, string id)
        {
            string filename = $"{id}.tmp";

            string tempFilePath = Path.Combine(saveLocationDirectory, filename);

            return tempFilePath;
        }

        public static void RenameFiles(string tempTrackPath, string saveLocation, string saveLocationDirectory)
        {
            try
            {
                File.Move(tempTrackPath, saveLocation);
            }
            catch (IOException ex)
            {
                Console.WriteLine(ex.Message);
                Helpers.RedMessage($"Failed to rename {tempTrackPath} to {saveLocation}");
            }

            string tempLyricFilePath = Path.Combine(saveLocationDirectory,
                Path.GetFileNameWithoutExtension(tempTrackPath) + ".lrc");

            if (File.Exists(tempLyricFilePath))
            {
                string properLyricFilePath =
                    Path.Combine(saveLocationDirectory, Path.GetFileNameWithoutExtension(saveLocation) + ".lrc");

                try
                {
                    File.Move(tempLyricFilePath, properLyricFilePath);
                }
                catch (IOException ex)
                {
                    Console.WriteLine(ex.Message);
                    Helpers.RedMessage($"Failed to rename {tempLyricFilePath} to {properLyricFilePath}");
                }
            }
        }

        public static string BuildSaveLocation(TrackInfo trackInfo, AlbumInfo albumInfo, AudioQuality audioQuality)
        {
            string artist = albumInfo.AlbumTags.Artists[0].Name.SanitseString();
            string type = albumInfo.AlbumTags.Type.SanitseString();
            string albumTitle = albumInfo.AlbumTags.Title.SanitseString();
            string trackTitle = trackInfo.TrackTags.Title.SanitseString();
            string discNumber = trackInfo.TrackTags.DiscNumber.SanitseString();
            string trackNumber = trackInfo.TrackTags.TrackNumber.SanitseString().PadNumber();
            string totalDiscs = albumInfo.AlbumTags.NumberOfDiscs;

            var downloadPath = Configuration.GetValue<string>("downloadLocation");
            string extension = audioQuality == AudioQuality.Flac
                ? ".flac"
                : ".mp3";

            string filename = $"{trackNumber} - {trackTitle}{extension}";
            string directoryPath = $@"{artist}\{albumTitle} ({type})\";

            if (int.Parse(totalDiscs) > 1)
            {
                directoryPath += $@"Disc {discNumber}\";
            }

            string savePath = Path.Combine(downloadPath, directoryPath, filename);

            return savePath;
        }
    }
}