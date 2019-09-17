using System.Collections.Generic;
using loc0Loadr.Enums;
using Newtonsoft.Json.Linq;

namespace loc0Loadr.Models
{
    internal class SingleDownloadInfo
    {
        public string Title { get; set; }
        public JToken Lyrics { get; set; }
        public KeyValuePair<AudioQuality, int> AudioQuality { get; set; }
        public AlbumInfo AlbumInfo { get; } = new AlbumInfo();
    }

    internal class AlbumInfo
    {
        public string Upc { get; set; }
        public string Label { get; set; }
        public string ReleaseDate { get; set; }
        public string NumberOfDiscs { get; set; }
        public string ArtName { get; set; }
        
    }
}