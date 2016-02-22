using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TagLib;
using TagLib.Id3v2;
using File = TagLib.File;

namespace DolomiteBackgroundProcessing
{
    /// <summary>
    /// This class represents the metadata for a track. It can easily be broken down into a
    /// dictionary that can be stored in the metadata table. It also utilizes the TagLib# library
    /// for processing files and generating this metadata object
    /// </summary>
    public class TrackMetadata
    {
        private static ImageCodecInfo[] _imageCodecs = ImageCodecInfo.GetImageEncoders();


        public string Codec { get; private set; }
        public int BitrateKbps { get; private set; }
        public int Duration { get; private set; }
        public string Artist { get; private set; }
        public string AlbumArtist { get; private set; }
        public string Album { get; private set; }
        public string Composer { get; private set; }
        public string Performer { get; private set; }
        public string Date { get; private set; }
        public string Genre { get; private set; }
        public string Title { get; private set; }
        public string DiscNumber { get; private set; }
        public string TotalDiscs { get; private set; }
        public string TrackNumber { get; private set; }
        public string TotalTracks { get; private set; }
        public string Copyright { get; private set; }
        public string Comment { get; private set; }
        public Dictionary<string, string> CustomFrames { get; private set; }
        public string Publisher { get; private set; }
        public byte[] ImageBytes { get; private set; }
        public string ImageMimetype { get; private set; }

        public TrackMetadata(FileStream file, string mimetype)
        {
            // Setup the internal arrays
            CustomFrames = new Dictionary<string, string>();

            File tagFile = File.Create(new StreamFileAbstraction(file.Name, file, null), mimetype, ReadStyle.Average);

            // Fetch the metadata from the file
            if (tagFile.TagTypesOnDisk.HasFlag(TagTypes.Xiph))
            {
                ReadXiphMetadata(tagFile);
            }
            else if (tagFile.TagTypesOnDisk.HasFlag(TagTypes.Id3v2))
            {
                ReadId3V2Metadata(tagFile);
            }
            else if (tagFile.TagTypesOnDisk.HasFlag(TagTypes.Id3v1))
            {
                ReadId3V1Metadata(tagFile);
            }
            else if (tagFile.TagTypes.HasFlag(TagTypes.Asf))
            {
                ReadAsfMetadata(tagFile);
            }
            else
            {
                throw new FormatException("File contains tags that should not be read.");
            }

            ReadCodecDetails(tagFile);
            ReadPictureDetails(tagFile);
        }

        #region TagLib# Providers

        private void ReadAsfMetadata(File tagFile)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Stores ID3v1.1 specific tags from a TagLib file abstraction.
        /// See http://id3.org/ID3v1 for details of the (incredibly limited) standard
        /// </summary>
        /// <param name="tagFile">The TagLib abstraction to extract tags from</param>
        private void ReadId3V1Metadata(File tagFile)
        {
            TagLib.Id3v1.Tag tags = (TagLib.Id3v1.Tag)tagFile.GetTag(TagTypes.Id3v1);

            // ID3v1 and ID3v1.1 are incredibly basic.
            Title = tags.Title;
            Artist = tags.Performers.FirstOrDefault();
            Album = tags.Album;
            Date = tags.Year == default(uint) ? null : tags.Year.ToString(CultureInfo.InvariantCulture);
            Comment = tags.Comment;
            Genre = tagFile.Tag.Genres.FirstOrDefault();
        }

