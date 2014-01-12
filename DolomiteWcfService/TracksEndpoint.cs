using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Net;
using System.ServiceModel.Channels;
using System.Text;
using DolomiteModel.PublicRepresentations;
using DolomiteWcfService.Exceptions;
using DolomiteWcfService.Responses;
using Newtonsoft.Json;
using AntsCode.Util;
using TagLib;

namespace DolomiteWcfService
{
    public class TracksEndpoint : ITracksEndpoint
    {

        #region Properties

        /// <summary>
        /// Instance of the Track Manager
        /// </summary>
        private TrackManager TrackManager { get; set; }

        /// <summary>
        /// Instance of the User Manager
        /// </summary>
        private UserManager UserManager { get; set; }

        #endregion

        public TracksEndpoint()
        {
            // Initialize the track and user manager
            TrackManager = TrackManager.Instance;
            UserManager = UserManager.Instance;
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
                // Step 0: Make sure the session is valid
                string api;
                string token = WebUtilities.GetDolomiteSessionToken(out api);
                string username = UserManager.GetUsernameFromSession(token, api);
                UserManager.ExtendIdleTimeout(token);

                // Step 1: Read the request body
                MemoryStream memoryStream;
                if (WebUtilities.GetContentType().StartsWith("multipart/form-data"))
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
                        return WebUtilities.GenerateResponse(new ErrorResponse("Failed to process request."),
                            HttpStatusCode.BadRequest);
                    }
                }
                else
                {
                    // There's no need to process out anything else. The body is the file.
                    memoryStream = new MemoryStream(file.ToByteArray());
                }

