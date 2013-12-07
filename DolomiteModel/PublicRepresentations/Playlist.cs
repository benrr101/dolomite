using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace DolomiteModel.PublicRepresentations
{
    [DataContract]
    public class Playlist
    {
        public enum PlaylistType
        {
            Auto,
            Static
        }

        /// <summary>
        /// Id of the playlist
        /// </summary>
        [DataMember]
        public Guid Id { get; set; }

        /// <summary>
        /// Href to the playlist
        /// </summary>
        [DataMember]
        public string Href {
            get
            {
                string href = "/playlists/";
                href += Type == PlaylistType.Auto ? "auto/" : "static/";
                href += Id.ToString();
                return href;
            }
        }

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

        /// <summary>
        /// The type of the playlist
        /// </summary>
        public PlaylistType Type { get; set; }
    }
}
