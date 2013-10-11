using System;
using System.Runtime.Serialization;

namespace DolomiteModel.PublicRepresentations
{
    [DataContract]
    public class Art
    {
        [DataMember]
        public Guid Id { get; set; }

        [DataMember]
        public string Mimetype { get; set; }
    }
}
