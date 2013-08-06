using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

namespace DolomiteWcfService
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
            private string _bitrate;
            [DataMember]
            public string Bitrate
            {
                get { return _bitrate; }
                set { _bitrate = value + "kbps"; }
            }

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
            /// The directory of the azure container storing the track at this quality
            /// </summary>
            public string Directory { get; set; }
        }

        #endregion

        #region Properties

        /// <summary>
        /// The unique identifier for the track
        /// </summary>
        [DataMember] public Guid Id;

        /// <summary>
        /// The metadata about the track
        /// </summary>
        [DataMember]
        public Dictionary<string, string> Metadata;

        [DataMember]
        public List<Quality> Qualities { get; set; } 

        #endregion

    }
}
