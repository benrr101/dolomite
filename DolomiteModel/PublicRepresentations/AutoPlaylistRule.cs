using System.Runtime.Serialization;

namespace DolomiteModel.PublicRepresentations
{
    [DataContract]
    public class AutoPlaylistRule
    {
        [DataMember]
        public long? Id { get; set; }

        /// <summary>
        /// The metadata field the rule is based on
        /// </summary>
        [DataMember]
        public string Field { get; set; }

        /// <summary>
        /// The comparison to make against the rule
        /// </summary>
        [DataMember]
        public string Comparison { get; set; }

        /// <summary>
        /// The object of the rule
        /// </summary>
        [DataMember]
        public string Value { get; set; }
    }
}
