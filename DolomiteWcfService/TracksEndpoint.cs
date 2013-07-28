using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.ServiceModel.Channels;
using System.ServiceModel.Web;
using System.Text;
using Newtonsoft.Json;

namespace DolomiteWcfService
{
    public class TracksEndpoint : ITracksEndpoint
    {

        #region Properties

        /// <summary>
        /// Instance of the Azure Storage Manager
        /// </summary>
        private TrackManager TrackManager { get; set; }

        #endregion

        public TracksEndpoint()
        {
            // Initialize the track manager
            TrackManager = TrackManager.Instance;
        }

        #region ITracksEndpoint Operations

        /// <summary>
        /// Downloads the given track's file stream from azure
        /// </summary>
        /// <param name="guid">The guid for the track</param>
        /// <returns>A stream with the track from azure</returns>
        public Stream DownloadTrack(string guid)
        {
            try
            {
                // Retrieve the track and return the stream
                Track track = TrackManager.DownloadTrack(guid);
                return track.FileStream;
            }
            catch (FileNotFoundException)
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.NotFound;
                return null;
            }
            catch (Exception)
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.InternalServerError;
                return null;
            }
        }

        /// <summary>
        /// Retrieves the given track's metadata -- does not load any streams 
        /// from azure
        /// </summary>
        /// <param name="guid">Guid of the track</param>
        /// <returns>A message with JSON formatted track metadata</returns>
        public Message GetTrackMetadata(string guid)
        {
            try
            {
                // Retrieve the track without the stream
                Track track = TrackManager.DownloadTrack(guid, false);
                string trackJson = JsonConvert.SerializeObject(track);
                return WebOperationContext.Current.CreateTextResponse(trackJson, "application/json", Encoding.UTF8);
            }
            catch (FileNotFoundException)
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.NotFound;
                return null;
            }
        }

        /// <summary>
        /// Uploads a track from the RESTful API to Azure blob storage. This
        /// also pulls out the track's metadata and loads the info into the db.
        /// Lastly, this kicks off a thread to run the converters to create the
        /// different bitrates of the track.
        /// </summary>
        /// <param name="file">Stream of the file that is uploaded</param>
        /// <returns>The GUID of the track that was uploaded</returns>
        public string UploadTrack(Stream file)
        {
            try
            {
                // Upload the track
                return TrackManager.UploadTrack(file).ToString();
            }
            catch (Exception e)
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.InternalServerError;
                return null;
            }
        }

        #endregion

        //TODO: REMOVE THIS TEST METHOD
        public List<string> GetTracks()
        {
            WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.NotImplemented;
            return null;
        }
    }
}
