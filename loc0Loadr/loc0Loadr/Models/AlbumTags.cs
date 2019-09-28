using System;
using Newtonsoft.Json;

namespace loc0Loadr.Models
{
    internal class AlbumTags
    {
        private string _title;
        [JsonProperty("ALB_TITLE")]
        public string Title
        {
            get => _title;
            set => _title = string.IsNullOrWhiteSpace(value)
                ? "Unknown Album"
                : value;
        }
        
        [JsonIgnore]
        public string NumberOfDiscs { get; set; }

        [JsonProperty("NUMBER_TRACK")]
        public string NumberOfTracks { get; set; }
        
        [JsonProperty("ARTISTS")]
        public Artists[] Artists { get; set; }

        [JsonProperty("PHYSICAL_RELEASE_DATE")]
        public string ReleaseDate { get; set; }
        
        [JsonProperty("COPYRIGHT")]
        public string Copyright { get; set; }
        
        [JsonProperty("ALB_PICTURE")]
        public string PictureId { get; set; }
        
        [JsonProperty("LABEL_NAME")]
        public string Label { get; set; }
        
        [JsonProperty("UPC")]
        public string Upc { get; set; }
        
        [JsonProperty("genres")]
        public Genres Genres { get; set; }

        private string _type;
        [JsonProperty("__TYPE__")]
        public string Type{
            get => _type;
            set
            {
                if (string.Equals("ep", value, StringComparison.OrdinalIgnoreCase))
                {
                    _type = "EP";
                }
                else if (string.Equals("compile", value, StringComparison.OrdinalIgnoreCase))
                {
                    _type = "Compilation";
                }
                else if (string.IsNullOrWhiteSpace(value))
                {
                    _type = "";
                }
                else
                {
                    _type = char.ToUpper(value[0]) + value.Substring(1);
                }
            }}
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