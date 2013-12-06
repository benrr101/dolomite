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
        [WebInvoke(Method = "POST", UriTemplate = "/auto/")]
        Message CreateAutoPlaylist(Stream body);

        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = "/static/")]
        Message CreateStaticPlaylist(Stream body);

        #endregion

        #region Retrieve Methods

        [OperationContract]
        [WebInvoke(Method = "GET", UriTemplate = "/")]
        Message GetAllPlaylists();


        [OperationContract]
        [WebInvoke(Method = "GET", UriTemplate = "/auto/{guid}")]
        Message GetAutoPlaylist(string guid);

        [OperationContract]
        [WebInvoke(Method = "GET", UriTemplate = "/static/{guid}")]
        Message GetStaticPlaylist(string guid);

        #endregion

        #region Update Methods

        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = "/auto/{guid}")]
        Message AddRuleToAutoPlaylist(Stream body, string guid);

        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = "/static/{guid}")]
        Message AddTrackToStaticPlaylist(Stream body, string guid);

        #endregion

        #region Delete Methods

        [OperationContract]
        [WebInvoke(Method = "DELETE", UriTemplate = "/auto/{guid}/{id}")]
        Message DeleteRuleFromAutoPlaylist(string guid, string id);

        [OperationContract]
        [WebInvoke(Method = "DELETE", UriTemplate = "/static/{guid}/{id}")]
        Message DeleteTrackFromStaticPlaylist(string guid, string id);

        [OperationContract]
        [WebInvoke(Method = "DELETE", UriTemplate = "/auto/{guid}")]
        Message DeleteAutoPlaylist(string guid);

        [OperationContract]
        [WebInvoke(Method = "DELETE", UriTemplate = "/static/{guid}")]
        Message DeleteStaticPlaylist(string guid);

        #endregion

    }
}
