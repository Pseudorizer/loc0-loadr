using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Konsole;
using loc0Loadr.Enums;
using loc0Loadr.Models;
using Newtonsoft.Json.Linq;
using File = System.IO.File;

namespace loc0Loadr
{
    internal class DeezerDownloader
    {
        private readonly DeezerHttp _deezerHttp;
        private AudioQuality _audioQuality;

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

            var artistResults = new List<bool>();

            foreach (JObject discographyItem in discographyItems.Children<JObject>())
            {
                var albumId = discographyItem["ALB_ID"].Value<string>();

                bool albumDownloadResults = await ProcessAlbum(albumId);

                artistResults.Add(albumDownloadResults);
            }

            int artistDownloadsFailed = artistResults.Count(x => x);

            if (artistDownloadsFailed != artistResults.Count)
            {
                Helpers.GreenMessage("Artist download successful");
            }
            else
            {
                Helpers.RedMessage($"{artistDownloadsFailed}/{artistResults.Count} Downloaded");
            }

            return true;
        }

        public async Task<bool> ProcessAlbum(string id, JObject albumJson)
        {
            JObject officialAlbumInfo = await _deezerHttp.HitOfficialApi("album", id);
            AlbumInfo albumInfo = AlbumInfo.BuildAlbumInfo(albumJson, officialAlbumInfo);

            return await ProcessAlbum(id, albumInfo);
        }

        public async Task<bool> ProcessAlbum(string id, AlbumInfo albumInfo = null)
        {
            if (albumInfo == null)
            {
                albumInfo = await GetAlbumInfo(id);
            }

            if (albumInfo == null)
            {
                Helpers.RedMessage("Failed to get album info");
                return false;
            }

            Console.WriteLine($"\nDownloading {albumInfo.AlbumTags.Title} ({albumInfo.AlbumTags.Type})");

            var results = new List<bool>();
            var tasks = new List<Task>();

            var maxConcurrentDownloads = Configuration.GetValue<int>("maxConcurrentDownloads");

            if (maxConcurrentDownloads <= 0)
            {
                maxConcurrentDownloads = 3;
            }

            var throttler = new SemaphoreSlim(maxConcurrentDownloads);

            List<JObject> songs = albumInfo.Songs.Children<JObject>().ToList();
            int songsCount = songs.Count;

            var progressBar = new ProgressBar(PbStyle.SingleLine, songsCount);
            var complete = 0;
            progressBar.Refresh(complete, $"Tracks processed {complete}/{songsCount}");

            foreach (JObject albumInfoSong in songs)
            {
                await throttler.WaitAsync();

                tasks.Add(
                    Task.Run(async () =>
                    {
                        try
                        {
                            var trackId = albumInfoSong["SNG_ID"].Value<string>();

                            bool downloadResult = await ProcessTrack(trackId, albumInfo);

                            complete++;
                            progressBar.Refresh(complete, $"Tracks processed {complete}/{songsCount}");

                            results.Add(downloadResult);

                        }
                        finally
                        {
                            // ReSharper disable once AccessToDisposedClosure
                            throttler.Release();
                        }
                    }));
            }

            await Task.WhenAll(tasks);

            throttler.Dispose();

            int downloadsSucceed = results.Count(x => x);

            if (downloadsSucceed == results.Count)
            {
                Helpers.GreenMessage("\nAlbum downloaded successfully");
            }
            else
            {
                Helpers.RedMessage($"\n{downloadsSucceed}/{results.Count} Downloaded");
            }

            return downloadsSucceed == results.Count;
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

            return AlbumInfo.BuildAlbumInfo(albumJson, officialAlbumJson);
        }

        public async Task<bool> DownloadPlaylist(string id)
        {

            return true;
        }

        public async Task<bool> ProcessTrack(string id, JObject trackJson)
        {
            JObject officialTrackInfo = await _deezerHttp.HitOfficialApi("track", id);
            TrackInfo trackInfo = TrackInfo.BuildTrackInfo(trackJson, officialTrackInfo, true);

            JObject lyricsJson = await _deezerHttp.HitUnofficialApi("song.getLyrics", new JObject
            {
                ["SNG_ID"] = trackInfo.TrackTags.Id
            });

            trackInfo.Lyrics = lyricsJson?["results"].ToObject<Lyrics>();

            return await ProcessTrack(id, trackInfo: trackInfo);
        }

