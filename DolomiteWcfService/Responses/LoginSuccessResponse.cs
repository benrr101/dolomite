using System.Runtime.Serialization;

namespace DolomiteWcfService.Responses
{
    [DataContract]
    public class LoginSuccessResponse : Response
    {
        [DataMember]
        public string Token { get; set; }

        public LoginSuccessResponse(string token)
            : base(StatusValue.Success)
        {
            Token = token;
        }
    }
}
