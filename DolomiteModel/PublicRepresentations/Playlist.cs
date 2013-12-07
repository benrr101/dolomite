using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace DolomiteModel.PublicRepresentations
{
    [DataContract]
    public class Playlist
    {
        /// <summary>
        /// Id of the playlist
        /// </summary>
        [DataMember]
        public Guid Id { get; set; }

        /// <summary>
        /// The name of the playlist
        /// </summary>
        [DataMember]
        public string Name { get; set; }

        /// <summary>
        /// List of tracks the playlist contains
        /// </summary>
        [DataMember]
        public List<Guid> Tracks { get; set; }

    }
}
