using System;
using System.Runtime.Serialization;

namespace DolomiteWcfService {
    [DataContract]
    public class WebResponse
    {
        [DataContract]
        public enum StatusValue
        {
            [EnumMember(Value = "error")] Error,
            [EnumMember(Value = "success")] Success
        }

        [DataMember]
        public StatusValue Status { get; set; }

        public WebResponse(StatusValue status)
        {
            Status = status;
        }
    }

    [DataContract]
    public class ErrorResponse : WebResponse
    {
        [DataMember]
        public string Message { get; set; }

        public ErrorResponse(string message) : base(StatusValue.Error)
        {
            Message = message;
        }
    }

    [DataContract]
    public class LoginSuccessResponse : WebResponse
    {
        [DataMember]
        public string Token { get; set; }

        public LoginSuccessResponse(string token) : base(StatusValue.Success)
        {
            Token = token;
        }
    }

    [DataContract]
    public class UploadSuccessResponse : WebResponse
    {
        [DataMember]
        public string Guid { get; set; }

        [DataMember]
        public string Hash { get; set; }

        public UploadSuccessResponse(Guid guid, string hash) : base(StatusValue.Success)
        {
            Guid = guid.ToString();
            Hash = hash;
        }
    }

    public class PlaylistCreationSuccessResponse : WebResponse
    {
        [DataMember]
        public string Guid { get; set; }

        public PlaylistCreationSuccessResponse(Guid guid) : base(StatusValue.Success)
        {
            Guid = guid.ToString();
        }
    }
}