using System.IO;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Web;

namespace DolomiteWcfService
{
    [ServiceContract]
    interface IAutoPlaylistEndpoint
    {
        #region Create Methods

        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = "/")]
        Message CreateAutoPlaylist(Stream body);

        #endregion

        #region Retrieve Methods

        [OperationContract]
        [WebInvoke(Method = "GET", UriTemplate = "/")]
        Message GetAllAutoPlaylists();

        [OperationContract]
        [WebInvoke(Method = "GET", UriTemplate = "/{guid}")]
        Message GetAutoPlaylist(string guid);

        #endregion

        #region Update Methods

        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = "/{guid}")]
        Message AddRuleToAutoPlaylist(Stream body, string guid);

        #endregion

        #region Delete Methods

        [OperationContract]
        [WebInvoke(Method = "DELETE", UriTemplate = "/{guid}/{id}")]
        Message DeleteRuleFromAutoPlaylist(string guid, string id);

        [OperationContract]
        [WebInvoke(Method = "DELETE", UriTemplate = "/{guid}")]
        Message DeleteAutoPlaylist(string guid);

        #endregion

    }
}
