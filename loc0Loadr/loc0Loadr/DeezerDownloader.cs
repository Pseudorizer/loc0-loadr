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
        private string AudioQualityOutput => Helpers.AudioQualityToOutputString[_audioQuality];
        private TrackInfo _trackInfo;
        private AlbumInfo _albumInfo;

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
            _albumInfo = await GetAlbumInfo(id);
                
            foreach (JObject albumInfoSong in _albumInfo.Songs.Children<JObject>())
            {
                var trackId = albumInfoSong["SNG_ID"].Value<string>();

                _trackInfo = TrackInfo.BuildTrackInfo(albumInfoSong);
                
                var f = await ProcessTrack(trackId);
            }

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

        public async Task<bool> DownloadPlaylist(string id)
        {
            
            return true;
        }

        public async Task<bool> ProcessTrack(string id)
        {
            if (_trackInfo == null)
            {
                _trackInfo = await GetTrackInfo(id);

                if (_trackInfo == null)
                {
                    return false;
                }
            }

            if (_albumInfo == null)
            {
                var albumId = _trackInfo.TrackJson["ALB_ID"].Value<string>();

                if (albumId != "0")
                {
                    _albumInfo = await GetAlbumInfo(albumId);
                }
            }

            if (!UpdateAudioQualityToAvailable())
            {
                Helpers.RedMessage("Failed to find valid quality");
                return false;
            }

            string saveLocation = BuildSaveLocation();
            
            return true;
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

        private bool UpdateAudioQualityToAvailable()
        {
            var enumIds = new List<int> {1, 5, 3, 9};

            int startIndex = enumIds.IndexOf((int) _audioQuality);

            if (_audioQuality == AudioQuality.Flac)
            {
                enumIds.Reverse();
                startIndex = 0;
            }

            if (ValidAudioQualityFound(enumIds, startIndex, enumIds.Count))
            {
                return true;
            }

            if (_audioQuality != AudioQuality.Flac)
            {
                enumIds.RemoveRange(startIndex, 4 - startIndex);
                enumIds.Reverse();
            }

            return ValidAudioQualityFound(enumIds, 0, startIndex);
        }

        private bool ValidAudioQualityFound(IReadOnlyList<int> enumIds, int startIndex, int endIndex)
        {
            for (int index = startIndex; index < endIndex; index++)
            {
                int enumId = enumIds[index];
                var tempAudioQuality = (AudioQuality) enumId;
                bool qualityIsAvailable = CheckIfQualityIsAvailable(tempAudioQuality);

                if (qualityIsAvailable)
                {
                    _audioQuality = tempAudioQuality;
                    return true;
                }
            }
            
            return false;
        }
        
        private bool CheckIfQualityIsAvailable(AudioQuality audioQuality)
        {
            switch (audioQuality)
            {
                case AudioQuality.Flac:
                    return _trackInfo.TrackTags.Flac != 0;
                case AudioQuality.Mp3320:
                    return _trackInfo.TrackTags.Mp3320 != 0;
                case AudioQuality.Mp3256:
                    return _trackInfo.TrackTags.Mp3256 != 0;
                case AudioQuality.Mp3128:
                    return _trackInfo.TrackTags.Mp3128 != 0;
                default:
                    return false;
            }
        }

        private string BuildSaveLocation()
        {
            string artist = _trackInfo.TrackTags.Artists[0].Name;

            return "";
        }
    }
}