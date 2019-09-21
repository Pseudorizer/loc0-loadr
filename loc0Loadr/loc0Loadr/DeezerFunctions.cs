using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using loc0Loadr.Enums;
using loc0Loadr.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace loc0Loadr
{
    internal class DeezerFunctions
    {
        public IEnumerable<SearchResult> ParseSearchJson(JObject json, TrackType type)
        {
            JToken albums = json["results"]?["ALBUM"];
            JToken artists = json["results"]?["ARTIST"];
            JToken tracks = json["results"]?["TRACK"];

            switch (type)
            {
                case TrackType.Track:
                    if (tracks != null && tracks["count"].Value<int>() > 0)
                    {
                        var data = (JArray) tracks["data"];

                        return data
                            .Take(10)
                            .Select(item => new SearchResult
                            {
                                Type = TrackType.Track,
                                Json = item,
                                Title = item["SNG_TITLE"].Value<string>(),
                                Id = item["SNG_ID"].Value<int>(),
                                Artists = item["ARTISTS"]
                                    .Select(r => r["ART_NAME"].Value<string>())
                            });
                    }

                    break;
                case TrackType.Album:
                    if (albums != null && albums["count"].Value<int>() > 0)
                    {
                        var data = (JArray) albums["data"];

                        return data
                            .Take(10)
                            .Select(item => new SearchResult
                            {
                                Type = TrackType.Album,
                                Json = item,
                                Title = item["ALB_TITLE"].Value<string>(),
                                Id = item["ALB_ID"].Value<int>(),
                                Artists = item["ARTISTS"]
                                    .Select(artist => artist["ART_NAME"].Value<string>())
                            });
                    }

                    break;
                case TrackType.Artist:
                    if (artists != null && artists["count"].Value<int>() > 0)
                    {
                        var data = (JArray) artists["data"];

                        return data
                            .Take(10)
                            .Select(item => new SearchResult
                            {
                                Type = TrackType.Artist,
                                Json = item,
                                Title = item["ART_NAME"].Value<string>(),
                                Id = item["ART_ID"].Value<int>()
                            });
                    }

                    break;
            }

            return null;
        }

        // the way this works is that if the wanted quality was not found, the next best will be tried and so on
        // until it wraps around to the start and tries the lower quality options, starting at highest lower quality
        // I.E. 320 -> FLAC -> 256 -> 128 -> null
        public ChosenAudioQuality GetAudioQuality(JToken data, AudioQuality audioQuality)
        {
            var enumIds = new List<int> {1, 5, 3, 9};

            List<JProperty> availableQualities = data.Children()
                .Select(x => (JProperty) x)
                .Where(y => y.Name.Contains("filesize", StringComparison.OrdinalIgnoreCase)
                            && y.Value.Value<int>() != 0)
                .ToList();

            int startIndex = enumIds.IndexOf((int) audioQuality);

            if (audioQuality == AudioQuality.Flac)
            {
                enumIds.Reverse();
                startIndex = 0;
            }

            for (int index = startIndex; index < enumIds.Count; index++)
            {
                int enumId = enumIds[index];
                ChosenAudioQuality newQuality = SearchForQuality(availableQualities, (AudioQuality) enumId);

                if (newQuality != null)
                {
                    return newQuality;
                }
            }

            if (audioQuality != AudioQuality.Flac)
            {
                enumIds.RemoveRange(startIndex, 4 - startIndex);
                enumIds.Reverse();
            }

            for (var i = 0; i < startIndex; i++)
            {
                int enumId = enumIds[i];
                ChosenAudioQuality newQuality = SearchForQuality(availableQualities, (AudioQuality) enumId);

                if (newQuality != null)
                {
                    return newQuality;
                }
                
            }

            return null;
        }

        private ChosenAudioQuality SearchForQuality(IEnumerable<JProperty> qualities, AudioQuality audioQuality)
        {
            return qualities
                .Where(x => Helpers.KeyToAudioQuality.ContainsKey(x.Name))
                .Where(y => Helpers.KeyToAudioQuality[y.Name] == audioQuality)
                .Select(z => new ChosenAudioQuality
                {
                    Extension = audioQuality == AudioQuality.Flac
                        ? "flac"
                        : "mp3",
                    AudioEnumId = (int) audioQuality,
                    QualityForOutput = Helpers.AudioQualityToOutputString[audioQuality],
                    Size = z.Value.Value<long>()
                })
                .FirstOrDefault();
        }

        public JToken AddOfficialTrackInfo(JToken officialTrackInfo, JToken trackInfo)
        {
            if (officialTrackInfo["bpm"] != null)
            {
                trackInfo["BPM"] = officialTrackInfo["bpm"].Value<string>();
            }

            if (officialTrackInfo["gain"] != null)
            {
                trackInfo["GAIN"] = officialTrackInfo["gain"].Value<string>();
            }

            return trackInfo;
        }

        public JToken AddAlbumInfo(JToken albumInfo, JToken trackInfo)
        {
            if (albumInfo?["DATA"]?["UPC"] != null)
            {
                trackInfo["UPC"] = albumInfo["DATA"]["UPC"].Value<string>();
            }

            if (trackInfo["PHYSICAL_RELEASE_DATE"] == null && albumInfo?["DATA"]?["PHYSICAL_RELEASE_DATE"] != null)
            {
                trackInfo["PHYSICAL_RELEASE_DATE"] = albumInfo["DATA"]["PHYSICAL_RELEASE_DATE"].Value<string>();
            }

            if (albumInfo?["SONGS"]?["data"].Children().Last()["DISK_NUMBER"] != null)
            {
                trackInfo["NUMBER_OF_DISKS"] =
                    albumInfo["SONGS"]["data"].Children().Last()["DISK_NUMBER"].Value<string>();
            }

            if (trackInfo["ART_NAME"] == null || albumInfo?["DATA"]?["ART_NAME"].Value<string>() == "Various Artists" && albumInfo["DATA"]?["ART_NAME"] != null)
            {
                trackInfo["ART_NAME"] = albumInfo["DATA"]["ART_NAME"].Value<string>();
            }

            if (albumInfo?["DATA"]?["LABEL_NAME"] != null)
            {
                trackInfo["ALB_LABEL"] = albumInfo["DATA"]["LABEL_NAME"].Value<string>();
            }

            if (albumInfo?["SONGS"]?["count"] != null)
            {
                trackInfo["ALB_NUM_TRACKS"] = albumInfo["SONGS"]["count"].Value<string>();
            }

            return trackInfo;
        }

        public JToken AddOfficialAlbumInfo(JToken officialAlbumInfo, JToken trackInfo)
        {
            if (officialAlbumInfo?["record_type"] != null)
            {
                trackInfo["__TYPE__"] = officialAlbumInfo["record_type"].Value<string>();
            }

            if (officialAlbumInfo?["genres"] != null && officialAlbumInfo["genres"].HasValues)
            {
                trackInfo["GENRES"] = officialAlbumInfo["genres"]["data"];
            }
            
            return trackInfo;
        }
        
        public string BuildSaveLocation(JToken trackInfo)
        {
            var artist = trackInfo["ART_NAME"]?.Value<string>();
            artist = artist.SanitseString();

            var albumType = trackInfo["__TYPE__"]?.Value<string>();
            albumType = albumType.SanitseString();

            if (albumType == "ep")
            {
                albumType = "EP";
            }
            else if (string.Equals(albumType, "compile", StringComparison.OrdinalIgnoreCase))
            {
                albumType = "Compilation";
            }
            else
            {
                albumType = char.ToUpper(albumType[0]) + albumType.Substring(1); 
            }

            var albumTitle = trackInfo["ALB_TITLE"]?.Value<string>();
            albumTitle = albumTitle.SanitseString();

            if (string.IsNullOrWhiteSpace(albumTitle))
            {
                albumTitle = "Unknown Album";
            }

            var title = trackInfo["SNG_TITLE"]?.Value<string>();
            title = title.SanitseString();

            var discNumber = trackInfo["DISK_NUMBER"]?.Value<string>();
            discNumber = discNumber.SanitseString().PadNumber();

            var trackIndex = trackInfo["TRACK_NUMBER"]?.Value<string>();
            trackIndex = trackIndex.SanitseString().PadNumber();

            var downloadPath = Configuration.GetValue<string>("downloadLocation");

            var extension = trackInfo["QUALITY"]?["Extension"]?.Value<string>();
            string filename = $"{trackIndex} - {title}.{extension}";

            string dirPath = $@"{artist}\{albumTitle} ({albumType})\";

            if (trackInfo["NUMBER_OF_DISKS"]?.Value<int>() > 1)
            {
                dirPath += $"Disc {discNumber}";
            }

            string saveLocation = Path.Combine(downloadPath, dirPath, filename);

            return saveLocation;
        }
    }
}