using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DolomiteWcfService
{
    internal class MimetypeDetector
    {
        private const string FlacBytes = "fLaC";

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
            {new byte[] {0x30, 0x26, 0xB2, 0x75, 0x8E, 0x66, 0xCF, 0x11, 0xA6, 0xD9, 0x00, 0xAA, 0x00, 0x62, 0xCE, 0x6C}, "audio/x-ms-wma" },
            // M4A Apple Lossless
            {new byte[] {0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70, 0x4D, 0x34, 0x41, 0x20, 0x00, 0x00, 0x00, 0x00}, "audio/mp4a-latm" },
            // Flac
            {new byte[] {0x66, 0x4C, 0x61, 0x43}, "audio/x-flac"},
            // MP2 LC-AAC
            {new byte[] {0xFF, 0xF1}, "audio/aac"},
            // MP4 LC-AAC
            {new byte[] {0xFF, 0xF9}, "audio/aac"},
            // Astrid/Quartex AAC
            {new byte[] {0x41, 0x41, 0x43, 0x00, 0x01, 0x00}, "audio/aac"},
            // OGG Vorbis
            {new byte[] {0x4F, 0x67, 0x67, 0x53, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00}, "audio/ogg"},
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
        private static readonly char[] WhitespaceBytes =
        {
            (char) 0x09,
            (char) 0x0A,
            (char) 0x0B,
            (char) 0x0C,
            (char) 0x0D
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
            // Read the file into a string
            byte[] bytes = new byte[stream.Length];
            stream.Read(bytes, 0, Convert.ToInt32(stream.Length));
            string streamString = Encoding.Default.GetString(bytes);
            streamString = streamString.TrimStart(WhitespaceBytes);

            // Iterate over the available audio types
            foreach (var audioType in AudioTypes)
            {
                // Turn the bytes into a string
                string typeString = Encoding.Default.GetString(audioType.Key);

                // The header of the file must be the 
                bool match = streamString.StartsWith(typeString);

                // If it matches, we need to check for mp3 vs flac
                if (match)
                {
                    if (audioType.Value == "audio/mpeg")
                    {
                        if (streamString.Contains(FlacBytes))
                            return "audio/x-flac";
                    }
                    else
                    {
                        return audioType.Value;
                    }
                }
            }

            // If we make it to here, we've exhausted our possibilities
            return null;
        }
    }
}
