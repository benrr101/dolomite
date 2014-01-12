using System;

namespace DolomiteModel.PublicRepresentations
{
    public class Session
    {
        public string Token { get; set; }

        public string Username { get; set; }

        public string ApiKey { get; set; }

        public string InitialIpAddress { get; set; }

        public DateTime InitializedTime { get; set; }

        public DateTime AbsoluteTimeout { get; set; }

        public DateTime IdleTimeout { get; set; }
    }
}
