using System.Runtime.Serialization;

namespace DolomiteModel.PublicRepresentations
{
    [DataContract]
    public class AutoPlaylistLimiter
    {
        /// <summary>
        /// The number of items to limit the playlist to
        /// </summary>
        [DataMember]
        public int Limit { get; set; }

        /// <summary>
        /// Whether to sort the list descending. Not used when randomly ordering.
        /// </summary>
        [DataMember]
        public bool? SortDescending { get; set; }

        /// <summary>
        /// The tagname of the field to sort the tracks by. May be omitted for random ordering
        /// </summary>
        [DataMember]
        public string SortField { get; set; }
    }
}
