using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mime;
using FlacLibSharp;
using loc0Loadr.Models;
using TagLib;
using TagLib.Id3v2;
using File = System.IO.File;
using Picture = TagLib.Picture;
using PictureType = TagLib.PictureType;
using Tag = TagLib.Id3v2.Tag;

namespace loc0Loadr.Metadata
{
    internal class MetadataWriter
    {
        private readonly TrackInfo _trackInfo;
        private readonly AlbumInfo _albumInfo;
        private readonly string _filepath;
        private readonly byte[] _coverBytes;

        public MetadataWriter(TrackInfo trackInfo, AlbumInfo albumInfo, string filepath, byte[] coverBytes)
        {
            _trackInfo = trackInfo;
            _albumInfo = albumInfo;
            _filepath = filepath;
            _coverBytes = coverBytes;
        }
        
        public bool WriteMetaData(bool isFlac)
        {
            return isFlac
                ? WriteFlacMetadata()
                : WriteMp3Metadata();
        }
        
        // just in case i find a way to get the album cover working in taglib
        /*private bool WriteFlacMetadata()
        {
            using (TagLib.File file = TagLib.File.Create(_filepath, "taglib/flac", ReadStyle.Average))
            {
                var tags = (TagLib.Ogg.XiphComment) file.GetTag(TagTypes.Xiph);

                if (_trackInfo.TrackTags != null)
                {
                    tags.Title = _trackInfo.TrackTags.Title;
                    tags.Performers = _trackInfo.TrackTags.Artists.Select(x => x.Name).ToArray();
                    tags.Composers = _trackInfo.TrackTags.Contributors.Composers;
                    tags.SetField("LENGTH", _trackInfo.TrackTags.Length);
                    tags.SetField("ISRC", _trackInfo.TrackTags.Isrc);
                    tags.SetField("EXPLICIT", _trackInfo.TrackTags.ExplicitLyrics ?? "0");
                    tags.SetField("REPLAYGAIN_TRACK_GAIN", _trackInfo.TrackTags.Gain);
                    tags.SetField("PRODUCER", _trackInfo.TrackTags.Contributors.Producers);
                    tags.SetField("ENGINEER", _trackInfo.TrackTags.Contributors.Engineers);
                    tags.SetField("MIXER", _trackInfo.TrackTags.Contributors.Mixers);
                    tags.SetField("WRITER", _trackInfo.TrackTags.Contributors.Writers);
                    tags.SetField("AUTHOR", _trackInfo.TrackTags.Contributors.Authors);
                    tags.SetField("PUBLISHER", _trackInfo.TrackTags.Contributors.Publishers);
                    

                    if (_trackInfo.TrackTags.TrackNumber != null &&
                        uint.TryParse(_trackInfo.TrackTags.TrackNumber, out uint trackNumber))
                    {
                        tags.Track = trackNumber;
                    }
                    
                    if (_trackInfo.TrackTags.DiscNumber != null &&
                        uint.TryParse(_trackInfo.TrackTags.DiscNumber, out uint discNumber))
                    {
                        tags.Disc = discNumber;
                    }
                    
                    if (_trackInfo.TrackTags.Bpm != null &&
                        uint.TryParse(_trackInfo.TrackTags.Bpm, out uint bpm))
                    {
                        tags.BeatsPerMinute = bpm;
                    }
                }

                if (_albumInfo.AlbumTags != null)
                {
                    tags.Album = _albumInfo.AlbumTags.Title;
                    tags.AlbumArtists = _albumInfo.AlbumTags.Artists.Select(x => x.Name).ToArray();
                    tags.Genres = _albumInfo.AlbumTags.Genres.GenreData.Select(x => x.Name).ToArray();
                
                    string year = _albumInfo.AlbumTags.ReleaseDate;

                    if (!string.IsNullOrWhiteSpace(year))
                    {
                        string[] yearSplit = year.Split("-");

                        if (yearSplit[0].Length == 4 && uint.TryParse(yearSplit[0], out uint yearParsed))
                        {
                            tags.Year = yearParsed;
                        }
                    }

                    tags.Copyright = _albumInfo.AlbumTags.Copyright;
                    tags.SetField("MEDIA", "Digital Media");
                    tags.SetField("ORIGINALDATE", _albumInfo.AlbumTags.ReleaseDate);
                    tags.SetField("UPC", _albumInfo.AlbumTags.Upc);
                    tags.SetField("LABEL", _albumInfo.AlbumTags.Label);

                    if (_albumInfo.AlbumTags.NumberOfTracks != null &&
                        uint.TryParse(_albumInfo.AlbumTags.NumberOfTracks, out uint numberOfTracks))
                    {
                        tags.TrackCount = numberOfTracks;
                    }

                    if (_albumInfo.AlbumTags.NumberOfDiscs != null &&
                        uint.TryParse(_albumInfo.AlbumTags.NumberOfDiscs, out uint numberOfDiscs))
                    {
                        tags.DiscCount = numberOfDiscs;
                    }
                }
                
                if (_trackInfo.Lyrics != null)
                {
                    tags.Lyrics = _trackInfo.Lyrics.UnSyncedLyrics;
                    WriteLyricsFile();
                }

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

            // this takes 800-900ms... why can't you just work taglib
            using (var file = new FlacFile(_filepath))
            {
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
        }*/

