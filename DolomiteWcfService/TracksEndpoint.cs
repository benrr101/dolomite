using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity.Core;
using System.IO;
using System.Net;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using DolomiteManagement;
using DolomiteManagement.Exceptions;
using DolomiteModel.PublicRepresentations;
using DolomiteWcfService.Requests;
using DolomiteWcfService.Responses;
using Newtonsoft.Json;

namespace DolomiteWcfService
{
    public class TracksEndpoint : ITracksEndpoint
    {

        #region Properties

        /// <summary>
        /// Instance of the Track Manager
        /// </summary>
        private static TrackManager TrackManager { get; set; }

        /// <summary>
        /// Instance of the User Manager
        /// </summary>
        private static UserManager UserManager { get; set; }

        private readonly WebUtilities _webUtilities = new WebUtilities();
        private WebUtilities WebUtilities { get { return _webUtilities; } }

        #endregion

        static TracksEndpoint()
        {
            // Initialize the track and user manager
            TrackManager = TrackManager.Instance;
            UserManager = UserManager.Instance;
        }

        #region ITracksEndpoint Operations

        #region Create Operations

        /// <summary>
        /// Takes in a stream for a file upload, if the track exists, we assume we can replace it.
        /// If the track doesn't exist, we assume it's a new upload.
        /// </summary>
        /// <remarks>
        /// If a track with the given guid exists and is owned by a different user, then the track
        /// will fail to upload at a later step in the process.
        /// </remarks>
        /// <param name="file">Stream of the file that is uploaded</param>
        /// <param name="guid">The GUID for identifying the track. Provided by the client.</param>
        /// <param name="providedHash">The MD5 hash provided by the client. Optional.</param>
        /// <returns>The GUID of the track that was uploaded</returns>
        public async Task<Message> UploadTrack(Stream file, string guid, string providedHash)
        {
            try
            {
                // Step 0.1: Make sure the session is valid
                UserSession sesh = WebUtilities.GetDolomiteSessionToken();
                string username = UserManager.GetUsernameFromSession(sesh.Token, sesh.ApiKey);
                UserManager.ExtendIdleTimeout(sesh.Token);

                // Step 0.2: Make sure the guid is valid
                Guid trackGuid;
                if (!Guid.TryParse(guid, out trackGuid) && trackGuid != Guid.Empty)
                {
                    throw new ArgumentException("A valid GUID must be provided as the ID for the track.");
                }

                // Step 0.3: Make sure the provided hash exists
                if (string.IsNullOrWhiteSpace(providedHash))
                {
                    throw new ArgumentException("A valid MD5 hash of the uploaded file must be provided.");
                }

                // Step 1: Read the request body into the temporary storage
                await LocalStorageManager.Instance.StoreStreamAsync(file, trackGuid.ToString());

                // Step 2: Calculate the hash of the file in temporary storage and compare to the
                // provided hash to validate the track
                string calculatedHash = await LocalStorageManager.Instance.CalculateMd5HashAsync(trackGuid.ToString());
                if (!String.Equals(calculatedHash, providedHash, StringComparison.OrdinalIgnoreCase))
                {
                    LocalStorageManager.Instance.DeleteFile(trackGuid.ToString());
                    throw new FileLoadException();
                }

                // Step 3: Triage the upload and kick off an async upload process
                HttpStatusCode returnCode;
                switch (await TrackManager.Instance.TriageUpload(trackGuid, username))
                {
                    case TrackUploadType.NewUpload:
                        returnCode = HttpStatusCode.Created;
                        TrackManager.Instance.UploadTrack(username, trackGuid);
                        break;
                    case TrackUploadType.Replace:
                        returnCode = HttpStatusCode.OK;
                        break;
                    default:
                        throw new Exception("Upload triage returned an invalid enum value.");
                }

                return WebUtilities.GenerateResponse(new UploadSuccessResponse(trackGuid), returnCode);
            }
            catch (InvalidSessionException)
            {
                return WebUtilities.GenerateUnauthorizedResponse();
            }
            catch (MissingFieldException mfe)
            {
                ErrorResponse eResponse = new ErrorResponse(mfe.Message);
                return WebUtilities.GenerateResponse(eResponse, HttpStatusCode.BadRequest);
            }
            catch (ArgumentException ae)
            {
                ErrorResponse eResponse = new ErrorResponse(ae.Message);
                return WebUtilities.GenerateResponse(eResponse, HttpStatusCode.BadRequest);
            }
            catch (DuplicateNameException)
            {
                ErrorResponse eResponse = new ErrorResponse(
                    "The request could not be completed. A track with the same hash already exists." 
                    + " Duplicate tracks are not permitted");
                return WebUtilities.GenerateResponse(eResponse, HttpStatusCode.Conflict);
            }
            catch (FileLoadException)
            {
                ErrorResponse eResponse = new ErrorResponse(
                    "The provided MD5 hash does not match the calculated MD5 hash."
                    + " The file may have been corrupted during upload.");
                return WebUtilities.GenerateResponse(eResponse, HttpStatusCode.BadRequest);
            }
            catch (Exception)
            {
                return WebUtilities.GenerateResponse(new ErrorResponse(WebUtilities.InternalServerMessage),
                    HttpStatusCode.InternalServerError);
            }
        }

