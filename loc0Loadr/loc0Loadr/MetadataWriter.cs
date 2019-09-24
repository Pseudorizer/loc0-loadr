using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using FlacLibSharp;
using loc0Loadr.Models;
using TagLib;
using TagLib.Id3v2;
using Metadata = loc0Loadr.Models.Metadata;
using Picture = TagLib.Picture;
using PictureType = TagLib.PictureType;
using Tag = TagLib.Id3v2.Tag;

namespace loc0Loadr
{
    internal class MetadataWriter
    {
        private readonly Metadata _metadata;
        private readonly string _filepath;
        private readonly byte[] _coverBytes;

        public MetadataWriter(Metadata metadata, string filepath, byte[] coverBytes)
        {
            _metadata = metadata;
            _filepath = filepath;
            _coverBytes = coverBytes;
        }
        
        public bool WriteMetaData()
        {
            string extension = Path.GetExtension(_filepath);

            Console.WriteLine("Writing tags");
            
            return extension == ".flac"
                ? WriteFlacMetadata()
                : WriteMp3Metadata();
        }

        private bool WriteFlacMetadata()
        {
            using (var file = new FlacFile(_filepath))
            {
                var comments = new VorbisComment();
                
                if (_coverBytes.Length > 0)
                {
                    var coverArt = new FlacLibSharp.Picture()
                    {
                        Description = "Cover",
                        ColorDepth = 8,
                        Data = _coverBytes,
                        Height = 1400,
                        Width = 1400,
                        MIMEType = MediaTypeNames.Image.Jpeg,
                        PictureType = FlacLibSharp.PictureType.CoverFront
                    };
                    
                    file.Metadata.Add(coverArt);
                }

                comments.Album = new VorbisCommentValues(_metadata.AlbumTitle ?? "");
                comments.Title = new VorbisCommentValues(_metadata.Title ?? "");
                comments.Genre = new VorbisCommentValues(_metadata.Genres.Select(x => x.Name));
                comments.Artist = new VorbisCommentValues(_metadata.Artists.Select(x => x.Name));
                comments.Date = new VorbisCommentValues(_metadata.Year ?? "");
                comments["Track"] = new VorbisCommentValues($"{_metadata.TrackNumber}/{_metadata.TotalTrackNumber}");
                comments["Disc"] = new VorbisCommentValues($"{_metadata.DiskNumber}/{_metadata.TotalDisks}");
                comments["Copyright"] = new VorbisCommentValues(_metadata.Copyright ?? "");
                comments["BPM"] = new VorbisCommentValues(_metadata.Bpm ?? "");
                comments["Media"] = new VorbisCommentValues("Digital Media");
                comments["Length"] = new VorbisCommentValues(_metadata.Duration ?? "");
                comments["ISRC"] = new VorbisCommentValues(_metadata.Isrc ?? "");
                comments["Barcode"] = new VorbisCommentValues(_metadata.Upc ?? "");
                comments["Label"] = new VorbisCommentValues(_metadata.Label ?? "");
                comments["Explicit"] = new VorbisCommentValues(_metadata.HasExplicitLyrics ?? "0");
                comments["Album Artist"] = new VorbisCommentValues(_metadata.AlbumArtist ?? "");
                comments["REPLAYGAIN_TRACK_GAIN"] = new VorbisCommentValues(_metadata.Gain ?? "");

                if (_metadata.Lyrics != null)
                {
                    comments["Lyrics"] = new VorbisCommentValues(_metadata.Lyrics.UnSyncedLyrics ?? "");

                    WriteLyricsFile();
                }
                
                Helpers.AddTagIfNotNull(comments, "Composers", _metadata.Contributors.Composers);
                Helpers.AddTagIfNotNull(comments, "Publishers", _metadata.Contributors.Publishers);
                Helpers.AddTagIfNotNull(comments, "Producers", _metadata.Contributors.Producers);
                Helpers.AddTagIfNotNull(comments, "Engineers", _metadata.Contributors.Engineers);
                Helpers.AddTagIfNotNull(comments, "Writers", _metadata.Contributors.Writers);
                Helpers.AddTagIfNotNull(comments, "Mixers", _metadata.Contributors.Mixers);
                Helpers.AddTagIfNotNull(comments, "Authors", _metadata.Contributors.Authors);

                file.Metadata.Add(comments);

                try
                {
                    file.Save();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return false;
                }
            }

            return true;
        }

