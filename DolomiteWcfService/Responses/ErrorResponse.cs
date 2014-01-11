using System.Runtime.Serialization;

namespace DolomiteWcfService.Responses
{
    [DataContract]
    public class ErrorResponse : Response
    {
        [DataMember]
        public string Message { get; set; }

        public ErrorResponse(string message)
            : base(StatusValue.Error)
        {
            Message = message;
        }
    }
}
