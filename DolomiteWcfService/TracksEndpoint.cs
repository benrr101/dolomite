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

        #region Create Operations

        /// <summary>
        /// Uploads a track from the RESTful API to Azure blob storage. This
        /// also pulls out the track's metadata and loads the info into the db.
        /// Lastly, this kicks off a thread to run the converters to create the
        /// different bitrates of the track.
        /// </summary>
        /// <param name="file">Stream of the file that is uploaded</param>
        /// <returns>The GUID of the track that was uploaded</returns>
        public Message UploadTrack(Stream file)
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
                    Guid guid = TrackManager.UploadTrack(memoryStream);
                    WebResponse response = new UploadSuccessResponse(guid);
                    string responseJson = JsonConvert.SerializeObject(response);
                    return WebOperationContext.Current.CreateTextResponse(responseJson, "application/json",
                        Encoding.UTF8);
                }

                // Failure of one kind or another
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                ErrorResponse fResponse = new ErrorResponse("Failed to process request.");
                string fResponseJson = JsonConvert.SerializeObject(fResponse);
                return WebOperationContext.Current.CreateTextResponse(fResponseJson, "application/json", Encoding.UTF8);
            }
            catch (Exception e)
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.InternalServerError;
                ErrorResponse eResponse = new ErrorResponse("An internal server error occurred: " + e.Message);
                string eResponseJson = JsonConvert.SerializeObject(eResponse);
                return WebOperationContext.Current.CreateTextResponse(eResponseJson, "application/json", Encoding.UTF8);
            }
        }

        #endregion

        #region Retrieval Operations

        /// <summary>
        /// Downloads the given track's file stream from azure
        /// </summary>
        /// <param name="quality">The name of the quality to download</param>
        /// <param name="guid">The hash for the track</param>
        /// <returns>A stream with the track from azure</returns>
        public Stream DownloadTrack(string quality, string guid)
        {
            try
            {
                // Retrieve the track and return the stream
                throw new NotImplementedException("haha.");
                //return TrackManager.GetTrack(Guid.Parse(guid));
            }
            catch (FormatException)
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                return null;
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
        /// Retrieves all the track objects in the database. These are not linked
        /// to any azure blob streams and only contain metadata.
        /// </summary>
        /// <returns>List of track objects from the track manager</returns>
        public Message GetAllTracks()
        {
            // Retrieve the track without the stream
            List<Track> tracks = TrackManager.FetchAllTracks();
            string trackJson = JsonConvert.SerializeObject(tracks);
            return WebOperationContext.Current.CreateTextResponse(trackJson, "application/json", Encoding.UTF8);
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
                Track track = TrackManager.GetTrack(Guid.Parse(guid));
                string trackJson = JsonConvert.SerializeObject(track);
                return WebOperationContext.Current.CreateTextResponse(trackJson, "application/json", Encoding.UTF8);
            }
            catch (FormatException)
            {
                // The guid was probably incorrect
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                string message = String.Format("The GUID supplied '{0}' is an invalid GUID.", guid);
                string responseJson = JsonConvert.SerializeObject(new ErrorResponse(message));
                return WebOperationContext.Current.CreateTextResponse(responseJson, "application/json", Encoding.UTF8);
            }
            catch (FileNotFoundException)
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.NotFound;
                string message = String.Format("The track with the specified GUID '{0}' does not exist", guid);
                string responseJson = JsonConvert.SerializeObject(new ErrorResponse(message));
                return WebOperationContext.Current.CreateTextResponse(responseJson, "application/json", Encoding.UTF8);
            }
        }

        #endregion

        #region Update Operations

        public Message ReplaceTrack(Stream file, string guid)
        {
            throw new NotImplementedException();
        }

        public Message ReplaceMetadata(string body, string guid)
        {
            throw new NotImplementedException();
        }

        public Message ReplaceAllMetadata(string body, string guid)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Deletion Operations

        public Message DeleteTrack(string guid)
        {
            throw new NotImplementedException();
        }

        #endregion

        #endregion
    }
}