        private bool WriteMp3Metadata()
        {
            using (TagLib.File file = TagLib.File.Create(_filepath))
            {
                var tags = (Tag) file.GetTag(TagTypes.Id3v2);

                if (_coverBytes.Length > 0)
                {
                    var byteVector = new ByteVector(_coverBytes);
                    var coverPicture = new Picture()
                    {
                        Description = "Cover",
                        Data = byteVector,
                        MimeType = MediaTypeNames.Image.Jpeg,
                        Type = PictureType.FrontCover
                    };
                    var attachedPictureFrame = new AttachedPictureFrame(coverPicture);

                    tags.Pictures = new IPicture[] {attachedPictureFrame};
                }
                
                tags.Album = _metadata.AlbumTitle;
                tags.Title = _metadata.Title;
                tags.Genres = _metadata.Genres.Select(x => x.Name).ToArray();
                tags.Performers = _metadata.Artists.Select(x => x.Name).ToArray();
                tags.AlbumArtists = new []{_metadata.AlbumArtist};

                if (_metadata.TrackNumber != null)
                {
                    tags.Track = uint.Parse(_metadata.TrackNumber);
                }

                if (_metadata.TotalTrackNumber != null)
                {
                    tags.TrackCount = uint.Parse(_metadata.TotalTrackNumber);
                }

                if (_metadata.DiskNumber != null)
                {
                    tags.Disc = uint.Parse(_metadata.DiskNumber);
                }
                
                if (_metadata.TotalDisks != null)
                {
                    tags.DiscCount = uint.Parse(_metadata.TotalDisks);
                }
                
                if (_metadata.Bpm != null)
                {
                    tags.BeatsPerMinute = uint.Parse(_metadata.Bpm);
                }
                
                tags.Copyright = _metadata.Copyright;
                tags.Composers = _metadata.Contributors.Composers;

                string year = _metadata.Year;
                year = year.Split("-")[0];
            
                tags.Year = uint.Parse(year);
                
                if (_metadata.Lyrics != null)
                {
                    tags.Lyrics = _metadata.Lyrics.UnSyncedLyrics;
                    WriteLyricsFile();
                }
                
                tags.AddFrame(Helpers.BuildTextInformationFrame("ISRC", _metadata.Isrc));
                tags.AddFrame(Helpers.BuildTextInformationFrame("MEDIA", "Digital Media"));
                tags.AddFrame(Helpers.BuildTextInformationFrame("Label", _metadata.Label));
                tags.AddFrame(Helpers.BuildTextInformationFrame("Length", _metadata.Duration));
                tags.AddFrame(Helpers.BuildTextInformationFrame("Barcode", _metadata.Upc));
                tags.AddFrame(Helpers.BuildTextInformationFrame("Explicit", _metadata.HasExplicitLyrics));
                tags.AddFrame(Helpers.BuildTextInformationFrame("REPLAYGAIN_TRACK_GAIN", _metadata.Gain));
                tags.AddFrame(Helpers.BuildTextInformationFrame("Date", _metadata.Year));
                tags.AddFrame(Helpers.BuildTextInformationFrame("Publishers", _metadata.Contributors.Publishers));
                tags.AddFrame(Helpers.BuildTextInformationFrame("Producers", _metadata.Contributors.Producers));
                tags.AddFrame(Helpers.BuildTextInformationFrame("Engineers", _metadata.Contributors.Engineers));
                tags.AddFrame(Helpers.BuildTextInformationFrame("Writers", _metadata.Contributors.Writers));
                tags.AddFrame(Helpers.BuildTextInformationFrame("Mixers", _metadata.Contributors.Mixers));
                tags.AddFrame(Helpers.BuildTextInformationFrame("Authors", _metadata.Contributors.Authors));

                try
                {
                    file.Save();
                }
                catch (IOException ex)
                {
                    Console.WriteLine(ex.Message);
                    return false;
                }
            }

            return true;
        }

