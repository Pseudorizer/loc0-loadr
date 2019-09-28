using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using loc0Loadr.Enums;
using loc0Loadr.Models;
using Newtonsoft.Json.Linq;

namespace loc0Loadr.Deezer
{
    internal class DeezerSearcher
    {
        private readonly DeezerHttp _deezerHttp;

        public DeezerSearcher(DeezerHttp deezerHttp)
        {
            _deezerHttp = deezerHttp;
        }

        public async Task<SearchResult> Search(string term, SearchType searchType)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return null;
            }

            JObject searchResults = await GetResults(term);

            if (searchResults == null)
            {
                return null;
            }

            var results = new List<SearchResult>();

            switch (searchType)
            {
                case SearchType.Track:
                    results = SearchResultParser.GetTrackResults(searchResults).ToList();
                    break;
                case SearchType.Album:
                    results = SearchResultParser.GetAlbumResults(searchResults).ToList();
                    break;
            }

            if (results.Count == 0)
            {
                Helpers.RedMessage("No results found");
                return null;
            }

            return ChooseResult(results);
        }

        private async Task<JObject> GetResults(string term)
        {
            var resultLimit = Configuration.GetValue<int>("maxSearchResults");

            if (resultLimit <= 0)
            {
                resultLimit = 40;
            }
            else if (resultLimit > 500)
            {
                resultLimit = 500;
            }


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
                return null;
            }

            return searchResults;
        }

        private static SearchResult ChooseResult(IReadOnlyCollection<SearchResult> results)
        {
            var page = 0;

            while (true)
            {
                int skip = page * 10;

                if (skip == results.Count)
                {
                    skip -= 10;
                    page -= 1;
                }

                var pageResults = results.Skip(skip).Take(10).ToList();
                int pageResultsLength = pageResults.Count;

                // not sure what to name these yet
                int k = (page + 1) * 10;
                int j = 10 - pageResultsLength;
                k -= j;

                Console.WriteLine($"\nPage {page + 1} | {page * 10}-{k}/{results.Count}");

                var outputStrings = pageResults.Select(x => x.OutputString).ToList();
                outputStrings.Add("See ten more");
                outputStrings.Add("Go back ten");
                outputStrings.Add("Back to start");
                outputStrings.Add("No valid results, exit");

                string searchAction = Helpers.TakeInput(1, outputStrings.Count, outputStrings.ToArray());

                if (searchAction == (pageResultsLength + 1).ToString())
                {
                    page++;
                }
                else if (searchAction == (pageResultsLength + 2).ToString())
                {
                    page = page - 1 < 0
                        ? 0
                        : page - 1;
                }
                else if (searchAction == (pageResultsLength + 3).ToString())
                {
                    page = 0;
                }
                else if (searchAction == (pageResultsLength + 4).ToString())
                {
                    return null;
                }
                else
                {
                    return pageResults[int.Parse(searchAction) - 1];
                }
            }
        }
    }
    
    internal static class SearchResultParser
    {
        public static IEnumerable<SearchResult> GetTrackResults(JObject searchResults)
        {
            if (searchResults["results"]?["TRACK"]?["data"] == null)
            {
                Helpers.RedMessage("No track results found");
                yield break;
            }

            JEnumerable<JObject> tracks = searchResults["results"]["TRACK"]["data"].Children<JObject>();

            if (!tracks.Any())
            {
                Helpers.RedMessage("No track results found");
                yield break;
            }

            foreach (JObject track in tracks)
            {
                var id = track?["SNG_ID"].Value<string>();
                var title = track?["SNG_TITLE"].Value<string>();
                var artistName = track?["ART_NAME"].Value<string>();
                var albumName = track?["ALB_TITLE"].Value<string>();

                yield return new SearchResult
                {
                    Id = id,
                    OutputString = $"[Artist: {artistName}] [Track: {title}] [Album: {albumName}]",
                    Json = track
                };
            }
        }
        
        public static IEnumerable<SearchResult> GetAlbumResults(JObject searchResults)
        {
            if (searchResults["results"]?["ALBUM"]?["data"] == null)
            {
                Helpers.RedMessage("No album results found");
                yield break;
            }

            var albums = searchResults["results"]["ALBUM"]["data"].Children<JObject>();

            if (!albums.Any())
            {
                Helpers.RedMessage("No album results found");
                yield break;
            }

            foreach (JObject album in albums)
            {
                var id = album?["ALB_ID"].Value<string>();
                var albumName = album?["ALB_TITLE"].Value<string>();
                var artistName = album?["ART_NAME"].Value<string>();

                yield return new SearchResult
                {
                    Id = id,
                    OutputString = $"[Artist: {artistName}] [Album: {albumName}]",
                    Json = album
                };
            }
        }
    }
}