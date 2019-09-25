using Newtonsoft.Json;

namespace loc0Loadr.Models
{
    internal class TrackTags
    {
        [JsonProperty("SNG_ID")]
        public string Id { get; set; }
        
        [JsonProperty("SNG_TITLE")]
        public string Title { get; set; }

        [JsonProperty("ALB_ID")]
        public string AlbumId { get; set; }
        
        [JsonProperty("ARTISTS")]
        public Artists[] Artists { get; set; }
        
        [JsonProperty("MD5_ORIGIN")]
        public string Md5Origin { get; set; }

        [JsonProperty("DURATION")]
        public string Length { get; set; }

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
        
        [JsonIgnore]
        public string Bpm { get; set; }

        [JsonProperty("DISK_NUMBER")]
        public string DiscNumber { get; set; }

        [JsonProperty("TRACK_NUMBER")]
        public string TrackNumber { get; set; }
        
        [JsonProperty("EXPLICIT_LYRICS")]
        public string ExplicitLyrics { get; set; }
        
        [JsonProperty("ISRC")]
        public string Isrc { get; set; }

        [JsonProperty("SNG_CONTRIBUTORS")]
        public Contributors Contributors { get; set; }
        
        [JsonProperty("__TYPE__")]
        public string Type { get; set; }
        
        [JsonProperty("MEDIA_VERSION")]
        public string MediaVersion { get; set; }
    }
    
    internal class Artists
    {
        [JsonProperty("ART_NAME")]
        public string Name { get; set; }
    }

    internal class Lyrics
    {
        [JsonProperty("LYRICS_TEXT")]
        public string UnSyncedLyrics { get; set; }
        
        [JsonProperty("LYRICS_SYNC_JSON")]
        public SyncedLyrics[] SyncedLyrics { get; set; }
    }
    
    public class SyncedLyrics
    {
        [JsonProperty("lrc_timestamp")]
        public string Timestamp { get; set; }
        
        [JsonProperty("line")]
        public string Line { get; set; }
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
        public string[] Authors { get; set; }
        
        [JsonProperty("mixer")]
        public string[] Mixers { get; set; }
    }
}