using System;
using System.Runtime.Serialization;

namespace DolomiteWcfService.Responses
{
    [DataContract]
    public class UploadSuccessResponse : Response
    {
        [DataMember]
        public string Guid { get; set; }

        public UploadSuccessResponse(Guid guid) : base(StatusValue.Success)
        {
            Guid = guid.ToString();
        }
    }
}
