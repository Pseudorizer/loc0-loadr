using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using loc0Loadr.Enums;
using Newtonsoft.Json.Linq;

namespace loc0Loadr
{
    internal class DeezerDownloader
    {
        private readonly DeezerHttp _deezerHttp;
        private AudioQuality _audioQuality;
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

            if (_albumInfo == null)
            {
                Helpers.RedMessage("Failed to get album info");
                return false;
            }
                
            foreach (JObject albumInfoSong in _albumInfo.Songs.Children<JObject>())
            {
                var trackId = albumInfoSong["SNG_ID"].Value<string>();
                
                var f = await ProcessTrack(trackId);
            }

            return true;
        }

        private async Task<AlbumInfo> GetAlbumInfo(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id == "0")
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
                    Helpers.RedMessage("Failed to get tracl info");
                    return false;
                }
            }

            if (_albumInfo == null)
            {
                var albumId = _trackInfo.TrackJson["ALB_ID"].Value<string>();
                
                _albumInfo = await GetAlbumInfo(albumId);

                if (_albumInfo == null)
                {
                    Helpers.RedMessage("Failed to get album info");
                    return false;
                }
            }

            if (!UpdateAudioQualityToAvailable())
            {
                Helpers.RedMessage("Failed to find valid quality");
                return false;
            }

            string saveLocation = BuildSaveLocation();
            string saveLocationDirectory = Path.GetDirectoryName(saveLocation);
            string tempTrackPath = GetTempTrackPath(saveLocationDirectory);

            if (File.Exists(saveLocation))
            {
                Helpers.GreenMessage("File already exists");
                return true;
            }

            byte[] decryptedBytes = await GetDecryptedBytes();

            if (!Helpers.WriteTrackBytes(decryptedBytes, tempTrackPath))
            {
                Helpers.RedMessage("Failed to write file to disk");
                return false;
            }

            byte[] albumCover = await GetAndSaveAlbumArt(_albumInfo.AlbumTags.PictureId, saveLocationDirectory);
            
            var e = new NewMetadataWriter(_trackInfo, _albumInfo, tempTrackPath, albumCover);
            e.WriteMetaData(_audioQuality == AudioQuality.Flac);

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

            JObject officialTrackInfo = await _deezerHttp.HitOfficialApi("track", id);

            return trackInfoJObject == null
                ? null
                : TrackInfo.BuildTrackInfo(trackInfoJObject, officialTrackInfo);
        }

        // the way this works is that if the wanted quality was not found, the next best will be tried and so on
        // until it wraps around to the start and tries the lower quality options, starting at highest lower quality
        // I.E. 320 -> FLAC -> 256 -> 128 -> null
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
            string artist = _trackInfo.TrackTags.ArtistName.SanitseString();
            string type = _albumInfo.AlbumTags.Type.SanitseString();
            string albumTitle = _albumInfo.AlbumTags.Title.SanitseString();
            string trackTitle = _trackInfo.TrackTags.Title.SanitseString();
            string discNumber = _trackInfo.TrackTags.DiskNumber.SanitseString().PadNumber();
            string trackNumber = _trackInfo.TrackTags.TrackNumber.SanitseString().PadNumber();
            
            var downloadPath = Configuration.GetValue<string>("downloadLocation");
            string extension = _audioQuality == AudioQuality.Flac
                ? ".flac"
                : ".mp3";
            string filename = $"{trackNumber} - {trackTitle}{extension}";
            string directoryPath = $@"{artist}\{albumTitle} ({type})\";

            if (int.Parse(discNumber) > 1)
            {
                directoryPath += $@"Disc {discNumber}\";
            }

            string savePath = Path.Combine(downloadPath, directoryPath, filename);

            return savePath;
        }

        private string GetTempTrackPath(string saveLocationDirectory)
        {
            string filename = $"{_trackInfo.TrackTags.Id}.tmp";

            string tempFilePath = Path.Combine(saveLocationDirectory, filename);

            return tempFilePath;
        }

        private async Task<byte[]> GetDecryptedBytes()
        {
            string downloadUrl = EncryptionHandler.GetDownloadUrl(_trackInfo, (int) _audioQuality);

            Console.WriteLine(
                $"Downloading {_trackInfo.TrackTags.ArtistName} - {_trackInfo.TrackTags.Title} | Quality: {Helpers.AudioQualityToOutputString[_audioQuality]}");

            byte[] encryptedBytes = await _deezerHttp.DownloadTrack(downloadUrl);

            if (encryptedBytes == null || encryptedBytes.Length == 0)
            {
                Helpers.RedMessage("Failed to download encrypted track");
                return new byte[0];
            }

            return EncryptionHandler.DecryptTrack(encryptedBytes, _trackInfo.TrackTags.Id);
        }

        private async Task<byte[]> GetAndSaveAlbumArt(string pictureId, string saveDirectory)
        {
            byte[] coverBytes = await _deezerHttp.GetAlbumArt(pictureId);

            if (coverBytes.Length == 0)
            {
                Helpers.RedMessage("Failed to get album cover");
                return new byte[0];
            }

            string coverPath = Path.Combine(saveDirectory, "cover.jpg");

            if (File.Exists(coverPath) && new FileInfo(coverPath).Length == coverBytes.Length)
            {
                return coverBytes;
            }
            
            try
            {
                File.WriteAllBytes(coverPath, coverBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return coverBytes;
        }
    }
}