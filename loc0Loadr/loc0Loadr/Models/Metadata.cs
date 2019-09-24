using Newtonsoft.Json;

namespace loc0Loadr.Models
{
    public class Metadata
    {
        [JsonProperty("SNG_TITLE")]
        public string Title { get; set; }
        
        [JsonProperty("ALB_TITLE")]
        public string AlbumTitle { get; set; }
        
        [JsonProperty("__TYPE__")]
        public string Type { get; set; }
        
        [JsonProperty("ARTISTS")]
        public Artist[] Artists { get; set; }
        
        [JsonProperty("GENRES")]
        public Genre[] Genres { get; set; }
        
        [JsonProperty("ART_NAME")]
        public string AlbumArtist { get; set; }
        
        [JsonProperty("TRACK_NUMBER")]
        public string TrackNumber { get; set; }
        
        [JsonProperty("ALB_NUM_TRACKS")]
        public string TotalTrackNumber { get; set; }
        
        [JsonProperty("DISK_NUMBER")]
        public string DiskNumber { get; set; }
        
        [JsonProperty("GAIN")]
        public string Gain { get; set; }
        
        [JsonProperty("NUMBER_OF_DISKS")]
        public string TotalDisks { get; set; }
        
        [JsonProperty("ALB_LABEL")]
        public string Label { get; set; }
        
        [JsonProperty("COPYRIGHT")]
        public string Copyright { get; set; }

        [JsonProperty("SNG_CONTRIBUTORS")]
        public Contributorss Contributors { get; set; }
        
        [JsonProperty("ISRC")]
        public string Isrc { get; set; }

        [JsonProperty("DURATION")]
        public string Duration { get; set; }
        
        [JsonProperty("BPM")]
        public string Bpm { get; set; }
        
        [JsonProperty("UPC")]
        public string Upc { get; set; }
        
        [JsonProperty("EXPLICIT_LYRICS")]
        public string HasExplicitLyrics { get; set; }
        
        [JsonProperty("LYRICS")]
        public Lyricss Lyrics { get; set; }
        
        [JsonProperty("PHYSICAL_RELEASE_DATE")]
        public string Year { get; set; }
    }

    public class Artist
    {
        [JsonProperty("ART_NAME")]
        public string Name { get; set; }
    }

    public class Genre
    {
        [JsonProperty("name")]
        public string Name { get; set; }
    }

    public class Contributorss
    {
        [JsonProperty("composer")]
        public string[] Composers { get; set; }
        
        [JsonProperty("musicpublisher")]
        public string[] Publishers { get; set; }
        
        [JsonProperty("producer")]
        public string[] Producers { get; set; }
        
        [JsonProperty("engineer")]
        public string[] Engineers { get; set; }
        
        [JsonProperty("writer")]
        public string[] Writers { get; set; }
        
        [JsonProperty("author")]
        public string[] Authors { get; set; }
        
        [JsonProperty("mixer")]
        public string[] Mixers { get; set; }
    }

    public class Lyricss
    {
        [JsonProperty("LYRICS_TEXT")]
        public string UnSyncedLyrics { get; set; }
        
        [JsonProperty("LYRICS_SYNC_JSON")]
        public SyncedLyrics[] SyncedLyrics { get; set; }
    }

    public class SyncLyricss
    {
        [JsonProperty("lrc_timestamp")]
        public string Timestamp { get; set; }
        
        [JsonProperty("line")]
        public string Line { get; set; }
    }
}