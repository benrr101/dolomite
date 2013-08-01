using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.ServiceModel.Channels;
using System.ServiceModel.Web;
using System.Text;
using Newtonsoft.Json;
using AntsCode.Util;

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
                Track track = TrackManager.GetTrack(guid);
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
                Track track = TrackManager.GetTrack(guid, false);
                string trackJson = JsonConvert.SerializeObject(track);
                return WebOperationContext.Current.CreateTextResponse(trackJson, "application/json", Encoding.UTF8);
            }
            catch (FormatException)
            {
                // The guid was probably incorrect
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                return null;
            }
            catch (FileNotFoundException)
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.NotFound;
                return null;
            }
        }

        /// <summary>
        /// Searches for a track based on its hash. Returns 200 if found, 404 if not found
        /// </summary>
        /// <param name="hash">The hash to search with</param>
        public void TrackExists(string hash)
        {
            WebOperationContext.Current.OutgoingResponse.StatusCode = TrackManager.TrackExists(hash) ? HttpStatusCode.OK : HttpStatusCode.NotFound;
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
                // Parse the file out of the request body. There's some content metadata in here that we don't really want
                // TODO: Use the content-disposition to do first-pass content-type filtering
                MultipartParser parser = new MultipartParser(file);
                if (parser.Success)
                {
                    // Upload the track
                    MemoryStream memoryStream = new MemoryStream(parser.FileContents);
                    return TrackManager.UploadTrack(memoryStream).ToString();
                }


                // Failure of one kind or another
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                return null;
            }
            catch (Exception)
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.InternalServerError;
                return null;
            }
        }

        #endregion

        /// <summary>
        /// Retrieves all the track objects in the database. These are not linked
        /// to any azure blob streams and only contain metadata.
        /// </summary>
        /// <returns>List of track objects from the track manager</returns>
        public List<Track> GetTracks()
        {
            return TrackManager.FetchAllTracks();
        }
    }
}