        public async Task<bool> ProcessTrack(string id, AlbumInfo albumInfo = null, TrackInfo trackInfo = null)
        {
            var trackProgress = new ProgressBar(100, 100);

            if (trackInfo == null)
            {
                trackInfo = await GetTrackInfo(id);

                if (trackInfo == null)
                {
                    trackProgress.Refresh(0, $"{id} | Failed to get track info");
                    return false;
                }
            }

            if (albumInfo == null)
            {
                string albumId = trackInfo.TrackTags.AlbumId;

                albumInfo = await GetAlbumInfo(albumId);

                if (albumInfo == null)
                {
                    trackProgress.Refresh(0, $"{id} | Failed to get album info");
                    return false; // this shouldn't return, it shouldn't care, force album info to be null at some point and see what needs fixing
                }
            }

            string trackProgressTitle =
                $"\nDownloading {albumInfo.AlbumTags.Artists[0].Name} - {trackInfo.TrackTags.Title} | Quality: {Helpers.AudioQualityToOutputString[_audioQuality]}";

            trackProgress.Next($"{trackProgressTitle} | Checking for available qualities");
            if (!UpdateAudioQualityToAvailable(trackInfo))
            {
                trackProgress.Refresh(0, $"{trackProgressTitle} | Failed to find valid quality");
                return false;
            }

            trackProgress.Next($"{trackProgressTitle} | Getting save location");
            string saveLocation = BuildSaveLocation(trackInfo, albumInfo);
            string saveLocationDirectory = Path.GetDirectoryName(saveLocation);
            string tempTrackPath = Helpers.GetTempTrackPath(saveLocationDirectory, trackInfo.TrackTags.Id);

            if (File.Exists(saveLocation))
            {
                trackProgress.Refresh(100, $"{trackProgressTitle} | file already exists");
                return true;
            }

            byte[] decryptedBytes = await GetDecryptedBytes(trackInfo, trackProgress, trackProgressTitle);

            trackProgress.Next($"{trackProgressTitle} | Writing to disk");
            if (!Helpers.WriteTrackBytes(decryptedBytes, tempTrackPath))
            {
                trackProgress.Refresh(0, $"{trackProgressTitle} | Failed to write file to disk");
                return false;
            }

            byte[] albumCover = await GetAndSaveAlbumArt(albumInfo.AlbumTags.PictureId, saveLocationDirectory);

            trackProgress.Next($"{trackProgressTitle} | Writing metadata");
            var metadataWriter = new MetadataWriter(trackInfo, albumInfo, tempTrackPath, albumCover);
            if (!metadataWriter.WriteMetaData(_audioQuality == AudioQuality.Flac))
            {
                trackProgress.Refresh(0, $"{trackProgressTitle} | Failed to write tags");
            }

            File.Move(tempTrackPath, saveLocation);

            string tempLyricFilePath = Path.Combine(saveLocationDirectory,
                Path.GetFileNameWithoutExtension(tempTrackPath) + ".lrc");

            if (File.Exists(tempLyricFilePath))
            {
                string properLyricFilePath =
                    Path.Combine(saveLocationDirectory, Path.GetFileNameWithoutExtension(saveLocation) + ".lrc");
                
                File.Move(tempLyricFilePath, properLyricFilePath);
            }

            trackProgress.Next($"{trackProgressTitle} | Complete");
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
        private bool UpdateAudioQualityToAvailable(TrackInfo trackInfo)
        {
            var enumIds = new List<int> {1, 5, 3, 9};

            int startIndex = enumIds.IndexOf((int) _audioQuality);

            if (_audioQuality == AudioQuality.Flac)
            {
                enumIds.Reverse();
                startIndex = 0;
            }

            if (ValidAudioQualityFound(startIndex, enumIds.Count))
            {
                return true;
            }

            if (_audioQuality != AudioQuality.Flac)
            {
                enumIds.RemoveRange(startIndex, 4 - startIndex);
                enumIds.Reverse();
            }

            return ValidAudioQualityFound(0, startIndex);

            bool ValidAudioQualityFound(int startingIndex, int endIndex)
            {
                for (int index = startingIndex; index < endIndex; index++)
                {
                    int enumId = enumIds[index];
                    var tempAudioQuality = (AudioQuality) enumId;
                    bool qualityIsAvailable = Helpers.CheckIfQualityIsAvailable(tempAudioQuality, trackInfo);

                    if (qualityIsAvailable)
                    {
                        _audioQuality = tempAudioQuality;
                        return true;
                    }
                }

                return false;
            }
        }

        private string BuildSaveLocation(TrackInfo trackInfo, AlbumInfo albumInfo)
        {
            string artist = albumInfo.AlbumTags.Artists[0].Name.SanitseString();
            string type = albumInfo.AlbumTags.Type.SanitseString();
            string albumTitle = albumInfo.AlbumTags.Title.SanitseString();
            string trackTitle = trackInfo.TrackTags.Title.SanitseString();
            string discNumber = trackInfo.TrackTags.DiscNumber.SanitseString();
            string trackNumber = trackInfo.TrackTags.TrackNumber.SanitseString().PadNumber();

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

        private async Task<byte[]> GetDecryptedBytes(TrackInfo trackInfo, IProgressBar trackProgress, string progressTitle)
        {
            trackProgress.Next($"{progressTitle} | Grabbing download URL");

            string downloadUrl = EncryptionHandler.GetDownloadUrl(trackInfo, (int) _audioQuality);

            byte[] encryptedBytes = await _deezerHttp.DownloadTrack(downloadUrl, trackProgress, progressTitle);

            if (encryptedBytes == null || encryptedBytes.Length == 0)
            {
                Helpers.RedMessage("Failed to download encrypted track");
                return new byte[0];
            }

            trackProgress.Next($"{progressTitle} | Decrypting track");
            return await EncryptionHandler.DecryptTrack(encryptedBytes, trackInfo.TrackTags.Id);
        }

        private async Task<byte[]> GetAndSaveAlbumArt(string pictureId, string saveDirectory)
        {
            string coverPath = Path.Combine(saveDirectory, "cover.jpg");

            if (File.Exists(coverPath))
            {
                while (true)
                {
                    try
                    {
                        return File.ReadAllBytes(coverPath);
                    }
                    catch (Exception)
                    {
                        await Task.Delay(10); // i have no idea if this will actually work
                    }
                }
            }

            byte[] coverBytes = await _deezerHttp.GetAlbumArt(pictureId);

            if (coverBytes.Length == 0)
            {
                Helpers.RedMessage("Failed to get album cover");
                return new byte[0];
            }

            if (File.Exists(coverPath) && new FileInfo(coverPath).Length == coverBytes.Length)
            {
                return coverBytes;
            }

            try
            {
                using (FileStream coverFileStream = File.Create(coverPath))
                {
                    coverFileStream.Write(coverBytes);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return coverBytes;
        }
    }
}