using System.Collections.Generic;
using System.Threading.Tasks;
using loc0Loadr.Enums;
using Newtonsoft.Json.Linq;

namespace loc0Loadr
{
    internal class DeezerSearcher
    {
        private readonly DeezerHttp _deezerHttp;
        private readonly AudioQuality _audioQuality;

        public DeezerSearcher(DeezerHttp deezerHttp, AudioQuality audioQuality)
        {
            _deezerHttp = deezerHttp;
            _audioQuality = audioQuality;
        }

        public async Task<string> Search(string term, SearchType searchType)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return string.Empty;
            }

            JObject searchResults = await _deezerHttp.HitUnofficialApi("deezer.pageSearch", new JObject
            {
                ["query"] = term,
                ["start"] = 0,
                ["nb"] = 40,
                ["suggest"] = false,
                ["artist_suggest"] = false,
                ["top_tracks"] = false
            });
            
            searchResults.DisplayDeezerErrors("Search");

            if (searchResults?["results"] == null)
            {
                Helpers.RedMessage("Results object was null");
                return string.Empty;
            }
            
            var results = new List<string>();

            switch (searchType)
            {
                case SearchType.Track:
                    if (searchResults["results"]?["TRACK"] == null || searchResults["results"]["TRACK"]["count"].Value<int>() <= 0)
                    {
                        Helpers.RedMessage("No track results found");
                        return string.Empty;
                    }

                    JEnumerable<JObject> tracks = searchResults["results"]["TRACK"]["data"].Children<JObject>();
                    
                    foreach (JObject track in tracks)
                    {
                        var id = track?["SNG_ID"].Value<string>();
                        var title = track?["SNG_TITLE"].Value<string>();
                        var artistName = track?["ART_NAME"].Value<string>();
                        var albumName = track?["ALB_TITLE"].Value<string>();
                        
                        results.Add($"[{id}] {artistName} - {title} from {albumName}");
                    }
                    
                    break;
            }

            return "";
        }
    }
}