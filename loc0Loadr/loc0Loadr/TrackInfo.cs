
using loc0Loadr.Models;
using Newtonsoft.Json.Linq;

namespace loc0Loadr
{
    internal class TrackInfo
    {
        public TrackTags TrackTags { get; set; }
        public JObject TrackJson { get; set; }

        public static TrackInfo BuildTrackInfo(JObject trackInfoJObject)
        {
            return new TrackInfo
            {
                TrackJson = trackInfoJObject,
                TrackTags = trackInfoJObject.ToObject<TrackTags>()
            };
        }
    }
}