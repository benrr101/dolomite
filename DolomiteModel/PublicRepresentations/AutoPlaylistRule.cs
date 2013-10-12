using System.Runtime.Serialization;

namespace DolomiteModel.PublicRepresentations
{
    [DataContract]
    class AutoPlaylistRule
    {
        /// <summary>
        /// The metadata field the rule is based on
        /// </summary>
        [DataMember]
        public string Field { get; set; }

        /// <summary>
        /// The type of the rule &gt; &lt;, etc are acceptable
        /// </summary>
        [DataMember]
        public string RuleType { get; set; }

        /// <summary>
        /// The object of the rule
        /// </summary>
        [DataMember]
        public string Value { get; set; }
    }
}
