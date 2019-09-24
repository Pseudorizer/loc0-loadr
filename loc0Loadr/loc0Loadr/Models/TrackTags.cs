using Newtonsoft.Json;

namespace loc0Loadr.Models
{
    internal class TrackTags
    {
        [JsonProperty("SNG_ID")]
        public string Id { get; set; }
        
        [JsonProperty("SNG_TITLE")]
        public string Title { get; set; }
        
        [JsonProperty("ART_NAME")]
        public string ArtistName { get; set; }
        
        [JsonProperty("ARTISTS")]
        public Artists[] Artists { get; set; }
        
        [JsonProperty("MD5_ORIGIN")]
        public string Md5Origin { get; set; }

        [JsonProperty("DURATION")]
        public string Duration { get; set; }

        [JsonProperty("FILESIZE_MP3_128")]
        public long Mp3128 { get; set; }

        [JsonProperty("FILESIZE_MP3_256")]
        public long Mp3256 { get; set; }

        [JsonProperty("FILESIZE_MP3_320")]
        public long Mp3320 { get; set; }

        [JsonProperty("FILESIZE_FLAC")]
        public long Flac { get; set; }
        
        [JsonProperty("GAIN")]
        public string Gain { get; set; }
        
        [JsonProperty("DISK_NUMBER")]
        public string DiskNumber { get; set; }

        [JsonProperty("TRACK_NUMBER")]
        public string TrackNumber { get; set; }
        
        [JsonProperty("EXPLICIT_LYRICS")]
        public string ExplicitLyrics { get; set; }
        
        [JsonProperty("ISRC")]
        public string Isrc { get; set; }
        
        [JsonProperty("LYRICS_ID")]
        public string LyricsId { get; set; }
        
        [JsonProperty("SNG_CONTRIBUTORS")]
        public Contributors Contributors { get; set; }
    }
    
    internal class Artists
    {
        [JsonProperty("ART_NAME")]
        public string Name { get; set; }
    }
    
    internal class Contributors
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
        public string[] Author { get; set; }
        
        [JsonProperty("mixer")]
        public string[] Mixer { get; set; }
    }
}