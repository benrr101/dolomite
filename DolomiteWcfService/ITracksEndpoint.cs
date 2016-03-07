using System.IO;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Web;
using System.Threading.Tasks;

namespace DolomiteWcfService
{
    [ServiceContract]
    public interface ITracksEndpoint
    {
        #region Create Operations

        [OperationContract]
        [WebInvoke(Method = "PUT", UriTemplate = "/{guid}?md5hash={hash}&originalFilename={filename}")]
        Task<Message> UploadTrack(Stream file, string guid, string hash, string filename);

        #endregion

        #region Retrieve Operations

        [OperationContract]
        [WebInvoke(Method = "GET", UriTemplate = "/{guid}")]
        Message GetTrackMetadata(string guid);

        [OperationContract]
        [WebInvoke(Method = "GET", UriTemplate = "/{quality}/{guid}")]
        Stream DownloadTrack(string quality, string guid);

        [OperationContract]
        [WebInvoke(Method = "GET", UriTemplate = "/")]
        Message GetAllTracks();

        #endregion

        #region Update Operations

        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = "/{guid}")]
        Message ReplaceMetadata(Stream body, string guid);

        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = "/{guid}?clear")]
        Message ReplaceAllMetadata(Stream body, string guid);

        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = "/{guid}/art")]
        Message ReplaceTrackArt(Stream body, string guid);

        #endregion

        #region Deletion Operations

        [OperationContract]
        [WebInvoke(Method = "DELETE", UriTemplate = "/{guid}")]
        Message DeleteTrack(string guid);

        #endregion

        [WebInvoke(Method = "OPTIONS", UriTemplate = "/*")]
        bool PreflyRequest();
    }
}
