using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Net;
using System.ServiceModel.Channels;
using System.ServiceModel.Web;
using System.Text;
using DolomiteModel.PublicRepresentations;
using Newtonsoft.Json;

namespace DolomiteWcfService
{
    class StaticPlaylistEndpoint : IStaticPlaylistEndpoint
    {

        #region Properties

        private PlaylistManager PlaylistManager { get; set; }

        #endregion

        public StaticPlaylistEndpoint()
        {
            PlaylistManager = PlaylistManager.Instance;
        }

        /// <summary>
        /// Handles requests to create a new static playlist. Deserializes a static
        /// playlist object and feeds it to the playlist manager.
        /// </summary>
        /// <param name="body">The body of the request. Should be a static playlist object</param>
        /// <returns>A message of success or failure</returns>
        public Message CreateStaticPlaylist(Stream body)
        {
            try
            {
                // Process the object we're send
                string bodyStr = Encoding.Default.GetString(ToByteArray(body));
                AutoPlaylist autoPlaylist = JsonConvert.DeserializeObject<AutoPlaylist>(bodyStr);
                Guid id = PlaylistManager.CreateAutoPlaylist(autoPlaylist);

                string responseJson = JsonConvert.SerializeObject(new PlaylistCreationSuccessResponse(id));
                return WebOperationContext.Current.CreateTextResponse(responseJson, "application/json", Encoding.UTF8);
            }
            catch (JsonSerializationException)
            {
                // The guid was probably incorrect
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                string responseJson =
                    JsonConvert.SerializeObject(new ErrorResponse("The supplied static playlist object is invalid."));
                return WebOperationContext.Current.CreateTextResponse(responseJson, "application/json", Encoding.UTF8);
            }
            catch (DuplicateNameException ex)
            {
                // The name is a duplicate
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.Conflict;
                string message =
                    String.Format(
                        "A static playlist with the name '{0}' already exists. Please choose a different name, or delete the existing playlist.",
                        ex.Message);
                string responseJson = JsonConvert.SerializeObject(new ErrorResponse(message));
                return WebOperationContext.Current.CreateTextResponse(responseJson, "application/json", Encoding.UTF8);
            }
            catch (InvalidExpressionException iee)
            {
                // The rule is invalid
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                string responseJson =
                    JsonConvert.SerializeObject(new ErrorResponse("Could not add static playlist: " + iee.Message));
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
        /// Fetches all the playlists from the database. This does not include
        /// their corresponding rules or tracks.
        /// </summary>
        /// <returns>A json seriailized version of the list of playlists</returns>
        public Message GetAllStaticPlaylists()
        {
            List<Playlist> playlists = PlaylistManager.GetAllPlaylists();
            string playlistsJson = JsonConvert.SerializeObject(playlists);
            return WebOperationContext.Current.CreateTextResponse(playlistsJson, "application/json", Encoding.UTF8);
        }

        /// <summary>
        /// Attempts to retrieve a specific static playlist from the database. This
        /// makes no differentiation about standard or auto playlists.
        /// </summary>
        /// <param name="guid">The guid of the static playlist to lookup</param>
        /// <returns>A json serialized version of the playlist</returns>
        public Message GetStaticPlaylist(string guid)
        {
            try
            {
                // Parse the guid into a Guid and attempt to delete
                Playlist playlist = PlaylistManager.GetPlaylist(Guid.Parse(guid));
                string responseJson = JsonConvert.SerializeObject(playlist);
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.OK;
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
            catch (ObjectNotFoundException)
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.NotFound;
                string message = String.Format("The static playlist with the specified GUID '{0}' does not exist", guid);
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
        /// Attempts to add a track to a playlist. The track's GUID is the
        /// body of the request.
        /// </summary>
        /// <param name="body">
        /// The payload of the request. Must be a GUID
        /// </param>
        /// <param name="guid">The guid for the playlist to add the rule to</param>
        /// <returns>Message of success or failure.</returns>
        public Message AddTrackToStaticPlaylist(Stream body, string guid)
        {
            try
            {
                // Read the body of the request and convert it to the guid of the track to add
                //TODO: Add support for batch adding tracks
                string bodyStr = Encoding.Default.GetString(ToByteArray(body));
                Guid trackGuid = Guid.Parse(bodyStr);

                // Success! Now, add the track to the playlist
                Guid playlistId = Guid.Parse(guid);
                PlaylistManager.AddTrackToPlaylist(playlistId, trackGuid);

                // Send a happy return message
                string responseJson = JsonConvert.SerializeObject(new WebResponse(WebResponse.StatusValue.Success));
                return WebOperationContext.Current.CreateTextResponse(responseJson, "application/json", Encoding.UTF8);
            }
            catch (FormatException)
            {
                // The payload was not a rule
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                string responseJson =
                    JsonConvert.SerializeObject(
                        new ErrorResponse("The body of the request was not a valid track guid"));
                return WebOperationContext.Current.CreateTextResponse(responseJson, "application/json", Encoding.UTF8);
            }
            catch (ObjectNotFoundException e)
            {
                // The type of the playlist was invalid
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.NotFound;
                string message = e.Message;
                string responseJson = JsonConvert.SerializeObject(new ErrorResponse(message));
                return WebOperationContext.Current.CreateTextResponse(responseJson, "application/json", Encoding.UTF8);
            }
            catch (InvalidExpressionException iee)
            {
                // The rule passed in was likely invalid
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                string responseJson = JsonConvert.SerializeObject(new ErrorResponse(iee.Message));
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

        public Message DeleteTrackFromStaticPlaylist(string guid, string id)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Attempts to delete a static playlist with the given guid
        /// </summary>
        /// <param name="guid">Guid of the static playlist to delete</param>
        /// <returns>Success or error message</returns>
        public Message DeleteStaticPlaylist(string guid)
        {
            try
            {
                // Parse the guid into a Guid and attempt to delete
                PlaylistManager.DeletePlaylist(Guid.Parse(guid));
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
                string message = String.Format("The static playlist with the specified GUID '{0}' does not exist", guid);
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
    }
}
