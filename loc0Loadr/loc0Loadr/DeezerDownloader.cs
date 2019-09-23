using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using loc0Loadr.Enums;
using loc0Loadr.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace loc0Loadr
{
    internal class DeezerDownloader
    {
        private readonly DeezerHttp _deezerHttp;
        private AudioQuality _audioQuality;
        private string _audioQualityOutput;

        public DeezerDownloader(DeezerHttp deezerHttp, AudioQuality audioQuality)
        {
            _deezerHttp = deezerHttp;
            _audioQuality = audioQuality;
        }
        
        public async Task<bool> ProcessArtist(string id)
        {
            JObject discographyInfo = await _deezerHttp.HitUnofficialApi("album.getDiscography", new JObject
            {
                ["art_id"] = id,
                ["filter_role_id"] = new JArray("0"),
                ["lang"] = "us",
                ["nb"] = 500,
                ["nb_songs"] = -1,
                ["start"] = 0
            });
            
            discographyInfo.DisplayDeezerErrors("Discography");

            if (discographyInfo["results"]?["data"] == null || discographyInfo["results"]["count"].Value<int>() <= 0)
            {
                Helpers.RedMessage("No items found in artist discography");
                return false;
            }

            var discographyItems = (JArray) discographyInfo["results"]["data"];

            foreach (JObject discographyItem in discographyItems.Children<JObject>())
            {
                var albumId = discographyItem["ALB_ID"].Value<string>();

                bool albumInfo = await ProcessAlbum(albumId);
            }

            return true;
        }

        public async Task<bool> ProcessAlbum(string id)
        {
            AlbumInfo albumInfo = await GetAlbumInfo(id);
                
            foreach (JObject albumInfoSong in albumInfo.Songs.Children<JObject>())
            {
                var trackId = albumInfoSong["SNG_ID"].Value<string>();

                TrackInfo trackInfo = TrackInfo.BuildTrackInfo(albumInfoSong);
                
                var f = await ProcessTrack(trackId, albumInfo, trackInfo);
            }

            return true;
        }

        public async Task<bool> DownloadPlaylist(string id)
        {
            
            return true;
        }
        
        private async Task<AlbumInfo> GetAlbumInfo(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }
            
            JObject albumJson = await _deezerHttp.HitUnofficialApi("deezer.pageAlbum", new JObject
            {
                ["ALB_ID"] = id,
                ["lang"] = "us",
                ["tab"] = 0
            });

            if (albumJson?["results"]?["DATA"] == null || albumJson["results"]?["SONGS"]?["data"] == null)
            {
                return null;
            }
            
            JObject officialAlbumJson = await _deezerHttp.HitOfficialApi("album", id);

            // ReSharper disable once ConvertIfStatementToReturnStatement
            if (officialAlbumJson == null)
            {
                return AlbumInfo.BuildAlbumInfo(albumJson, null);
            }
            
            return AlbumInfo.BuildAlbumInfo(albumJson, officialAlbumJson);
        }

        private async Task<TrackInfo> GetTrackInfo(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }
            
            JObject trackInfoJObject = await _deezerHttp.HitUnofficialApi("deezer.pageTrack", new JObject
            {
                ["SNG_ID"] = id
            });

            return trackInfoJObject == null
                ? null
                : TrackInfo.BuildTrackInfo(trackInfoJObject);
        }

        public async Task<bool> ProcessTrack(string id, AlbumInfo albumInfo = null, TrackInfo trackInfo = null)
        {
            if (trackInfo == null)
            {
                trackInfo = await GetTrackInfo(id);

                if (trackInfo == null)
                {
                    return false;
                }
            }

            if (albumInfo == null)
            {
                var albumId = trackInfo.TrackJson["ALB_ID"].Value<string>();

                if (albumId != "0")
                {
                    albumInfo = await GetAlbumInfo(albumId);
                
                    if (albumInfo == null)
                    {
                        return false;
                    }
                }
            }
            
            
            
            return true;
        }

        public void SetAudioQuality(TrackInfo trackInfo, AudioQuality audioQuality)
        {
            _audioQuality = audioQuality;
            
            var enumIds = new List<int> {1, 5, 3, 9};

            int startIndex = enumIds.IndexOf((int) audioQuality);

            if (audioQuality == AudioQuality.Flac)
            {
                enumIds.Reverse();
                startIndex = 0;
            }

            for (int index = startIndex; index < enumIds.Count; index++)
            {
                int enumId = enumIds[index];
                ChosenAudioQuality newQuality = SearchForQuality(availableQualities, (AudioQuality) enumId);

                if (newQuality != null)
                {
                    return newQuality;
                }
            }

            if (audioQuality != AudioQuality.Flac)
            {
                enumIds.RemoveRange(startIndex, 4 - startIndex);
                enumIds.Reverse();
            }

            for (var i = 0; i < startIndex; i++)
            {
                int enumId = enumIds[i];
                ChosenAudioQuality newQuality = SearchForQuality(availableQualities, (AudioQuality) enumId);

                if (newQuality != null)
                {
                    return newQuality;
                }
                
            }

            return null;
        }
        
        private ChosenAudioQuality SearchForQuality(TrackInfo qualities, AudioQuality audioQuality)
        {
            return qualities
                .Where(x => Helpers.KeyToAudioQuality.ContainsKey(x.Name))
                .Where(y => Helpers.KeyToAudioQuality[y.Name] == audioQuality)
                .Select(z => new ChosenAudioQuality
                {
                    Extension = audioQuality == AudioQuality.Flac
                        ? "flac"
                        : "mp3",
                    AudioEnumId = (int) audioQuality,
                    QualityForOutput = Helpers.AudioQualityToOutputString[audioQuality],
                    Size = z.Value.Value<long>()
                })
                .FirstOrDefault();
        }
    }
}