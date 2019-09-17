using Newtonsoft.Json;

namespace loc0Loadr.Models
{
    internal class ConfigFile
    {
        [JsonProperty("arl")]
        public string Arl { get; set; }

        [JsonProperty("downloadLocation")]
        public string DownloadLocation { get; set; }
    }
}