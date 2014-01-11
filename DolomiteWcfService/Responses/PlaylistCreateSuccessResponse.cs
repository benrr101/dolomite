using System;
using System.Runtime.Serialization;

namespace DolomiteWcfService.Responses
{
    public class PlaylistCreationSuccessResponse : Response
    {
        [DataMember]
        public string Guid { get; set; }

        public PlaylistCreationSuccessResponse(Guid guid)
            : base(StatusValue.Success)
        {
            Guid = guid.ToString();
        }
    }
}
