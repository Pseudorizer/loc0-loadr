using Newtonsoft.Json.Linq;

namespace loc0Loadr.Models
{
    internal class SearchResult
    {
        public string Id { get; set; }
        public string OutputString { get; set; }
        public JObject Json { get; set; }
    }
}