        private void WriteLyricsFile()
        {
            var syncedLyrics = _metadata.Lyrics.SyncedLyrics
                .Where(y => y != null)
                .Where(x => !string.IsNullOrWhiteSpace(x.Timestamp) || !string.IsNullOrWhiteSpace(x.Line));

            string lyricsFormatted = string.Join("\r\n", syncedLyrics.Select(x => x.Timestamp + x.Line));

            string lyricFilePath = Path.Combine(
                Path.GetDirectoryName(_filepath), Path.GetFileNameWithoutExtension(_filepath) + ".lrc");

            try
            {
                System.IO.File.Create(lyricFilePath).Close();
                System.IO.File.WriteAllText(lyricFilePath, lyricsFormatted);
            }
            catch (IOException ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
    
        internal class NewMetadataWriter
    {
        private readonly TrackInfo _trackInfo;
        private readonly AlbumInfo _albumInfo;
        private readonly string _filepath;
        private readonly byte[] _coverBytes;

        public NewMetadataWriter(TrackInfo trackInfo, AlbumInfo albumInfo, string filepath, byte[] coverBytes)
        {
            _trackInfo = trackInfo;
            _albumInfo = albumInfo;
            _filepath = filepath;
            _coverBytes = coverBytes;
        }
        
        public bool WriteMetaData(bool isFlac)
        {
            Console.WriteLine("Writing tags");
            
            return isFlac
                ? WriteFlacMetadata()
                : WriteMp3Metadata();
        }

        private bool WriteFlacMetadata()
        {
            using (var file = new FlacFile(_filepath))
            {
                var comments = new VorbisComment();
                
                if (_coverBytes.Length > 0)
                {
                    var coverArt = new FlacLibSharp.Picture()
                    {
                        Description = "Cover",
                        ColorDepth = 8,
                        Data = _coverBytes,
                        Height = 1400,
                        Width = 1400,
                        MIMEType = MediaTypeNames.Image.Jpeg,
                        PictureType = FlacLibSharp.PictureType.CoverFront
                    };
                    
                    file.Metadata.Add(coverArt);
                }

                comments.Album = new VorbisCommentValues(_albumInfo.AlbumTags.Title ?? "");
                comments.Title = new VorbisCommentValues(_trackInfo.TrackTags.Title ?? "");
                comments.Genre = new VorbisCommentValues(_albumInfo.AlbumTags.Genres.GenreData.Select(x => x.Name));
                comments.Artist = new VorbisCommentValues(_trackInfo.TrackTags.Artists.Select(x => x.Name));
                comments.Date = new VorbisCommentValues(_albumInfo.AlbumTags.ReleaseDate ?? "");
                comments["Track"] = new VorbisCommentValues($"{_trackInfo.TrackTags.TrackNumber}/{_albumInfo.AlbumTags.NumberOfTracks}");
                comments["Disc"] = new VorbisCommentValues($"{_trackInfo.TrackTags.DiskNumber}/{_albumInfo.AlbumTags.NumberOfDisks}");
                comments["Copyright"] = new VorbisCommentValues(_albumInfo.AlbumTags.Copyright ?? "");
                comments["BPM"] = new VorbisCommentValues(_trackInfo.TrackTags.Bpm ?? "");
                comments["Media"] = new VorbisCommentValues("Digital Media");
                comments["Length"] = new VorbisCommentValues(_trackInfo.TrackTags.Duration ?? "");
                comments["ISRC"] = new VorbisCommentValues(_trackInfo.TrackTags.Isrc ?? "");
                comments["Barcode"] = new VorbisCommentValues(_albumInfo.AlbumTags.Barcode ?? "");
                comments["Label"] = new VorbisCommentValues(_albumInfo.AlbumTags.Label ?? "");
                comments["Explicit"] = new VorbisCommentValues(_trackInfo.TrackTags.ExplicitLyrics ?? "0");
                comments["Album Artist"] = new VorbisCommentValues(_trackInfo.TrackTags.ArtistName ?? "");
                comments["REPLAYGAIN_TRACK_GAIN"] = new VorbisCommentValues(_trackInfo.TrackTags.Gain ?? "");

                if (_trackInfo.Lyrics != null)
                {
                    comments["Lyrics"] = new VorbisCommentValues(_trackInfo.Lyrics.UnSyncedLyrics ?? "");

                    WriteLyricsFile();
                }
                
                Helpers.AddTagIfNotNull(comments, "Composers", _trackInfo.TrackTags.Contributors.Composers);
                Helpers.AddTagIfNotNull(comments, "Publishers", _trackInfo.TrackTags.Contributors.Publishers);
                Helpers.AddTagIfNotNull(comments, "Producers", _trackInfo.TrackTags.Contributors.Producers);
                Helpers.AddTagIfNotNull(comments, "Engineers", _trackInfo.TrackTags.Contributors.Engineers);
                Helpers.AddTagIfNotNull(comments, "Writers", _trackInfo.TrackTags.Contributors.Writers);
                Helpers.AddTagIfNotNull(comments, "Mixers", _trackInfo.TrackTags.Contributors.Mixers);
                Helpers.AddTagIfNotNull(comments, "Authors", _trackInfo.TrackTags.Contributors.Authors);

                file.Metadata.Add(comments);

                try
                {
                    file.Save();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return false;
                }
            }

            return true;
        }

        private bool WriteMp3Metadata()
        {
            using (TagLib.File file = TagLib.File.Create(_filepath))
            {
                var tags = (Tag) file.GetTag(TagTypes.Id3v2);

                if (_coverBytes.Length > 0)
                {
                    var byteVector = new ByteVector(_coverBytes);
                    var coverPicture = new Picture()
                    {
                        Description = "Cover",
                        Data = byteVector,
                        MimeType = MediaTypeNames.Image.Jpeg,
                        Type = PictureType.FrontCover
                    };
                    var attachedPictureFrame = new AttachedPictureFrame(coverPicture);

                    tags.Pictures = new IPicture[] {attachedPictureFrame};
                }
                
/*                tags.Album = _metadata.AlbumTitle;
                tags.Title = _metadata.Title;
                tags.Genres = _metadata.Genres.Select(x => x.Name).ToArray();
                tags.Performers = _metadata.Artists.Select(x => x.Name).ToArray();
                tags.AlbumArtists = new []{_metadata.AlbumArtist};

                if (_metadata.TrackNumber != null)
                {
                    tags.Track = uint.Parse(_metadata.TrackNumber);
                }

                if (_metadata.TotalTrackNumber != null)
                {
                    tags.TrackCount = uint.Parse(_metadata.TotalTrackNumber);
                }

                if (_metadata.DiskNumber != null)
                {
                    tags.Disc = uint.Parse(_metadata.DiskNumber);
                }
                
                if (_metadata.TotalDisks != null)
                {
                    tags.DiscCount = uint.Parse(_metadata.TotalDisks);
                }
                
                if (_metadata.Bpm != null)
                {
                    tags.BeatsPerMinute = uint.Parse(_metadata.Bpm);
                }
                
                tags.Copyright = _metadata.Copyright;
                tags.Composers = _metadata.Contributors.Composers;

                string year = _metadata.Year;
                year = year.Split("-")[0];
            
                tags.Year = uint.Parse(year);
                
                if (_metadata.Lyrics != null)
                {
                    tags.Lyrics = _metadata.Lyrics.UnSyncedLyrics;
                    WriteLyricsFile();
                }
                
                tags.AddFrame(Helpers.BuildTextInformationFrame("ISRC", _metadata.Isrc));
                tags.AddFrame(Helpers.BuildTextInformationFrame("MEDIA", "Digital Media"));
                tags.AddFrame(Helpers.BuildTextInformationFrame("Label", _metadata.Label));
                tags.AddFrame(Helpers.BuildTextInformationFrame("Length", _metadata.Duration));
                tags.AddFrame(Helpers.BuildTextInformationFrame("Barcode", _metadata.Upc));
                tags.AddFrame(Helpers.BuildTextInformationFrame("Explicit", _metadata.HasExplicitLyrics));
                tags.AddFrame(Helpers.BuildTextInformationFrame("REPLAYGAIN_TRACK_GAIN", _metadata.Gain));
                tags.AddFrame(Helpers.BuildTextInformationFrame("Date", _metadata.Year));
                tags.AddFrame(Helpers.BuildTextInformationFrame("Publishers", _metadata.Contributors.Publishers));
                tags.AddFrame(Helpers.BuildTextInformationFrame("Producers", _metadata.Contributors.Producers));
                tags.AddFrame(Helpers.BuildTextInformationFrame("Engineers", _metadata.Contributors.Engineers));
                tags.AddFrame(Helpers.BuildTextInformationFrame("Writers", _metadata.Contributors.Writers));
                tags.AddFrame(Helpers.BuildTextInformationFrame("Mixers", _metadata.Contributors.Mixers));
                tags.AddFrame(Helpers.BuildTextInformationFrame("Authors", _metadata.Contributors.Authors));*/

                try
                {
                    file.Save();
                }
                catch (IOException ex)
                {
                    Console.WriteLine(ex.Message);
                    return false;
                }
            }

            return true;
        }

        private void WriteLyricsFile()
        {
            IEnumerable<SyncedLyrics> syncedLyrics = _trackInfo.Lyrics.SyncedLyrics
                .Where(y => y != null)
                .Where(x => !string.IsNullOrWhiteSpace(x.Timestamp) || !string.IsNullOrWhiteSpace(x.Line));

            string lyricsFormatted = string.Join("\r\n", syncedLyrics.Select(x => x.Timestamp + x.Line));

            string lyricFilePath = Path.Combine(
                Path.GetDirectoryName(_filepath), Path.GetFileNameWithoutExtension(_filepath) + ".lrc");

            try
            {
                System.IO.File.Create(lyricFilePath).Close();
                System.IO.File.WriteAllText(lyricFilePath, lyricsFormatted);
            }
            catch (IOException ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}