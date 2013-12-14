using System;
using System.ServiceModel.Channels;

namespace DolomiteWcfService
{
    class UserEndpoint : IUserEndpoint
    {
        public Message CreateUser(string username)
        {
            throw new NotImplementedException();
        }

        public Message Login()
        {
            throw new NotImplementedException();
        }

        public Message Logout()
        {
            throw new NotImplementedException();
        }
    }
}
