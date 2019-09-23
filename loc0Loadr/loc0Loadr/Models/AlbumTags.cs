using System.Collections.Generic;
using Newtonsoft.Json;

namespace loc0Loadr.Models
{
    internal class AlbumTags
    {
        [JsonProperty("NUMBER_DISK")]
        public string NumberOfDisks { get; set; }
        
        [JsonProperty("NUMBER_TRACK")]
        public string NumberOfTracks { get; set; }
        
        [JsonProperty("PHYSICAL_RELEASE_DATE")]
        public string ReleaseDate { get; set; }
        
        [JsonProperty("COPYRIGHT")]
        public string Copyright { get; set; }
        
        [JsonProperty("ALB_PICTURE")]
        public string PictureId { get; set; }
        
        [JsonProperty("LABEL_NAME")]
        public string Label { get; set; }
        
        [JsonProperty("UPC")]
        public string Barcode { get; set; }
        
        [JsonProperty("genres")]
        public Genres Genres { get; set; }
    }

    internal class Genres
    {
        [JsonProperty("data")]
        public GenreData[] GenreData { get; set; }
    }

    internal class GenreData
    {
        [JsonProperty("name")]
        public string Name { get; set; }
    }
}