﻿using System.Collections.Generic;
using System.IO;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Web;

namespace DolomiteWcfService
{
    [ServiceContract]
    public interface ITracksEndpoint
    {
        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = "/", ResponseFormat = WebMessageFormat.Json)]
        string UploadTrack(Stream file);

        [OperationContract]
        [WebInvoke(Method = "HEAD", UriTemplate = "/{hash}")]
        void TrackExists(string hash);

        [OperationContract]
        [WebInvoke(Method = "GET", UriTemplate = "/original/{guid}")]
        Stream DownloadTrack(string guid);

        [OperationContract]
        [WebInvoke(Method = "GET", UriTemplate = "/{guid}")]
        Message GetTrackMetadata(string guid);

        [OperationContract]
        [WebInvoke(Method = "GET", UriTemplate = "/", ResponseFormat = WebMessageFormat.Json)]
        List<Track> GetTracks();
    }
}