        /// <summary>
        /// Reads ID3v2 specific tags from a TagLib File abstraction.
        /// See http://id3.org/id3v2.3.0 for details of the v2 standard
        /// </summary>
        /// <param name="tagFile">The TagLib abstraction to extract tags from</param>
        private void ReadId3V2Metadata(File tagFile)
        {
            TagLib.Id3v2.Tag tags = (TagLib.Id3v2.Tag)tagFile.GetTag(TagTypes.Id3v2);

            // Process all the frames for the tags
            foreach (Frame frame in tags)
            {
                switch (Encoding.ASCII.GetString(frame.FrameId.ToArray()))
                {
                    case "TPE1":
                        Artist = ((TextInformationFrame) frame).Text[0];
                        break;
                    case "TPE2":
                        AlbumArtist = ((TextInformationFrame) frame).Text[0];
                        break;
                    case "TALB":
                        Album = ((TextInformationFrame) frame).Text[0];
                        break;
                    case "TDRC":            // As per investigation
                    case "TDAT":            // As per ID3.org
                    case "TYER":            // Also as per ID3.org
                        Date = ((TextInformationFrame) frame).Text[0];
                        break;
                    case "TCON":
                        // I ain't fuckin around with no fuckin ID3v1 genre codes
                        Genre = tags.Genres[0];
                        break;
                    case "TCOM":
                        Composer = ((TextInformationFrame) frame).Text[0];
                        break;
                    case "TPOS":
                        string discInfo = ((TextInformationFrame) frame).Text[0];
                        FractionalField disc = new FractionalField(discInfo);
                        DiscNumber = disc.Positional;
                        TotalDiscs = disc.Total;
                        break;
                    case "TRCK":
                        string trackInfo = ((TextInformationFrame)frame).Text[0];
                        FractionalField track = new FractionalField(trackInfo);
                        TrackNumber = track.Positional;
                        TotalTracks = track.Total;
                        break;
                    case "TIT2":
                        Title = ((TextInformationFrame)frame).Text[0];
                        break;
                    case "TCOP":
                        Copyright = ((TextInformationFrame)frame).Text[0];
                        break;
                    case "COMM":
                        Comment = ((CommentsFrame)frame).Text;
                        break;
                    case "PRIV":
                        PrivateFrame privFrame = (PrivateFrame)frame;
                        string value = Encoding.ASCII.GetString(privFrame.PrivateData.ToArray());
                        CustomFrames.Add(privFrame.Owner, value);
                        break;
                    case "TPUB":
                        Publisher = ((TextInformationFrame)frame).Text[0];
                        break;
                    case "APIC":
                        // We can trust the TagLib abstraction to handle this for us.
                        break;
                    case "TXXX":
                        // This requires some additional processing to do correctly
                        UserTextInformationFrame f = (UserTextInformationFrame) frame;
                        switch (f.Description.ToUpperInvariant())
                        {
                            case "PERFORMER":
                                Publisher = f.Text[0];
                                break;
                            default:
                                CustomFrames.Add(f.Description, String.Join(";", f.Text));
                                break;
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Reads XIPH tags from the TagLib File abstraction. These tags are generally added to
        /// FLAC and OGG files. There isn't really any consensus on header values, but see
        /// https://wiki.xiph.org/Field_names for a list to some sources
        /// </summary>
        /// <param name="tagFile">The TagLib abstraction to extract tags from</param>
        private void ReadXiphMetadata(File tagFile)
        {
            TagLib.Ogg.XiphComment tags = (TagLib.Ogg.XiphComment)tagFile.GetTag(TagTypes.Xiph);

            foreach (string fieldName in tags)
            {
                string fieldValue = tags.GetFirstField(fieldName);
                switch (fieldName)
                {
                    case "ARTIST":
                        Artist = fieldValue;
                        break;
                    case "ALBUMARTIST":
                    case "ALBUM ARTIST":            // Old foobar2000 format
                        AlbumArtist = fieldValue;
                        break;
                    case "ALBUM":
                        Album = fieldValue;
                        break;
                    case "DATE":
                        Date = fieldValue;
                        break;
                    case "TITLE":
                        Title = fieldValue;
                        break;
                    case "GENRE":
                        Genre = fieldValue;
                        break;
                    case "DISCNUMBER":
                        DiscNumber = fieldValue;
                        break;
                    case "DISCTOTAL":
                    case "TOTALDISCS":              // Old foobar2000 format
                        TotalDiscs = fieldValue;
                        break;
                    case "TRACKNUMBER":
                        TrackNumber = fieldValue;
                        break;
                    case "TRACKTOTAL":
                    case "TOTALTRACKS":             // Old foobar2000 format
                        TotalTracks = fieldValue;
                        break;
                    case "COPYRIGHT":
                        Copyright = fieldValue;
                        break;
                    case "COMMENT":
                        Comment = fieldValue;
                        break;
                    case "ORGANIZATION":
                        Publisher = fieldValue;
                        break;
                    default:
                        // Private fields/custom fields
                        CustomFrames.Add(fieldName, fieldValue);
                        break;
                }
            }
        }

        /// <summary>
        /// Extracts codec information. At most basic, the codec used is determined. For types with
        /// subtypes (such as MP3's VBR/CBR) this information is processed into the codec string.
        /// </summary>
        /// <param name="tagFile">The TagLib abstraction to extract tags from</param>
        private void ReadCodecDetails(File tagFile)
        {
            Duration = (int)Math.Ceiling(tagFile.Properties.Duration.TotalMilliseconds);

            ICodec codec = tagFile.Properties.Codecs.First();
            if (codec is TagLib.Mpeg.AudioHeader)
            {
                TagLib.Mpeg.AudioHeader mp3Codec = (TagLib.Mpeg.AudioHeader)codec;
                BitrateKbps = mp3Codec.AudioBitrate;
                Codec = "MP3 / ";
                if (mp3Codec.VBRIHeader.Present || mp3Codec.XingHeader.Present)
                {
                    Codec += "VBR";
                }
                else
                {
                    Codec += "CBR";
                }
            }
            else if (codec is TagLib.Flac.StreamHeader)
            {
                TagLib.Flac.StreamHeader flacCodec = (TagLib.Flac.StreamHeader)codec;
                BitrateKbps = flacCodec.AudioBitrate;
                Codec = "FLAC";
            }
            else if (codec is TagLib.Ogg.Codecs.Vorbis)
            {
                TagLib.Ogg.Codecs.Vorbis vorbisCodec = (TagLib.Ogg.Codecs.Vorbis)codec;
                BitrateKbps = vorbisCodec.AudioBitrate;
                Codec = "Vorbis";
            }
        }

        /// <summary>
        /// Uses the Tag abstraction from TagLib to determine whether or not a track has an
        /// attached image and if so, what its dimensions are. Dimension calculation is performed
        /// using the System.Drawing libraries.
        /// </summary>
        /// <param name="tagFile">The TagLib abstraction to extract tags from</param>
        private void ReadPictureDetails(File tagFile)
        {
            // Do stuff to figure out if it has a picture attached
            if (tagFile.Tag.Pictures.Length > 0)
            {
                ImageBytes = tagFile.Tag.Pictures.First().Data.Data;
            }
            else if (tagFile.TagTypesOnDisk.HasFlag(TagTypes.Xiph))
            {
                TagLib.Ogg.XiphComment xiph = (TagLib.Ogg.XiphComment)tagFile.GetTag(TagTypes.Xiph);
                string imageField = xiph.GetFirstField("METADATA_BLOCK_PICTURE");
                if (!String.IsNullOrWhiteSpace(imageField))
                {
                    FlacImage flacImage = new FlacImage(imageField);
                    ImageBytes = flacImage.ImageBytes;
                }
            }

            // Figure out the mimetype so we can store it
            if (ImageBytes != null)
            {
                using (MemoryStream ms = new MemoryStream(ImageBytes))
                {
                    Image imageObj = Image.FromStream(ms);
                    ImageMimetype = _imageCodecs.First(c => c.FormatID == imageObj.RawFormat.Guid).MimeType;
                }
            }
        }

        #endregion

        #region Internal Parsers

        private class FlacImage
        {
            public string Mimetype { get; private set; }
            public byte[] ImageBytes { get; private set; }

            public FlacImage(string metadataBlock)
            {
                // Convert the string to a proper char array
                byte[] metadataBytes = Convert.FromBase64String(metadataBlock);

                // Extract out the mimetype
                int mimetypeLength = GetBigEndianInt(metadataBytes, 4);

                if (mimetypeLength > 0)
                {
                    byte[] mimetypeBytes = new byte[mimetypeLength];
                    Buffer.BlockCopy(metadataBytes, 8, mimetypeBytes, 0, mimetypeLength);
                    Mimetype = Encoding.ASCII.GetString(mimetypeBytes);     // Standard declares this will always be ASCII 0x20-0x7e
                }

                // Extract out the description
                int descriptionLengthOffset = 8 + mimetypeLength;
                int descriptionLength = GetBigEndianInt(metadataBytes, descriptionLengthOffset);
                int descriptionOffset = descriptionLengthOffset + 4;

                // Extract the length of the image
                int imageLengthOffset = descriptionOffset + descriptionLength + 16;
                int pictureBytesLength = GetBigEndianInt(metadataBytes, imageLengthOffset);

                // Extract the bytes of the image
                if (pictureBytesLength > 0)
                {
                    ImageBytes = new byte[pictureBytesLength];
                    Buffer.BlockCopy(metadataBytes, imageLengthOffset + 4, ImageBytes, 0, pictureBytesLength);
                }
            }

            private static int GetBigEndianInt(IList<byte> source, int offset)
            {
                return (source[offset++] << 24) | (source[offset++] << 16) | (source[offset++] << 8) | (source[offset]);
            }
        }

        private class FractionalField
        {
            private static readonly Regex InternalRegex = new Regex(@"(?<positional>\w+)(/(?<total>\w+))?");

            public FractionalField(string value)
            {
                // Apply the regex to see if we have a match
                var match = InternalRegex.Match(value);
                if (match.Success)
                {
                    // Extract the information from the regex
                    Group positionalGroup = match.Groups["positional"];
                    Positional = positionalGroup.Success ? positionalGroup.Value : null;

                    Group totalGroup = match.Groups["total"];
                    Total = totalGroup.Success ? totalGroup.Value : null;
                }
                else
                {
                    Positional = null;
                    Total = null;
                }
            }

            public string Positional { get; private set; }
            public string Total { get; private set; }
        }

        #endregion
    }
}
