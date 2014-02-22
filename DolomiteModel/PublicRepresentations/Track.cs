using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

namespace DolomiteModel.PublicRepresentations
{
    [DataContract]
    public class Track
    {

        #region Internal Track Quality Class

        [DataContract]
        public class Quality
        {
            #region Serializable Properties

            /// <summary>
            /// The bitrate of the track at this quality
            /// </summary>
            [DataMember]
            public string Bitrate
            {
                get { return BitrateKbps + "kbps"; }
            }

            /// <summary>
            /// The file extension of the quality
            /// </summary>
            [DataMember]
            public string Extension { get; set; }

            /// <summary>
            /// The href to the download for the quality
            /// </summary>
            [DataMember]
            public string Href { get; set; }

            /// <summary>
            /// The name of the quality
            /// </summary>
            [DataMember]
            public string Name { get; set; }

            /// <summary>
            /// The mimetype of the track at this quality
            /// </summary>
            [DataMember]
            public string Mimetype { get; set; }

            #endregion

            /// <summary>
            /// Numeric representation of the bitrate in kbps
            /// </summary>
            public int BitrateKbps { get; set; }

            /// <summary>
            /// The directory of the azure container storing the track at this quality
            /// </summary>
            public string Directory { get; set; }

            /// <summary>
            /// The stream representing this quality of the track. Normally set to null.
            /// </summary>
            public Stream FileStream { get; set; }
        }

        #endregion

        #region Properties

        /// <summary>
        /// The href to the track's art file (if it has one -- it will be null
        /// otherwise)
        /// </summary>
        [DataMember]
        public string ArtHref { get; set; }

        /// <summary>
        /// Whether or not there's an art change pending on the track. Not
        /// intended for use by clients.
        /// </summary>
        public bool ArtChange { get; set; }

        /// <summary>
        /// The Guid for the art object for this track. Not intented for use
        /// by clients.
        /// </summary>
        public Guid? ArtId { get; set; }

        /// <summary>
        /// The unique identifier for the track
        /// </summary>
        [DataMember]
        public Guid Id;

        /// <summary>
        /// The metadata about the track
        /// </summary>
        [DataMember]
        public Dictionary<string, string> Metadata;

        /// <summary>
        /// The username of the owner of the track. This is not a datamember since
        /// it should not be used by clients.
        /// </summary>
        public string Owner { get; set; }

        /// <summary>
        /// List of qualities that are available for the given track.
        /// </summary>
        [DataMember]
        public List<Quality> Qualities { get; set; }

        #endregion

    }
}