        private bool WriteFlacMetadata()
        {
            using (var file = new FlacFile(_filepath))
            {
                var comments = new VorbisComment();
                
                if (_coverBytes.Length > 0)
                {
                    var coverArt = new FlacLibSharp.Picture
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
                
                comments["Media"] = new VorbisCommentValues("Digital Media");

                if (_albumInfo?.AlbumTags != null)
                {
                    comments.Album = new VorbisCommentValues(_albumInfo.AlbumTags.Title ?? "");

                    if (_albumInfo.AlbumTags?.Genres?.GenreData != null)
                    {
                        comments.Genre = new VorbisCommentValues(_albumInfo.AlbumTags.Genres.GenreData.Select(x => x.Name));
                    }

                    string year = _albumInfo.AlbumTags.ReleaseDate ?? "";

                    if (!string.IsNullOrWhiteSpace(year))
                    {
                        var yearSplit = year.Split("-");

                        if (yearSplit[0].Length == 4)
                        {
                            year = yearSplit[0];
                        }
                    }

                    if (_albumInfo.AlbumTags.Type == "Compilation" || _albumInfo.AlbumTags.Type == "Playlist")
                    {
                        comments["COMPILATION"] = new VorbisCommentValues("1");
                    }
                    
                    comments.Date = new VorbisCommentValues(year ?? "");
                    comments["ORIGINALDATE"] = new VorbisCommentValues(_albumInfo.AlbumTags.ReleaseDate ?? "");
                    comments["TRACKTOTAL"] = new VorbisCommentValues(_albumInfo.AlbumTags.NumberOfTracks ?? "");
                    comments["DISCTOTAL"] = new VorbisCommentValues(_albumInfo.AlbumTags.NumberOfDiscs ?? "");
                    comments["COPYRIGHT"] = new VorbisCommentValues(_albumInfo.AlbumTags.Copyright ?? "");
                    comments["UPC"] = new VorbisCommentValues(_albumInfo.AlbumTags.Upc ?? "");
                    comments["LABEL"] = new VorbisCommentValues(_albumInfo.AlbumTags.Label ?? "");
                    comments["ALBUMARTIST"] = new VorbisCommentValues(_albumInfo.AlbumTags.Artists.Select(x => x.Name));
                }

                if (_trackInfo?.TrackTags != null)
                {
                    comments.Title = new VorbisCommentValues(_trackInfo.TrackTags.Title ?? "");
                    comments.Artist = new VorbisCommentValues(_trackInfo.TrackTags.Artists.Select(x => x.Name));
                    comments["DISCNUMBER"] = new VorbisCommentValues(_trackInfo.TrackTags.DiscNumber ?? "");
                    comments["TRACKNUMBER"] = new VorbisCommentValues(_trackInfo.TrackTags.TrackNumber ?? "");
                    comments["BPM"] = new VorbisCommentValues(_trackInfo.TrackTags.Bpm ?? "");
                    comments["LENGTH"] = new VorbisCommentValues(_trackInfo.TrackTags.Length ?? "");
                    comments["ISRC"] = new VorbisCommentValues(_trackInfo.TrackTags.Isrc ?? "");
                    comments["EXPLICIT"] = new VorbisCommentValues(_trackInfo.TrackTags.ExplicitLyrics ?? "0");
                    comments["REPLAYGAIN_TRACK_GAIN"] = new VorbisCommentValues(_trackInfo.TrackTags.Gain ?? ""); 
                    
                    MetadataHelpers.AddTagIfNotNull(comments, "COMPOSER", _trackInfo.TrackTags.Contributors.Composers);
                    MetadataHelpers.AddTagIfNotNull(comments, "PUBLISHER", _trackInfo.TrackTags.Contributors.Publishers);
                    MetadataHelpers.AddTagIfNotNull(comments, "PRODUCER", _trackInfo.TrackTags.Contributors.Producers);
                    MetadataHelpers.AddTagIfNotNull(comments, "ENGINEER", _trackInfo.TrackTags.Contributors.Engineers);
                    MetadataHelpers.AddTagIfNotNull(comments, "WRITER", _trackInfo.TrackTags.Contributors.Writers);
                    MetadataHelpers.AddTagIfNotNull(comments, "MIXER", _trackInfo.TrackTags.Contributors.Mixers);
                    MetadataHelpers.AddTagIfNotNull(comments, "AUTHOR", _trackInfo.TrackTags.Contributors.Authors);
                }

                if (_trackInfo?.Lyrics != null)
                {
                    comments["Lyrics"] = new VorbisCommentValues(_trackInfo.Lyrics.UnSyncedLyrics ?? "");
                    WriteLyricsFile();
                }

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
            using (TagLib.File file = TagLib.File.Create(_filepath, "taglib/mp3", ReadStyle.Average))
            {
                var tags = (Tag) file.GetTag(TagTypes.Id3v2);

                if (_coverBytes.Length > 0)
                {
                    var byteVector = new ByteVector(_coverBytes);
                    var coverPicture = new Picture
                    {
                        Description = "Cover",
                        Data = byteVector,
                        MimeType = MediaTypeNames.Image.Jpeg,
                        Type = PictureType.FrontCover
                    };
                    var attachedPictureFrame = new AttachedPictureFrame(coverPicture);

                    tags.Pictures = new IPicture[] {attachedPictureFrame};
                }
                
                tags.AddFrame(MetadataHelpers.BuildTextInformationFrame("TMED", "Digital Media"));

                if (_trackInfo?.TrackTags != null)
                {
                    tags.Title = _trackInfo.TrackTags.Title;
                    tags.Performers = _trackInfo.TrackTags.Artists.Select(x => x.Name).ToArray();
                    tags.AlbumArtists = _albumInfo.AlbumTags.Artists.Select(x => x.Name).ToArray(); // i don't like just using ART_NAME as the only album artist
                    tags.Composers = _trackInfo.TrackTags.Contributors.Composers;
                    
                    if (_trackInfo.TrackTags.TrackNumber != null)
                    {
                        tags.Track = uint.Parse(_trackInfo.TrackTags.TrackNumber);
                    }
                    
                    if (_trackInfo.TrackTags.DiscNumber != null)
                    {
                        tags.Disc = uint.Parse(_trackInfo.TrackTags.DiscNumber);
                    }
                    
                    if (_trackInfo.TrackTags.Bpm != null)
                    {
                        string bpm = _trackInfo.TrackTags.Bpm;

                        if (double.TryParse(bpm, out double bpmParsed))
                        {
                            bpmParsed = Math.Round(bpmParsed);
                            bpm = bpmParsed.ToString(CultureInfo.InvariantCulture);
                        }
                    
                        tags.BeatsPerMinute = uint.Parse(bpm);
                    }
                    
                    tags.AddFrame(MetadataHelpers.BuildTextInformationFrame("TSRC", _trackInfo.TrackTags.Isrc));
                    tags.AddFrame(MetadataHelpers.BuildTextInformationFrame("TPUB", _trackInfo.TrackTags.Contributors.Publishers));
                    tags.AddFrame(MetadataHelpers.BuildTextInformationFrame("TLEN", _trackInfo.TrackTags.Length));
                    tags.AddFrame(MetadataHelpers.BuildUserTextInformationFrame("EXPLICIT", _trackInfo.TrackTags.ExplicitLyrics));
                    tags.AddFrame(MetadataHelpers.BuildUserTextInformationFrame("REPLAYGAIN_TRACK_GAIN", _trackInfo.TrackTags.Gain));
                    tags.AddFrame(MetadataHelpers.BuildUserTextInformationFrame("WRITERS", _trackInfo.TrackTags.Contributors.Writers));
                    tags.AddFrame(MetadataHelpers.BuildUserTextInformationFrame("AUTHORS", _trackInfo.TrackTags.Contributors.Authors));
                    tags.AddFrame(MetadataHelpers.BuildUserTextInformationFrame("TIPL", "PRODUCERS", _trackInfo.TrackTags.Contributors.Publishers));
                    tags.AddFrame(MetadataHelpers.BuildUserTextInformationFrame("TIPL", "ENGINEERS", _trackInfo.TrackTags.Contributors.Engineers));
                    tags.AddFrame(MetadataHelpers.BuildUserTextInformationFrame("TIPL", "MIXERS", _trackInfo.TrackTags.Contributors.Mixers));
                }

                if (_albumInfo?.AlbumTags != null)
                {
                    tags.Album = _albumInfo.AlbumTags.Title;
                    tags.Genres = _albumInfo.AlbumTags.Genres.GenreData.Select(x => x.Name).ToArray();

                    if (_albumInfo.AlbumTags.NumberOfTracks != null)
                    {
                        tags.TrackCount = uint.Parse(_albumInfo.AlbumTags.NumberOfTracks);
                    }

                    if (_albumInfo.AlbumTags.NumberOfDiscs != null)
                    {
                        tags.DiscCount = uint.Parse(_albumInfo.AlbumTags.NumberOfDiscs);
                    }

                    if (_albumInfo.AlbumTags.Type == "Compilation" || _albumInfo.AlbumTags.Type == "Playlist")
                    {
                        tags.IsCompilation = true;
                    }

                    tags.Copyright = _albumInfo.AlbumTags.Copyright;

                    string year = _albumInfo.AlbumTags.ReleaseDate;

                    if (!string.IsNullOrWhiteSpace(year))
                    {
                        string[] yearSplit = year.Split("-");

                        if (yearSplit[0].Length == 4 && uint.TryParse(yearSplit[0], out uint yearParsed))
                        {
                            tags.Year = yearParsed;
                        }
                    }
                    
                    tags.AddFrame(MetadataHelpers.BuildTextInformationFrame("TPUB", _albumInfo.AlbumTags.Label));
                    tags.AddFrame(MetadataHelpers.BuildTextInformationFrame("TDOR", _albumInfo.AlbumTags.ReleaseDate));
                    tags.AddFrame(MetadataHelpers.BuildUserTextInformationFrame("UPC", _albumInfo.AlbumTags.Upc));
                }

                if (_trackInfo?.Lyrics != null)
                {
                    tags.Lyrics = _trackInfo.Lyrics.UnSyncedLyrics;
                    WriteLyricsFile();
                }

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
            if (_trackInfo.Lyrics.SyncedLyrics == null)
            {
                return;
            }
            
            IEnumerable<SyncedLyrics> syncedLyrics = _trackInfo.Lyrics.SyncedLyrics
                .Where(y => y != null)
                .Where(x => !string.IsNullOrWhiteSpace(x.Timestamp) || !string.IsNullOrWhiteSpace(x.Line));

            string lyricsFormatted = string.Join("\r\n", syncedLyrics.Select(x => x.Timestamp + x.Line));

            string lyricFilePath = Path.Combine(
                Path.GetDirectoryName(_filepath), Path.GetFileNameWithoutExtension(_filepath) + ".lrc");

            try
            {
                using (FileStream fileStream = File.Create(lyricFilePath))
                {
                    using (var sw = new StreamWriter(fileStream))
                    {
                        sw.Write(lyricsFormatted);
                    }
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}