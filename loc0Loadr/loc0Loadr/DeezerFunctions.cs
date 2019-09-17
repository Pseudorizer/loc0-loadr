using System;
using System.Collections.Generic;
using System.Linq;
using loc0Loadr.Enums;
using loc0Loadr.Models;
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

        public KeyValuePair<AudioQuality, int> GetAudioQuality(JToken data, AudioQuality chosenAudioQuality)
        {
            var availableQualities = data.Children()
                .Select(x => (JProperty) x)
                .Where(y => y.Name.Contains("filesize", StringComparison.OrdinalIgnoreCase)
                            && y.Value.Value<int>() != 0)
                .ToList();

            var tries = 0;
            int max = (int) chosenAudioQuality + 1;

            while (tries < max)
            {
                foreach (JProperty availableQuality in availableQualities)
                {
                    if (Helpers.KeyToAudioQuality.ContainsKey(availableQuality.Name))
                    {
                        if (Helpers.KeyToAudioQuality[availableQuality.Name] == chosenAudioQuality)
                        {
                            return new KeyValuePair<AudioQuality, int>(
                                chosenAudioQuality,
                                availableQuality.Value.Value<int>());
                        }
                    }
                }

                tries++;
                chosenAudioQuality = Helpers.GetNextAudioLevelDown(chosenAudioQuality);
            }

            Helpers.RedMessage("Failed to find acceptable quality");
            return new KeyValuePair<AudioQuality, int>();
        }
    }
}