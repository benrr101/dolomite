using System.Collections.Generic;
using System.Runtime.Serialization;

namespace DolomiteModel.PublicRepresentations
{
    [DataContract]
    public class AutoPlaylist : Playlist
    {
        /// <summary>
        /// Limit the playlist to a certain number of tracks. Can be null.
        /// </summary>
        [DataMember]
        public int? Limit { get; set; }

        /// <summary>
        /// Whether or not to matching tracks must match all rules. True 
        /// corresponds to "all" rules must be matched. False corresponds to
        /// "any" rule must be matched.
        /// </summary>
        [DataMember]
        public bool MatchAll { get; set; }

        /// <summary>
        /// The list of rules to use when determining the contents of the playlist
        /// </summary>
        [DataMember]
        public List<AutoPlaylistRule> Rules { get; set; } 
    }
}
