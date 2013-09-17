using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DolomiteWcfService
{
    class MimetypeDetector
    {
        /// <summary>
        /// Mapping of mimetype to signature. If it isn't in this list, it isn't supported.
        /// </summary>
        /// <source>
        /// Information on the header formats came from: file-extension.net
        /// </source>
        private static readonly Dictionary<byte[], string> AudioTypes = new Dictionary<byte[], string>
        {
            // MP3 w/ID3v1 tags
            {new byte[] {0xFF}, "audio/mpeg"},
            // MP3 w/ID3v2 tags
            {new byte[] {0x49, 0x44, 0x33}, "audio/mpeg"},
            // WMA
            { new byte[] {0x30, 0x26, 0xB2, 0x75, 0x8E, 0x66, 0xCF, 0x11, 0xA6, 0xD9, 0x00, 0xAA, 0x00, 0x62, 0xCE, 0x6C}, "audio/x-ms-wma" },
            // M4A Apple Lossless
            { new byte[] {0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70, 0x4D, 0x34, 0x41, 0x20, 0x00, 0x00, 0x00, 0x00}, "audio/mp4a-latm" },
            // Flac
            {new byte[] {0x66, 0x4C, 0x61, 0x43}, "audio/x-flac"},
            // MP2 LC-AAC
            {new byte[] {0xFF, 0xF1}, "audio/aac"},
            // MP4 LC-AAC
            {new byte[] {0xFF, 0xF9}, "audio/aac"},
            // Astrid/Quartex AAC
            {new byte[] {0x41, 0x41, 0x43, 0x00, 0x01, 0x00}, "audio/aac"},
            // OGG Vorbis
            { new byte[] {0x4F, 0x67, 0x67, 0x53, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00}, "audio/ogg" },
            // Wav
            {new byte[] {0x52, 0x49, 0x46, 0x46}, "audio/wav"}
        };

        /// <summary>
        /// Mapping of mimetype to extension. This is useful for determining the original
        /// extension of the uploaded file.
        /// </summary>
        private static readonly Dictionary<string, string> MimeToExtension = new Dictionary<string, string>
        {
            {"audio/mpeg", "mp3"},
            {"audio/x-ms-wma", "wma"},
            {"audio/mp4a-latm", "m4a"},
            {"audio/x-flac", "flac"},
            {"audio/aac", "aac"},
            {"audio/ogg", "ogg"},
            {"audio/wav", "wav"}
        };

        /// <summary>
        /// Whitespace hex values. Useful for detecting multipart parser fuck-ups.
        /// </summary>
        private static readonly byte[] WhitespaceBytes = new byte[]
            {
                0x09,
                0x0A,
                0x0B,
                0x0C,
                0x0D
            };

        /// <summary>
        /// Fetches the file extension that goes with the requested mimetype
        /// </summary>
        /// <param name="mimetype">Mimetype to look up the file extension of</param>
        /// <returns>The file extension of a file with the given mimetype</returns>
        public static string GetExtension(string mimetype)
        {
            return MimeToExtension[mimetype];
        }

        /// <summary>
        /// Determine the mime type of the file stream
        /// </summary>
        /// <param name="stream">The stream to determine the mimetype of</param>
        /// <returns>A string mimetype on successful lookup. Null otherwise</returns>
        public static string GetMimeType(FileStream stream)
        {
            // Iterate over the available audio types
            foreach (var audioType in AudioTypes)
            {
                // Reset the stream to the beginning
                stream.Position = 0;
                
                // Compare all bytes in the header of the stream to the type's header
                bool match = true;
                foreach (byte b in audioType.Key)
                {
                    byte sb = (byte)stream.ReadByte();

                    // Skip whitespace bytes (if multipart parser messes up)
                    if (WhitespaceBytes.Contains(b))
                        continue;

                    // If the byte doesn't match, then it's not a match
                    if (b != sb)
                    {
                        match = false;
                        break;
                    }
                }

                // If we find a match, return it
                if (match)
                    return audioType.Value;
            }

            // If we make it to here, we've exhausted our possibilities
            return null;
        }
    }
}
