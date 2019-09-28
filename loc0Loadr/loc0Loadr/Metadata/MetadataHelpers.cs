using System.Linq;
using FlacLibSharp;
using TagLib;
using TagLib.Id3v2;

namespace loc0Loadr.Metadata
{
    internal static class MetadataHelpers
    {
        public static TextInformationFrame BuildTextInformationFrame(string frameType, params string[] text)
        {
            return new TextInformationFrame(new ByteVector(frameType), StringType.UTF8)
            {
                Text = text
            };
        }
        
        public static UserTextInformationFrame BuildUserTextInformationFrame(string description, params string[] text)
        {
            return new UserTextInformationFrame("TXXX", StringType.UTF8)
            {
                Text = text,
                Description = description
            };
        }
        
        public static UserTextInformationFrame BuildUserTextInformationFrame(string frameType, string description, params string[] text)
        {
            return new UserTextInformationFrame(frameType, StringType.UTF8)
            {
                Text = text,
                Description = description
            };
        }

        public static void AddTagIfNotNull(VorbisComment comments, string key, params string[] values)
        {
            if (values == null || values.Any(string.IsNullOrWhiteSpace))
            {
                return;
            }

            comments[key] = new VorbisCommentValues(values);
        }
    }
}