                // Upload the track
                Guid guid;
                string hash;
                TrackManager.UploadTrack(memoryStream, username, out guid, out hash);
                return WebUtilities.GenerateResponse(new UploadSuccessResponse(guid, hash), HttpStatusCode.Created);
            }
            catch (InvalidSessionException)
            {
                return WebUtilities.GenerateUnauthorizedResponse();
            }
            catch (DuplicateNameException)
            {
                ErrorResponse eResponse = new ErrorResponse("The request could not be completed. A track with the same hash already exists." +
                                                            " Duplicate tracks are not permitted");
                return WebUtilities.GenerateResponse(eResponse, HttpStatusCode.Conflict);
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
                // Are we fetching an art object?
                if (quality == TrackManager.ArtDirectory)
                {
                    // Fetch the art with the given GUID
                    string artMime;
                    Stream artStream = TrackManager.GetTrackArt(Guid.Parse(guid), out artMime);
                    
                    // Set the headers
                    string contentDisp = String.Format("attachment; filename=\"{0}\";", guid);
                    WebUtilities.SetStatusCode(HttpStatusCode.OK);
                    WebUtilities.SetHeader("Content-Disposition", contentDisp);
                    WebUtilities.SetHeader(HttpResponseHeader.ContentType, artMime);

                    return artStream;
                }

                // Retrieve the track and return the stream
                Track track = TrackManager.GetTrack(Guid.Parse(guid));
                Track.Quality qualityObj = TrackManager.GetTrackStream(track, quality);

                // Set special headers that tell the client to download the file
                // and what filename to give it
                string disposition = String.Format("attachment; filename=\"{0}.{1}\";", guid, qualityObj.Extension);
                WebUtilities.SetStatusCode(HttpStatusCode.OK);
                WebUtilities.SetHeader("Content-Disposition", disposition);
                WebUtilities.SetHeader(HttpResponseHeader.ContentType, qualityObj.Mimetype);
                return qualityObj.FileStream;
            }
            catch (FormatException)
            {
                WebUtilities.SetStatusCode(HttpStatusCode.BadRequest);
            }
            catch (UnsupportedFormatException)
            {
                WebUtilities.SetStatusCode(HttpStatusCode.NotFound);
            }
            catch (ObjectNotFoundException)
            {
                WebUtilities.SetStatusCode(HttpStatusCode.NotFound);
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
            // Retrieve the track without the stream
            List<Track> tracks = TrackManager.FetchAllTracks();
            return WebUtilities.GenerateResponse(tracks, HttpStatusCode.OK);
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
                return WebUtilities.GenerateResponse(track, HttpStatusCode.OK);
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
                if(WebUtilities.GetContentType().StartsWith("multipart/form-data"))
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
                        return WebUtilities.GenerateResponse(new ErrorResponse("Failed to process request."),
                            HttpStatusCode.BadRequest);
                    }
                } 
                else
                {
                    // There's no need to process out anything else. The body is the file.
                    memoryStream = new MemoryStream(file.ToByteArray());
                }
                
                // Replace the file
                string hash;
                Guid guid = Guid.Parse(trackGuid);
                TrackManager.ReplaceTrack(memoryStream, guid, out hash);
                return WebUtilities.GenerateResponse(new UploadSuccessResponse(guid, hash), HttpStatusCode.OK);
            }
            catch (FormatException)
            {
                // The guid was probably incorrect
                string message = String.Format("The GUID supplied '{0}' is an invalid GUID.", trackGuid);
                return WebUtilities.GenerateResponse(new ErrorResponse(message), HttpStatusCode.BadRequest);
            }
            catch (ObjectNotFoundException)
            {
                string message = String.Format("The track with the specified GUID '{0}' does not exist", trackGuid);
                return WebUtilities.GenerateResponse(new ErrorResponse(message), HttpStatusCode.NotFound);
            }
            catch (Exception)
            {
                return WebUtilities.GenerateResponse(new ErrorResponse(WebUtilities.InternalServerMessage),
                    HttpStatusCode.InternalServerError);
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
                string bodyStr = Encoding.Default.GetString(body.ToByteArray());
                var metadata = JsonConvert.DeserializeObject<Dictionary<string, string>>(bodyStr);

                // Pass it along to the track manager
                TrackManager.ReplaceMetadata(trackGuid, metadata);

                // Sucess
                return WebUtilities.GenerateResponse(new Response(Response.StatusValue.Success), HttpStatusCode.OK);
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
                // Translate the body and guid
                Guid trackGuid = Guid.Parse(guid);
                string bodyStr = Encoding.Default.GetString(body.ToByteArray());
                var metadata = JsonConvert.DeserializeObject<Dictionary<string, string>>(bodyStr);

                // Pass it along to the track manager
                TrackManager.ReplaceMetadata(trackGuid, metadata, true);

                // Sucess
                return WebUtilities.GenerateResponse(new Response(Response.StatusValue.Success), HttpStatusCode.OK);
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
                // Translate the body and guid
                Guid trackGuid = Guid.Parse(guid);

                MemoryStream memoryStream;
                if (WebUtilities.GetContentLength() == 0)
                {
                    memoryStream = new MemoryStream();
                }
                else
                {
                    if (WebUtilities.GetContentType().StartsWith("multipart/form-data"))
                    {
                        // Attempt to replace the track with the new file
                        MultipartParser parser = new MultipartParser(body);
                        if (parser.Success)
                        {
                            // Upload the track
                            memoryStream = new MemoryStream(parser.FileContents);
                        }
                        else
                        {
                            // Failure of one kind or another
                            return WebUtilities.GenerateResponse(new ErrorResponse("Failed to process request."),
                                HttpStatusCode.BadRequest);
                        }
                    }
                    else
                    {
                        // There's no need to process out anything else. The body is the file.
                        memoryStream = new MemoryStream(body.ToByteArray());
                    }
                }

                // Pass it along to the track manager
                TrackManager.ReplaceTrackArt(trackGuid, memoryStream);

                // Sucess
                return WebUtilities.GenerateResponse(new Response(Response.StatusValue.Success), HttpStatusCode.OK);
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
                // Parse the guid into a Guid and attempt to delete
                TrackManager.DeleteTrack(Guid.Parse(guid));
                return WebUtilities.GenerateResponse(new Response(Response.StatusValue.Success), HttpStatusCode.OK);
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

        #endregion
    }
}
