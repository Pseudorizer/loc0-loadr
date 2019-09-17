using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace loc0Loadr.Models
{
    internal class SearchResult
    {
        public TrackType Type { get; set; }
        public string Title { get; set; }
        public JToken Json { get; set; }
        public int Id { get; set; }
        public IEnumerable<string> Artists { get; set; }
    }
}