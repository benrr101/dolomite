using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Net;
using System.ServiceModel.Channels;
using System.ServiceModel.Web;
using System.Text;
using Newtonsoft.Json;
using AntsCode.Util;
using TagLib;

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
                MemoryStream memoryStream;
                if (WebOperationContext.Current.IncomingRequest.ContentType.StartsWith("multipart/form-data"))
                {
                    // We need to process out other form data for the stuff we want
                    MultipartParser parser = new MultipartParser(file);
                    if (parser.Success)
                    {
                        memoryStream = new MemoryStream(parser.FileContents);
                    }
                    else
                    {
                        // Failure of one kind or another
                        WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                        ErrorResponse fResponse = new ErrorResponse("Failed to process request.");
                        string fResponseJson = JsonConvert.SerializeObject(fResponse);
                        return WebOperationContext.Current.CreateTextResponse(fResponseJson, "application/json", Encoding.UTF8);
                    }
                }
                else
                {
                    // There's no need to process out anything else. The body is the file.
                    memoryStream = new MemoryStream(ToByteArray(file));
                }

                // Upload the track
                Guid guid;
                string hash;
                TrackManager.UploadTrack(memoryStream, out guid, out hash);
                WebResponse response = new UploadSuccessResponse(guid, hash);
                string responseJson = JsonConvert.SerializeObject(response);
                return WebOperationContext.Current.CreateTextResponse(responseJson, "application/json", Encoding.UTF8);
            }
            catch (DuplicateNameException)
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.Conflict;
                ErrorResponse eResponse = new ErrorResponse("The request could not be completed. A track with the same hash already exists." +
                                                            " Duplicate tracks are not permitted");
                string eResponseJson = JsonConvert.SerializeObject(eResponse);
                return WebOperationContext.Current.CreateTextResponse(eResponseJson, "application/json", Encoding.UTF8);
            }
            catch (Exception)
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.InternalServerError;
                ErrorResponse eResponse = new ErrorResponse("An internal server error occurred");
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
                Track track = TrackManager.GetTrack(Guid.Parse(guid));
                Track.Quality qualityObj = TrackManager.GetTrackStream(track, quality);

                // Set special headers that tell the client to download the file
                // and what filename to give it
                string disposition = String.Format("attachment; filename=\"{0}.{1}\";", guid, qualityObj.Extension);
                WebOperationContext.Current.OutgoingResponse.Headers.Add("Content-Disposition", disposition);
                return qualityObj.FileStream;
            }
            catch (FormatException)
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                return null;
            }
            catch (UnsupportedFormatException)
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.NotFound;
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
            catch (ObjectNotFoundException)
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.NotFound;
                string message = String.Format("The track with the specified GUID '{0}' does not exist", guid);
                string responseJson = JsonConvert.SerializeObject(new ErrorResponse(message));
                return WebOperationContext.Current.CreateTextResponse(responseJson, "application/json", Encoding.UTF8);
            }
            catch (Exception)
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.InternalServerError;
                string message = String.Format("An internal server error occurred");
                string responseJson = JsonConvert.SerializeObject(new ErrorResponse(message));
                return WebOperationContext.Current.CreateTextResponse(responseJson, "application/json", Encoding.UTF8);
            }
        }

        #endregion

        #region Update Operations

        /// <summary>
        /// Attempts to replace the track with the given guid with a new stream
        /// </summary>
        /// <param name="file">The file stream that is to be replaced</param>
        /// <param name="trackGuid">The guid of the track to replace</param>
        /// <returns>A message representing the success or failure</returns>
        public Message ReplaceTrack(Stream file, string trackGuid)
        {
            try
            {
                MemoryStream memoryStream;
                if(WebOperationContext.Current.IncomingRequest.ContentType.StartsWith("multipart/form-data"))
                {
                    // Attempt to replace the track with the new file
                    MultipartParser parser = new MultipartParser(file);
                    if (parser.Success)
                    {
                        // Upload the track
                        memoryStream = new MemoryStream(parser.FileContents);
                    }
                    else
                    {
                        // Failure of one kind or another
                        WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                        ErrorResponse fResponse = new ErrorResponse("Failed to process request.");
                        string fResponseJson = JsonConvert.SerializeObject(fResponse);
                        return WebOperationContext.Current.CreateTextResponse(fResponseJson, "application/json",
                            Encoding.UTF8);
                    }
                } 
                else
                {
                    // There's no need to process out anything else. The body is the file.
                    memoryStream = new MemoryStream(ToByteArray(file));
                }
                
                // Replace the file
                string hash;
                Guid guid = Guid.Parse(trackGuid);
                TrackManager.ReplaceTrack(memoryStream, guid, out hash);
                WebResponse response = new UploadSuccessResponse(guid, hash);
                string responseJson = JsonConvert.SerializeObject(response);
                return WebOperationContext.Current.CreateTextResponse(responseJson, "application/json", Encoding.UTF8);
            }
            catch (FormatException)
            {
                // The guid was probably incorrect
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                string message = String.Format("The GUID supplied '{0}' is an invalid GUID.", trackGuid);
                string responseJson = JsonConvert.SerializeObject(new ErrorResponse(message));
                return WebOperationContext.Current.CreateTextResponse(responseJson, "application/json", Encoding.UTF8);
            }
            catch (ObjectNotFoundException)
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.NotFound;
                string message = String.Format("The track with the specified GUID '{0}' does not exist", trackGuid);
                string responseJson = JsonConvert.SerializeObject(new ErrorResponse(message));
                return WebOperationContext.Current.CreateTextResponse(responseJson, "application/json", Encoding.UTF8);
            }
            catch (Exception)
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.InternalServerError;
                string message = String.Format("An internal server error occurred");
                string responseJson = JsonConvert.SerializeObject(new ErrorResponse(message));
                return WebOperationContext.Current.CreateTextResponse(responseJson, "application/json", Encoding.UTF8);
            }
        }

        /// <summary>
        /// Replaces one or more metadata records for a given track
        /// </summary>
        /// <param name="body">The JSON passed to in as part of the POST request</param>
        /// <param name="guid">The track GUID passed in as part of the URI</param>
        /// <returns>A message for success or failure</returns>
        public Message ReplaceMetadata(Stream body, string guid)
        {
            try
            {
                // Translate the body and guid
                Guid trackGuid = Guid.Parse(guid);
                string bodyStr = Encoding.Default.GetString(ToByteArray(body));
                var metadata = JsonConvert.DeserializeObject<Dictionary<string, string>>(bodyStr);

                // Pass it along to the track manager
                TrackManager.ReplaceMetadata(trackGuid, metadata);

                // Sucess
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.OK;
                string responseJson = JsonConvert.SerializeObject(new WebResponse(WebResponse.StatusValue.Success));
                return WebOperationContext.Current.CreateTextResponse(responseJson, "application/json", Encoding.UTF8);
            }
            catch (FormatException)
            {
                // The guid was probably incorrect
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                string message = String.Format("The GUID supplied '{0}' is an invalid GUID.", guid);
                string responseJson = JsonConvert.SerializeObject(new ErrorResponse(message));
                return WebOperationContext.Current.CreateTextResponse(responseJson, "application/json", Encoding.UTF8);
            }
            catch (JsonReaderException)
            {
                // The json was formatted poorly
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                string responseJson = JsonConvert.SerializeObject(new ErrorResponse("The JSON is invalid."));
                return WebOperationContext.Current.CreateTextResponse(responseJson, "application/json", Encoding.UTF8);
            }
            catch (ObjectNotFoundException)
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.NotFound;
                string message = String.Format("The track with the specified GUID '{0}' does not exist", guid);
                string responseJson = JsonConvert.SerializeObject(new ErrorResponse(message));
                return WebOperationContext.Current.CreateTextResponse(responseJson, "application/json", Encoding.UTF8);
            }
            catch (Exception)
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.InternalServerError;
                string message = String.Format("An internal server error occurred");
                string responseJson = JsonConvert.SerializeObject(new ErrorResponse(message));
                return WebOperationContext.Current.CreateTextResponse(responseJson, "application/json", Encoding.UTF8);
            }
        }

        /// <summary>
        /// Replaces all metadata records for a given track. If a metadata field
        /// is not provided, then it will be deleted.
        /// </summary>
        /// <param name="body">The JSON passed to in as part of the POST request</param>
        /// <param name="guid">The track GUID passed in as part of the URI</param>
        /// <returns>A message for success or failure</returns>
        public Message ReplaceAllMetadata(Stream body, string guid)
        {
            try
            {
                // Translate the body and guid
                Guid trackGuid = Guid.Parse(guid);
                string bodyStr = Encoding.Default.GetString(ToByteArray(body));
                var metadata = JsonConvert.DeserializeObject<Dictionary<string, string>>(bodyStr);

                // Pass it along to the track manager
                TrackManager.ReplaceMetadata(trackGuid, metadata, true);

                // Sucess
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.OK;
                string responseJson = JsonConvert.SerializeObject(new WebResponse(WebResponse.StatusValue.Success));
                return WebOperationContext.Current.CreateTextResponse(responseJson, "application/json", Encoding.UTF8);
            }
            catch (FormatException)
            {
                // The guid was probably incorrect
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                string message = String.Format("The GUID supplied '{0}' is an invalid GUID.", guid);
                string responseJson = JsonConvert.SerializeObject(new ErrorResponse(message));
                return WebOperationContext.Current.CreateTextResponse(responseJson, "application/json", Encoding.UTF8);
            }
            catch (JsonReaderException)
            {
                // The json was formatted poorly
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                string responseJson = JsonConvert.SerializeObject(new ErrorResponse("The JSON is invalid."));
                return WebOperationContext.Current.CreateTextResponse(responseJson, "application/json", Encoding.UTF8);
            }
            catch (ObjectNotFoundException)
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.NotFound;
                string message = String.Format("The track with the specified GUID '{0}' does not exist", guid);
                string responseJson = JsonConvert.SerializeObject(new ErrorResponse(message));
                return WebOperationContext.Current.CreateTextResponse(responseJson, "application/json", Encoding.UTF8);
            }
            catch (Exception)
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.InternalServerError;
                string message = String.Format("An internal server error occurred");
                string responseJson = JsonConvert.SerializeObject(new ErrorResponse(message));
                return WebOperationContext.Current.CreateTextResponse(responseJson, "application/json", Encoding.UTF8);
            }
        }

        #endregion

        #region Deletion Operations

        /// <summary>
        /// Attempts to delete the track with the given GUID from the database
        /// </summary>
        /// <param name="guid">Guid of the track to delete.</param>
        /// <returns>A message with JSON formatted web message</returns>
        public Message DeleteTrack(string guid)
        {
            try
            {
                // Parse the guid into a Guid and attempt to delete
                TrackManager.DeleteTrack(Guid.Parse(guid));
                string responseJson = JsonConvert.SerializeObject(new WebResponse(WebResponse.StatusValue.Success));
                return WebOperationContext.Current.CreateTextResponse(responseJson, "application/json", Encoding.UTF8);
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

        #region Helper Methods
        private byte[] ToByteArray(Stream stream)
        {
            byte[] buffer = new byte[32768];
            using (MemoryStream ms = new MemoryStream())
            {
                while (true)
                {
                    int read = stream.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                        return ms.ToArray();
                    ms.Write(buffer, 0, read);
                }
            }
        }
        #endregion

        #endregion
    }
}
