using System.Runtime.Serialization;

namespace DolomiteWcfService.Responses
{
    [DataContract]
    public class Response
    {
        [DataContract]
        public enum StatusValue
        {
            [EnumMember(Value = "error")]
            Error,
            [EnumMember(Value = "success")]
            Success
        }

        [DataMember]
        public StatusValue Status { get; set; }

        public Response(StatusValue status)
        {
            Status = status;
        }
    }
}