        #endregion

        #region Retrieval Operations

        /// <summary>
        /// Downloads the given track's file stream from azure. Also handles
        /// downloading the art for a given track.
        /// </summary>
        /// <param name="quality">The name of the quality to download</param>
        /// <param name="guid">The hash for the track</param>
        /// <returns>A stream with the track from azure</returns>
        public Stream DownloadTrack(string quality, string guid)
        {
            try
            {
                // Art retrieval does not need authentication, since it's shared between users
                // Are we fetching an art object?
                if (quality == TrackManager.ArtDirectory)
                {
                    // Fetch the art with the given GUID
                    Art artObj = TrackManager.GetTrackArt(Guid.Parse(guid));

                    // Set the headers
                    string contentDisp = String.Format("attachment; filename=\"{0}\";", guid);
                    WebUtilities.SetStatusCode(HttpStatusCode.OK);
                    WebUtilities.SetHeader("Content-Disposition", contentDisp);
                    WebUtilities.SetHeader(HttpResponseHeader.ContentType, artObj.Mimetype);

                    return artObj.ArtStream;
                }

                // We're not retrieving art, so the user must be authenticated
                UserSession sesh = WebUtilities.GetDolomiteSessionToken();
                string username = UserManager.GetUsernameFromSession(sesh.Token, sesh.ApiKey);
                UserManager.ExtendIdleTimeout(sesh.Token);

                // Retrieve the track and return the stream
                Track track = TrackManager.GetTrack(Guid.Parse(guid), username);
                if (!track.Ready)
                    throw new TrackNotReadyException();
                Track.Quality qualityObj = TrackManager.GetTrackStream(track.Id, quality, username);

                // Set special headers that tell the client to download the file
                // and what filename to give it
                string disposition = String.Format("attachment; filename=\"{0}.{1}\";", guid, qualityObj.Extension);
                WebUtilities.SetStatusCode(HttpStatusCode.OK);
                WebUtilities.SetHeader("Content-Disposition", disposition);
                WebUtilities.SetHeader(HttpResponseHeader.ContentType, qualityObj.Mimetype);
                return qualityObj.FileStream;
            }
            catch (UnauthorizedAccessException)
            {
                WebUtilities.SetStatusCode(HttpStatusCode.Forbidden);
            }
            catch (InvalidSessionException)
            {
                WebUtilities.GenerateUnauthorizedResponse();
            }
            catch (FormatException)
            {
                WebUtilities.SetStatusCode(HttpStatusCode.BadRequest);
            }
            catch (ObjectNotFoundException)
            {
                WebUtilities.SetStatusCode(HttpStatusCode.NotFound);
            }
            catch (FileNotFoundException)
            {
                WebUtilities.SetStatusCode(HttpStatusCode.NotFound);
            }
            catch (TrackNotReadyException)
            {
                WebUtilities.SetStatusCode(HttpStatusCode.ServiceUnavailable);
                WebUtilities.SetHeader(HttpResponseHeader.RetryAfter, "60");
            }
            catch (Exception)
            {
                WebUtilities.SetStatusCode(HttpStatusCode.InternalServerError);
            }

            return null;
        }

