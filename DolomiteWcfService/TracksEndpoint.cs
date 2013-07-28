using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.ServiceModel.Web;

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

        /// <summary>
        /// Uploads a track from the RESTful API to Azure blob storage. This
        /// also pulls out the track's metadata and loads the info into the db.
        /// Lastly, this kicks off a thread to run the converters to create the
        /// different bitrates of the track.
        /// </summary>
        /// <param name="file">Stream of the file that is uploaded</param>
        /// <returns>Http response. Follows the standard.</returns>
        public void UploadTrack(Stream file)
        {
            try
            {
                // Upload the track
                TrackManager.UploadTrack(file);
            }
            catch (Exception e)
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.InternalServerError;
            }
        }

        public Stream DownloadTrack(string guid)
        {
            WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.NotImplemented;
            return null;
            
            /*Guid trackGuid;
            if (!Guid.TryParse(guid, out trackGuid))
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                return null;
            }

            Stream output = TrackManager.GetTrack(guid);
            if (output == null)
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.NotFound;
                return null;
            }
            return output;*/
        }

        //TODO: REMOVE THIS TEST METHOD
        public List<string> GetTracks()
        {
            WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.NotImplemented;
            return null;
        }
    }
}
