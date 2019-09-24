using System.Linq;
using loc0Loadr.Models;
using Newtonsoft.Json.Linq;

namespace loc0Loadr
{
    internal class AlbumInfo
    {
        public AlbumTags AlbumTags { get; set; }
        public JArray Songs { get; set; }

        public static AlbumInfo BuildAlbumInfo(JObject albumInfoJObject, JObject officialAlbumInfo)
        {
            var albumInfo = new AlbumInfo
            {
                AlbumTags = albumInfoJObject["results"]["DATA"].ToObject<AlbumTags>(),
                Songs = (JArray) albumInfoJObject["results"]["SONGS"]["data"]
            };

            JToken lastSong = albumInfo.Songs?.Last();

            if (lastSong?["DISK_NUMBER"] != null)
            {
                albumInfo.AlbumTags.NumberOfDiscs = lastSong["DISK_NUMBER"].Value<string>();
            }

            if (officialAlbumInfo?["genres"] != null)
            {
                albumInfo.AlbumTags.Genres = officialAlbumInfo["genres"].ToObject<Genres>();
            }

            if (officialAlbumInfo?["record_type"] != null)
            {
                albumInfo.AlbumTags.Type = officialAlbumInfo["record_type"].Value<string>();
            }

            if (albumInfo.AlbumTags.NumberOfTracks == null && albumInfoJObject["results"]["SONGS"]?["total"] != null)
            {
                albumInfo.AlbumTags.NumberOfTracks = albumInfoJObject["results"]["SONGS"]["total"].Value<string>();
            }

            return albumInfo;
        }
    }
}