        /// <summary>
        /// Retrieves all the track objects in the database. These are not linked
        /// to any azure blob streams and only contain metadata.
        /// </summary>
        /// <returns>List of track objects from the track manager</returns>
        public Message GetAllTracks()
        {
            try
            {
                // Make sure we have a valid session
                UserSession sesh = WebUtilities.GetDolomiteSessionToken();
                string username = UserManager.GetUsernameFromSession(sesh.Token, sesh.ApiKey);
                UserManager.ExtendIdleTimeout(sesh.Token);

                // See if there are search query parameters included
                Dictionary<string, string> queryParams = WebUtilities.GetQueryParameters();
                if (queryParams.Count > 0)
                    return SearchTracks(username, queryParams);

                // Retrieve the track without the stream
                List<Track> tracks = TrackManager.FetchAllTracksByOwner(username);
                return WebUtilities.GenerateResponse(tracks, HttpStatusCode.OK);
            }
            catch (InvalidSessionException)
            {
                return WebUtilities.GenerateUnauthorizedResponse();
            }
            catch (Exception)
            {
                return WebUtilities.GenerateResponse(new ErrorResponse(WebUtilities.InternalServerMessage),
                    HttpStatusCode.InternalServerError);
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
                // Make sure we have a valid session
                UserSession sesh = WebUtilities.GetDolomiteSessionToken();
                string username = UserManager.GetUsernameFromSession(sesh.Token, sesh.ApiKey);
                UserManager.ExtendIdleTimeout(sesh.Token);

                // Retrieve the track without the stream
                Track track = TrackManager.GetTrack(Guid.Parse(guid), username);
                if (!track.Ready)
                    throw new TrackNotReadyException();
                return WebUtilities.GenerateResponse(track, HttpStatusCode.OK);
            }
            catch (InvalidSessionException)
            {
                return WebUtilities.GenerateUnauthorizedResponse();
            }
            catch (UnauthorizedAccessException)
            {
                string message = String.Format("The GUID supplied '{0}' refers to a track that is not owned by you.", guid);
                return WebUtilities.GenerateResponse(new ErrorResponse(message), HttpStatusCode.Forbidden);
            }
            catch (FormatException)
            {
                // The guid was probably incorrect
                string message = String.Format("The GUID supplied '{0}' is an invalid GUID.", guid);
                return WebUtilities.GenerateResponse(new ErrorResponse(message), HttpStatusCode.BadRequest);
            }
            catch (ObjectNotFoundException)
            {
                string message = String.Format("The track with the specified GUID '{0}' does not exist", guid);
                return WebUtilities.GenerateResponse(new ErrorResponse(message), HttpStatusCode.NotFound);
            }
            catch (TrackNotReadyException)
            {
                const string message = "The track is not ready, yet. Please try again later.";
                WebUtilities.SetHeader(HttpResponseHeader.RetryAfter, "60");
                return WebUtilities.GenerateResponse(new ErrorResponse(message), HttpStatusCode.ServiceUnavailable);
            }
            catch (Exception)
            {
                return WebUtilities.GenerateResponse(new ErrorResponse(WebUtilities.InternalServerMessage),
                    HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Searches for a track using the search criteria from the url query parameters
        /// </summary>
        /// <param name="username">The username of the track owner</param>
        /// <param name="queryParameters">The search criteria from the uri query params</param>
        /// <returns>A message suitable for sending out over the wire</returns>
        private Message SearchTracks(string username, Dictionary<string, string> queryParameters)
        {
            List<Guid> tracks = TrackManager.SearchTracks(username, queryParameters);
            return WebUtilities.GenerateResponse(tracks, HttpStatusCode.OK);
        }

        #endregion

        #region Update Operations

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
                // Make sure we have a valid session
                UserSession sesh = WebUtilities.GetDolomiteSessionToken();
                string username = UserManager.GetUsernameFromSession(sesh.Token, sesh.ApiKey);
                UserManager.ExtendIdleTimeout(sesh.Token);

                // Translate the body and guid
                Guid trackGuid = Guid.Parse(guid);
                string bodyStr = WebUtilities.GetUtf8String(body);
                var metadata = JsonConvert.DeserializeObject<Dictionary<string, string>>(bodyStr);

                // Pass it along to the track manager
                TrackManager.ReplaceMetadata(trackGuid, username, metadata);

                // Sucess
                return WebUtilities.GenerateResponse(new Response(Response.StatusValue.Success), HttpStatusCode.OK);
            }
            catch (InvalidSessionException)
            {
                return WebUtilities.GenerateUnauthorizedResponse();
            }
            catch (UnauthorizedAccessException)
            {
                string message = String.Format("The GUID supplied '{0}' refers to a track that is not owned by you.",
                    guid);
                return WebUtilities.GenerateResponse(new ErrorResponse(message), HttpStatusCode.Forbidden);
            }
            catch (FormatException)
            {
                // The guid was probably incorrect
                string message = String.Format("The GUID supplied '{0}' is an invalid GUID.", guid);
                return WebUtilities.GenerateResponse(new ErrorResponse(message), HttpStatusCode.BadRequest);
            }
            catch (JsonReaderException)
            {
                // The json was formatted poorly
                return WebUtilities.GenerateResponse(new ErrorResponse("The JSON is invalid."),
                    HttpStatusCode.BadRequest);
            }
            catch (JsonSerializationException)
            {
                return WebUtilities.GenerateResponse(new ErrorResponse("The JSON is invalid."),
                    HttpStatusCode.BadRequest);
            }
            catch (ObjectNotFoundException)
            {
                string message = String.Format("The track with the specified GUID '{0}' does not exist", guid);
                return WebUtilities.GenerateResponse(new ErrorResponse(message), HttpStatusCode.NotFound);
            }
            catch (Exception)
            {
                return WebUtilities.GenerateResponse(new ErrorResponse(WebUtilities.InternalServerMessage),
                    HttpStatusCode.InternalServerError);
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
                // Make sure we have a valid session
                UserSession sesh = WebUtilities.GetDolomiteSessionToken();
                string username = UserManager.GetUsernameFromSession(sesh.Token, sesh.ApiKey);
                UserManager.ExtendIdleTimeout(sesh.Token);

                // Translate the body and guid
                Guid trackGuid = Guid.Parse(guid);
                string bodyStr = WebUtilities.GetUtf8String(body);
                var metadata = JsonConvert.DeserializeObject<Dictionary<string, string>>(bodyStr);

                // Pass it along to the track manager
                TrackManager.ReplaceMetadata(trackGuid, username, metadata, true);

                // Sucess
                return WebUtilities.GenerateResponse(new Response(Response.StatusValue.Success), HttpStatusCode.OK);
            }
            catch (InvalidSessionException)
            {
                return WebUtilities.GenerateUnauthorizedResponse();
            }
            catch (UnauthorizedAccessException)
            {
                string message = String.Format("The GUID supplied '{0}' refers to a track that is not owned by you.",
                    guid);
                return WebUtilities.GenerateResponse(new ErrorResponse(message), HttpStatusCode.Forbidden);
            }
            catch (FormatException)
            {
                // The guid was probably incorrect
                string message = String.Format("The GUID supplied '{0}' is an invalid GUID.", guid);
                return WebUtilities.GenerateResponse(new ErrorResponse(message), HttpStatusCode.BadRequest);
            }
            catch (JsonReaderException)
            {
                // The json was formatted poorly
                return WebUtilities.GenerateResponse(new ErrorResponse("The JSON is invalid."),
                    HttpStatusCode.BadRequest);
            }
            catch (JsonSerializationException)
            {
                return WebUtilities.GenerateResponse(new ErrorResponse("The JSON is invalid."),
                    HttpStatusCode.BadRequest);
            }
            catch (ObjectNotFoundException)
            {
                string message = String.Format("The track with the specified GUID '{0}' does not exist", guid);
                return WebUtilities.GenerateResponse(new ErrorResponse(message), HttpStatusCode.NotFound);
            }
            catch (Exception)
            {
                return WebUtilities.GenerateResponse(new ErrorResponse(WebUtilities.InternalServerMessage),
                    HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Replaces the given track's art with the given stream.
        /// </summary>
        /// <param name="body">A stream of the art file that is being uploaded</param>
        /// <param name="guid">The guid of the track to replace the art for</param>
        /// <returns>Message of the status of the request</returns>
        public Message ReplaceTrackArt(Stream body, string guid)
        {
            try
            {
                // Make sure we have a valid session
                //UserSession sesh = WebUtilities.GetDolomiteSessionToken();
                //string username = UserManager.GetUsernameFromSession(sesh.Token, sesh.ApiKey);
                //UserManager.ExtendIdleTimeout(sesh.Token);

                //// Translate the body and guid
                //Guid trackGuid = Guid.Parse(guid);

                //MemoryStream memoryStream;
                //if (WebUtilities.GetContentLength() == 0)
                //{
                //    // This allows the art file to be deleted
                //    memoryStream = new MemoryStream();
                //}
                //else
                //{
                //    if (WebUtilities.GetContentType() == null)
                //        throw new MissingFieldException("The content type header is missing from the request.");
                //    if (WebUtilities.GetContentType().StartsWith("multipart/form-data"))
                //    {
                //        // Attempt to replace the track with the new file
                //        MultipartParser parser = new MultipartParser(body);
                //        if (parser.Success)
                //        {
                //            // Upload the track
                //            memoryStream = new MemoryStream(parser.FileContents);
                //        }
                //        else
                //        {
                //            // Failure of one kind or another
                //            return WebUtilities.GenerateResponse(new ErrorResponse("Failed to process request."),
                //                HttpStatusCode.BadRequest);
                //        }
                //    }
                //    else
                //    {
                //        // There's no need to process out anything else. The body is the file.
                //        memoryStream = new MemoryStream(body.ToByteArray());
                //    }
                //}

                //// Pass it along to the track manager
                //TrackManager.ReplaceTrackArt(trackGuid, username, memoryStream);

                //// Sucess
                //return WebUtilities.GenerateResponse(new Response(Response.StatusValue.Success), HttpStatusCode.OK);
                throw new NotImplementedException();
            }
            catch (InvalidSessionException)
            {
                return WebUtilities.GenerateUnauthorizedResponse();
            }
            catch (MissingFieldException mfe)
            {
                ErrorResponse eResponse = new ErrorResponse(mfe.Message);
                return WebUtilities.GenerateResponse(eResponse, HttpStatusCode.BadRequest);
            }
            catch (UnauthorizedAccessException)
            {
                string message = String.Format("The GUID supplied '{0}' refers to a track that is not owned by you.", guid);
                return WebUtilities.GenerateResponse(new ErrorResponse(message), HttpStatusCode.Forbidden);
            }
            catch (FormatException)
            {
                // The guid was probably incorrect
                string message = String.Format("The GUID supplied '{0}' is an invalid GUID.", guid);
                return WebUtilities.GenerateResponse(new ErrorResponse(message), HttpStatusCode.BadRequest);
            }
            catch (ObjectNotFoundException)
            {
                string message = String.Format("The track with the specified GUID '{0}' does not exist", guid);
                return WebUtilities.GenerateResponse(new ErrorResponse(message), HttpStatusCode.NotFound);
            }
            catch (Exception)
            {
                return WebUtilities.GenerateResponse(new ErrorResponse(WebUtilities.InternalServerMessage),
                    HttpStatusCode.InternalServerError);
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
                // Make sure we have a valid session
                UserSession sesh = WebUtilities.GetDolomiteSessionToken();
                string username = UserManager.GetUsernameFromSession(sesh.Token, sesh.ApiKey);
                UserManager.ExtendIdleTimeout(sesh.Token);

                // Parse the guid into a Guid and attempt to delete
                TrackManager.DeleteTrack(Guid.Parse(guid), username);
                return WebUtilities.GenerateResponse(new Response(Response.StatusValue.Success), HttpStatusCode.OK);
            }
            catch (InvalidSessionException)
            {
                return WebUtilities.GenerateUnauthorizedResponse();
            }
            catch (UnauthorizedAccessException)
            {
                string message = String.Format("The GUID supplied '{0}' refers to a track that is not owned by you.", guid);
                return WebUtilities.GenerateResponse(new ErrorResponse(message), HttpStatusCode.Forbidden);
            }
            catch (FormatException)
            {
                // The guid was probably incorrect
                string message = String.Format("The GUID supplied '{0}' is an invalid GUID.", guid);
                return WebUtilities.GenerateResponse(new ErrorResponse(message), HttpStatusCode.BadRequest);
            }
            catch (FileNotFoundException)
            {
                string message = String.Format("The track with the specified GUID '{0}' does not exist", guid);
                return WebUtilities.GenerateResponse(new ErrorResponse(message), HttpStatusCode.NotFound);
            }
        }

        #endregion

        /// <summary>
        /// Returns true just to allow the CORS preflight request via OPTIONS
        /// HTTP method to go through
        /// </summary>
        /// <returns>True</returns>
        public bool PreflyRequest()
        {
            return true;
        }

        #endregion
    }
}
