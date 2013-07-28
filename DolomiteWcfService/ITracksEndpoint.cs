using System.Collections.Generic;
using System.IO;
using System.ServiceModel;
using System.ServiceModel.Web;

namespace DolomiteWcfService
{
    [ServiceContract]
    public interface ITracksEndpoint
    {
        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = "/")]
        void UploadTrack(Stream file);

        [OperationContract]
        [WebInvoke(Method = "GET", UriTemplate = "/{guid}")]
        Stream DownloadTrack(string guid);

        // TODO: REMOVE THIS TEST METHOD
        [OperationContract]
        [WebInvoke(Method = "GET", UriTemplate = "/", ResponseFormat = WebMessageFormat.Json)]
        List<string> GetTracks();
    }
}
