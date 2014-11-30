using System.IO;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Web;

namespace DolomiteWcfService
{
    [ServiceContract]
    interface IUserEndpoint
    {
        [WebInvoke(Method = "PUT", UriTemplate = "/{username}")]
        Message CreateUser(string username, Stream body);

        [WebInvoke(Method = "GET", UriTemplate = "/{username}")]
        Message GetUserStatistics(string username);

        [WebInvoke(Method="GET", UriTemplate = "/{username}/settings")]
        Message GetUserSettings(string username);

        [WebInvoke(Method = "POST", UriTemplate = "/{username}/login")]
        Message Login(string username, Stream body);

        [WebInvoke(Method = "POST", UriTemplate = "/{username}/logout")]
        Message Logout(string username);

        [WebInvoke(Method ="PUT", UriTemplate = "/{username}/settings")]
        Message StoreUserSettings(string username, Stream body);

        [WebInvoke(Method = "OPTIONS", UriTemplate = "/*")]
        bool PreflyRequest();
    }
}
