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
        
        private string _numberOfDisks;
        [JsonProperty("NUMBER_DISK")]
        public string NumberOfDisks {
            get => _numberOfDisks;
            set => _numberOfDisks = value.PadNumber();
        }

        private string _numberOfTracks;
        [JsonProperty("NUMBER_TRACK")]
        public string NumberOfTracks {
            get => _numberOfTracks;
            set => _numberOfTracks = value.PadNumber();
        }
        
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

        private string _type;
        [JsonProperty("record_type")]
        public string Type{
            get => _type;
            set
            {
                if (string.Equals("ep", value, StringComparison.OrdinalIgnoreCase))
                {
                    _type = "EP";
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