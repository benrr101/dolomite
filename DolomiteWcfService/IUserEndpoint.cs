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

        [WebInvoke(Method = "POST", UriTemplate = "/login")]
        Message Login();

        [WebInvoke(Method = "POST", UriTemplate = "/logout")]
        Message Logout();
    }
}
