using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using loc0Loadr.Enums;
using Newtonsoft.Json.Linq;

namespace loc0Loadr
{
    internal class DeezerSearcher
    {
        private readonly DeezerHttp _deezerHttp;

        public DeezerSearcher(DeezerHttp deezerHttp)
        {
            _deezerHttp = deezerHttp;
        }

        public async Task<string> Search(string term, SearchType searchType)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return string.Empty;
            }

            var resultLimit = Configuration.GetValue<int>("maxSearchResults");

            if (resultLimit <= 0)
            {
                resultLimit = 40;
            }
            else if (resultLimit > 500)
            {
                resultLimit = 500;
            }

            resultLimit = 20;

            JObject searchResults = await _deezerHttp.HitUnofficialApi("deezer.pageSearch", new JObject
            {
                ["query"] = term,
                ["start"] = 0,
                ["nb"] = resultLimit,
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
            
            var results = new List<SearchResult>();

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
                        
                        results.Add(new SearchResult
                        {
                            Id = id,
                            OutputString = $"[{id}] {artistName} - {title} from {albumName}"
                        });
                    }
                    
                    break;
            }

            if (results.Count == 0)
            {
                Helpers.RedMessage("No results found");
                return string.Empty;
            }

            int r = results.Count / 10;
            int p = 0;

            while (true)
            {
                var skip = p * 10;

                if (skip == results.Count)
                {
                    skip -= 10;
                }

                var g = results.Skip(skip).Take(10).ToList();
                var gLength = g.Count;

                if (gLength == 0)
                {
                    Helpers.RedMessage("No more results found");
                    p = 0;
                    continue;
                }

                Console.WriteLine($"\nPage {p + 1} | {p * 10}-{(p + 1) * 10}/{results.Count}");

                var t = g.Select(x => x.OutputString).ToList();
                t.Add("See ten more");
                t.Add("Go back ten");
                t.Add("Back to start");
                t.Add("No valid results, exit");
                
                var e = Helpers.TakeInput(1, t.Count, t.ToArray());

                if (e == (gLength + 1).ToString())
                {
                    p++;
                }
                else if (e == (gLength + 2).ToString())
                {
                    p = p - 1 < 0 ? 0 : p - 1;
                }
                else if (e == (gLength + 3).ToString())
                {
                    p = 0;
                }
                else if (e == (gLength + 4).ToString())
                {
                    return string.Empty;
                }
                else
                {
                    return g[int.Parse(e)].Id;
                }
            }

            return "";
        }
    }

    internal class SearchResult
    {
        public string Id { get; set; }
        public string OutputString { get; set; }
    }
}