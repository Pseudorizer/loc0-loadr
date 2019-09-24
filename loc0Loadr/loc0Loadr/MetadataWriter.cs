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
using Picture = TagLib.Picture;
using PictureType = TagLib.PictureType;
using Tag = TagLib.Id3v2.Tag;

namespace loc0Loadr
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
                
                comments["Media"] = new VorbisCommentValues("Digital Media");

                if (_albumInfo?.AlbumTags != null)
                {
                    comments.Album = new VorbisCommentValues(_albumInfo.AlbumTags.Title ?? "");
                    comments.Genre = new VorbisCommentValues(_albumInfo.AlbumTags.Genres.GenreData.Select(x => x.Name));

                    string year = _albumInfo.AlbumTags.ReleaseDate;

                    if (!string.IsNullOrWhiteSpace(year))
                    {
                        var yearSplit = year.Split("-");

                        if (yearSplit[0].Length == 4)
                        {
                            year = yearSplit[0];
                        }
                    }
                    
                    comments.Date = new VorbisCommentValues(year ?? "");
                    comments["ORIGINALDATE"] = new VorbisCommentValues(_albumInfo.AlbumTags.ReleaseDate ?? "");
                    comments["TRACKTOTAL"] = new VorbisCommentValues(_albumInfo.AlbumTags.NumberOfTracks ?? "");
                    comments["DISCTOTAL"] = new VorbisCommentValues(_albumInfo.AlbumTags.NumberOfDiscs ?? "");
                    comments["COPYRIGHT"] = new VorbisCommentValues(_albumInfo.AlbumTags.Copyright ?? "");
                    comments["UPC"] = new VorbisCommentValues(_albumInfo.AlbumTags.Upc ?? "");
                    comments["LABEL"] = new VorbisCommentValues(_albumInfo.AlbumTags.Label ?? "");
                }

                if (_trackInfo?.TrackTags != null)
                {
                    comments.Title = new VorbisCommentValues(_trackInfo.TrackTags.Title ?? "");
                    comments.Artist = new VorbisCommentValues(_trackInfo.TrackTags.Artists.Select(x => x.Name));
                    comments["DISCNUMBER"] = new VorbisCommentValues(_trackInfo.TrackTags.DiscNumber ?? "");
                    comments["TRACKNUMBER"] = new VorbisCommentValues(_trackInfo.TrackTags.TrackNumber ?? "");
                    comments["BPM"] = new VorbisCommentValues(_trackInfo.TrackTags.Bpm ?? "");
                    comments["LENGTH"] = new VorbisCommentValues(_trackInfo.TrackTags.Duration ?? "");
                    comments["ISRC"] = new VorbisCommentValues(_trackInfo.TrackTags.Isrc ?? "");
                    comments["EXPLICIT"] = new VorbisCommentValues(_trackInfo.TrackTags.ExplicitLyrics ?? "0");
                    comments["ALBUMARTIST"] = new VorbisCommentValues(_trackInfo.TrackTags.AlbumArtist ?? "");
                    comments["REPLAYGAIN_TRACK_GAIN"] = new VorbisCommentValues(_trackInfo.TrackTags.Gain ?? ""); 
                    
                    Helpers.AddTagIfNotNull(comments, "COMPOSER", _trackInfo.TrackTags.Contributors.Composers);
                    Helpers.AddTagIfNotNull(comments, "PUBLISHER", _trackInfo.TrackTags.Contributors.Publishers);
                    Helpers.AddTagIfNotNull(comments, "PRODUCER", _trackInfo.TrackTags.Contributors.Producers);
                    Helpers.AddTagIfNotNull(comments, "ENGINEER", _trackInfo.TrackTags.Contributors.Engineers);
                    Helpers.AddTagIfNotNull(comments, "WRITER", _trackInfo.TrackTags.Contributors.Writers);
                    Helpers.AddTagIfNotNull(comments, "MIXER", _trackInfo.TrackTags.Contributors.Mixers);
                    Helpers.AddTagIfNotNull(comments, "AUTHOR", _trackInfo.TrackTags.Contributors.Authors);
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
                
                tags.AddFrame(Helpers.BuildTextInformationFrame("TMED", "Digital Media"));

                if (_trackInfo?.TrackTags != null)
                {
                    tags.Title = _trackInfo.TrackTags.Title;
                    tags.Performers = _trackInfo.TrackTags.Artists.Select(x => x.Name).ToArray();
                    tags.AlbumArtists = new[] {_trackInfo.TrackTags.AlbumArtist}; // i don't like just using ART_NAME as the only album artist
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
                    
                    tags.AddFrame(Helpers.BuildTextInformationFrame("TSRC", _trackInfo.TrackTags.Isrc));
                    tags.AddFrame(Helpers.BuildTextInformationFrame("TPUB", _trackInfo.TrackTags.Contributors.Publishers));
                    tags.AddFrame(Helpers.BuildTextInformationFrame("TLEN", _trackInfo.TrackTags.Duration));
                    tags.AddFrame(Helpers.BuildUserTextInformationFrame("EXPLICIT", _trackInfo.TrackTags.ExplicitLyrics));
                    tags.AddFrame(Helpers.BuildUserTextInformationFrame("REPLAYGAIN_TRACK_GAIN", _trackInfo.TrackTags.Gain));
                    tags.AddFrame(Helpers.BuildUserTextInformationFrame("WRITERS", _trackInfo.TrackTags.Contributors.Writers));
                    tags.AddFrame(Helpers.BuildUserTextInformationFrame("AUTHORS", _trackInfo.TrackTags.Contributors.Authors));
                    tags.AddFrame(Helpers.BuildUserTextInformationFrame("TIPL", "PRODUCERS", _trackInfo.TrackTags.Contributors.Publishers));
                    tags.AddFrame(Helpers.BuildUserTextInformationFrame("TIPL", "ENGINEERS", _trackInfo.TrackTags.Contributors.Engineers));
                    tags.AddFrame(Helpers.BuildUserTextInformationFrame("TIPL", "MIXERS", _trackInfo.TrackTags.Contributors.Mixers));
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

                    tags.Copyright = _albumInfo.AlbumTags.Copyright;

                    string year = _albumInfo.AlbumTags.ReleaseDate;

                    if (!string.IsNullOrWhiteSpace(year))
                    {
                        var yearSplit = year.Split("-");

                        if (yearSplit[0].Length == 4 && uint.TryParse(yearSplit[0], out uint yearParsed))
                        {
                            tags.Year = yearParsed;
                        }
                    }
                    
                    tags.AddFrame(Helpers.BuildTextInformationFrame("TPUB", _albumInfo.AlbumTags.Label));
                    tags.AddFrame(Helpers.BuildTextInformationFrame("TDOR", _albumInfo.AlbumTags.ReleaseDate));
                    tags.AddFrame(Helpers.BuildUserTextInformationFrame("UPC", _albumInfo.AlbumTags.Upc));
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