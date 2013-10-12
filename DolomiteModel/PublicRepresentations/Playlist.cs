using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace DolomiteModel.PublicRepresentations
{
    [DataContract]
    class Playlist
    {
        /// <summary>
        /// Id of the playlist
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// The type of the playlist. Should only ever be "Normal" or "Auto"
        /// </summary>
        [DataMember]
        public string PlaylistType { get; set; }

        /// <summary>
        /// List of tracks the playlist contains
        /// </summary>
        [DataMember]
        public List<Guid> Tracks { get; set; }

    }
}
