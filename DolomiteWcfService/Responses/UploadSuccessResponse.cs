using System;
using System.Runtime.Serialization;

namespace DolomiteWcfService.Responses
{
    [DataContract]
    public class UploadSuccessResponse : Response
    {
        [DataMember]
        public string Guid { get; set; }

        [DataMember]
        public string Hash { get; set; }

        public UploadSuccessResponse(Guid guid, string hash)
            : base(StatusValue.Success)
        {
            Guid = guid.ToString();
            Hash = hash;
        }
    }
}
