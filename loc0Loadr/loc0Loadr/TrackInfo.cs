
using loc0Loadr.Models;
using Newtonsoft.Json.Linq;

namespace loc0Loadr
{
    internal class TrackInfo
    {
        public TrackTags TrackTags { get; set; }
        public Lyrics Lyrics { get; set; }
        public JObject TrackJson { get; set; }

        public static TrackInfo BuildTrackInfo(JObject trackInfoJObject, JObject officialTrackInfo)
        {
            var trackInfo = new TrackInfo
            {
                TrackJson = trackInfoJObject
            };

            if (trackInfoJObject?["results"]?["DATA"] != null)
            {
                trackInfo.TrackTags = trackInfoJObject["results"]["DATA"].ToObject<TrackTags>();
            }

            if (trackInfoJObject?["results"]?["LYRICS"] != null)
            {
                trackInfo.Lyrics = trackInfoJObject["results"]["LYRICS"].ToObject<Lyrics>();
            }

            if (officialTrackInfo?["bpm"] != null)
            {
                trackInfo.TrackTags.Bpm = officialTrackInfo["bpm"].Value<string>();
            }

            return trackInfo;
        }
    }
}