using System;
using System.IO;
using System.Runtime.Serialization;

namespace DolomiteModel.PublicRepresentations
{
    [DataContract]
    public class Art
    {
        /// <summary>
        /// Stream for the art file in blob storage.
        /// </summary>
        public Stream ArtStream { get; set; }

        [DataMember]
        public Guid Id { get; set; }

        /// <summary>
        /// The internal ID for the art object in the database
        /// </summary>
        public long InternalId { get; set; }

        [DataMember]
        public string Mimetype { get; set; }
    }
}
