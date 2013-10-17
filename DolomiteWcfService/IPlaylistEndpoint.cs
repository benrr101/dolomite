using System.IO;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Web;

namespace DolomiteWcfService
{
    [ServiceContract]
    interface IPlaylistEndpoint
    {

        #region Create Methods

        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = "/")]
        Message CreatePlaylist(Stream body);

        #endregion

        #region Retrieve Methods

        [OperationContract]
        [WebInvoke(Method = "GET", UriTemplate = "/")]
        Message GetAllPlaylists();


        [OperationContract]
        [WebInvoke(Method = "GET", UriTemplate = "/{guid}")]
        Message GetPlaylist(string guid);

        #endregion

        #region Update Methods

        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = "/{guid}/")]
        Message AddToPlaylist(Stream body, string guid);

        #endregion

        #region Delete Methods

        [OperationContract]
        [WebInvoke(Method = "DELETE", UriTemplate = "/{guid}/{id}")]
        Message DeleteFromPlaylist(string guid, string id);

        [OperationContract]
        [WebInvoke(Method = "DELETE", UriTemplate = "/{guid}/")]
        Message DeletePlaylist(string guid);

        #endregion

    }
}
