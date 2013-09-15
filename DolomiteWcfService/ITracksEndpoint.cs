using System.IO;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Web;

namespace DolomiteWcfService
{
    [ServiceContract]
    public interface ITracksEndpoint
    {
        #region Create Operations

        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = "/")]
        Message UploadTrack(Stream file);

        #endregion

        #region Retrieve Operations

        [OperationContract]
        [WebInvoke(Method = "GET", UriTemplate = "/{hash}")]
        Message GetTrackMetadata(string hash);

        [OperationContract]
        [WebInvoke(Method = "GET", UriTemplate = "/{quality}/{guid}")]
        Stream DownloadTrack(string quality, string guid);

        [OperationContract]
        [WebInvoke(Method = "GET", UriTemplate = "/")]
        Message GetAllTracks();

        #endregion

        #region Update Operations

        [OperationContract]
        [WebInvoke(Method = "PUT", UriTemplate = "/{guid}")]
        Message ReplaceTrack(Stream file, string guid);

        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = "/{guid}")]
        Message ReplaceMetadata(string body, string guid);

        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = "/{guid}?clear")]
        Message ReplaceAllMetadata(string body, string guid);

        #endregion

        #region Deletion Operations

        [OperationContract]
        [WebInvoke(Method = "DELETE", UriTemplate = "/{guid}")]
        Message DeleteTrack(string guid);

        #endregion
    }
}
