using System.IO;
using System.Net.Http;
using System.ServiceModel;
using System.ServiceModel.Web;

namespace DolomiteWcfService
{
    [ServiceContract]
    public interface IServiceEndpoint
    {
        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = "/tracks/")]
        void UploadTrack(Stream file);
    }
}
