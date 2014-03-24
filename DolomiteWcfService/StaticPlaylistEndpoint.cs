using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel.Channels;
using DolomiteModel.PublicRepresentations;
using DolomiteWcfService.Exceptions;
using DolomiteWcfService.Responses;
using Newtonsoft.Json;

namespace DolomiteWcfService
{
    class StaticPlaylistEndpoint : IStaticPlaylistEndpoint
    {

        #region Properties

        private PlaylistManager PlaylistManager { get; set; }

        private UserManager UserManager { get; set; }

        #endregion

        public StaticPlaylistEndpoint()
        {
            PlaylistManager = PlaylistManager.Instance;
            UserManager = UserManager.Instance;
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
                // Make sure the session is valid
                string api;
                string token = WebUtilities.GetDolomiteSessionToken(out api);
                string username = UserManager.GetUsernameFromSession(token, api);
                UserManager.ExtendIdleTimeout(token);

                // Process the object we're send
                string bodyStr = WebUtilities.GetUtf8String(body);
                Playlist playlist = JsonConvert.DeserializeObject<Playlist>(bodyStr);
                Guid id = PlaylistManager.CreateStaticPlaylist(playlist, username);

                return WebUtilities.GenerateResponse(new PlaylistCreationSuccessResponse(id), HttpStatusCode.Created);
            }
            catch (InvalidSessionException)
            {
                return WebUtilities.GenerateUnauthorizedResponse();
            }
            catch (UnauthorizedAccessException uae)
            {
                return WebUtilities.GenerateResponse(new ErrorResponse(uae.Message), HttpStatusCode.BadRequest);
            }
            catch (ObjectNotFoundException onfe)
            {
                string message = "The playlist could not be created: " + onfe.Message;
                return WebUtilities.GenerateResponse(new ErrorResponse(message), HttpStatusCode.BadRequest);
            }
            catch (JsonReaderException)
            {
                // The playlist object was probably incorrect
                return WebUtilities.GenerateResponse(new ErrorResponse("The JSON for the request is invalid."),
                    HttpStatusCode.BadRequest);
            }
            catch (JsonSerializationException)
            {
                return WebUtilities.GenerateResponse(new ErrorResponse("The JSON for the request is invalid."),
                    HttpStatusCode.BadRequest);
            }
            catch (DuplicateNameException ex)
            {
                // The name is a duplicate
                string message = String.Format("A static playlist with the name '{0}' already exists. " +
                                               "Please choose a different name, or delete the existing playlist.",
                                               ex.Message);
                return WebUtilities.GenerateResponse(new ErrorResponse(message), HttpStatusCode.Conflict);
            }
            catch (InvalidExpressionException iee)
            {
                // The rule is invalid
                object payload = new ErrorResponse("Could not add static playlist: " + iee.Message);
                return WebUtilities.GenerateResponse(payload, HttpStatusCode.BadRequest);
            }
            catch (Exception)
            {
                return WebUtilities.GenerateResponse(new ErrorResponse(WebUtilities.InternalServerMessage),
                    HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Fetches all the playlists from the database. This does not include
        /// their corresponding rules or tracks.
        /// </summary>
        /// <returns>A json seriailized version of the list of playlists</returns>
        public Message GetAllStaticPlaylists()
        {
            try
            {
                // Make sure we have a valid session
                string apiKey;
                string token = WebUtilities.GetDolomiteSessionToken(out apiKey);
                string username = UserManager.GetUsernameFromSession(token, apiKey);
                UserManager.ExtendIdleTimeout(token);

                List<Playlist> playlists = PlaylistManager.GetAllStaticPlaylists(username);
                return WebUtilities.GenerateResponse(playlists, HttpStatusCode.OK);
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
        /// Attempts to retrieve a specific static playlist from the database. This
        /// makes no differentiation about standard or auto playlists.
        /// </summary>
        /// <param name="guid">The guid of the static playlist to lookup</param>
        /// <returns>A json serialized version of the playlist</returns>
        public Message GetStaticPlaylist(string guid)
        {
            try
            {
                // Make sure we have a valid session
                string apiKey;
                string token = WebUtilities.GetDolomiteSessionToken(out apiKey);
                string username = UserManager.GetUsernameFromSession(token, apiKey);
                UserManager.ExtendIdleTimeout(token);

                // Parse the guid into a Guid and attempt to delete
                Playlist playlist = PlaylistManager.GetStaticPlaylist(Guid.Parse(guid), username);
                return WebUtilities.GenerateResponse(playlist, HttpStatusCode.OK);
            }
            catch (InvalidSessionException)
            {
                return WebUtilities.GenerateUnauthorizedResponse();
            }
            catch (UnauthorizedAccessException)
            {
                string message = String.Format("The GUID supplied '{0}' refers to a playlist that is not owned by you.", guid);
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
                string message = String.Format("The static playlist with the specified GUID '{0}' does not exist", guid);
                return WebUtilities.GenerateResponse(new ErrorResponse(message), HttpStatusCode.NotFound);
            }
            catch (Exception)
            {
                return WebUtilities.GenerateResponse(new ErrorResponse(WebUtilities.InternalServerMessage),
                    HttpStatusCode.InternalServerError);
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
                // Make sure we have a valid session
                string apiKey;
                string token = WebUtilities.GetDolomiteSessionToken(out apiKey);
                string username = UserManager.GetUsernameFromSession(token, apiKey);
                UserManager.ExtendIdleTimeout(token);

                // Read the body of the request and convert it to the guid of the track to add
                //TODO: Add support for batch adding tracks
                string bodyStr = WebUtilities.GetUtf8String(body);
                Guid trackGuid = Guid.Parse(bodyStr);
                Guid playlistId = Guid.Parse(guid);

                // See if a position was passed in as part of the request
                int position;
                if (WebUtilities.GetQueryParameters().Keys.Contains("position") &&
                    Int32.TryParse(WebUtilities.GetQueryParameters()["position"], out position))
                {
                    PlaylistManager.AddTrackToPlaylist(playlistId, trackGuid, username, position);
                }
                else
                {
                    PlaylistManager.AddTrackToPlaylist(playlistId, trackGuid, username);
                }

                // Send a happy return message
                return WebUtilities.GenerateResponse(new Response(Response.StatusValue.Success), HttpStatusCode.OK);
            }
            catch (InvalidSessionException)
            {
                return WebUtilities.GenerateUnauthorizedResponse();
            }
            catch (UnauthorizedAccessException)
            {
                string message = String.Format("The GUID supplied '{0}' refers to a playlist that is not owned by you.", guid);
                return WebUtilities.GenerateResponse(new ErrorResponse(message), HttpStatusCode.Forbidden);
            }
            catch (FormatException)
            {
                // The payload was not a rule
                const string message = "The body of the request was not a valid track guid";
                return WebUtilities.GenerateResponse(new ErrorResponse(message), HttpStatusCode.BadRequest);
            }
            catch (ObjectNotFoundException e)
            {
                // The type of the playlist was invalid
                return WebUtilities.GenerateResponse(new ErrorResponse(e.Message), HttpStatusCode.NotFound);
            }
            catch (InvalidExpressionException iee)
            {
                // The rule passed in was likely invalid
                return WebUtilities.GenerateResponse(new ErrorResponse(iee.Message), HttpStatusCode.BadRequest);
            }
            catch (Exception)
            {
                return WebUtilities.GenerateResponse(new ErrorResponse(WebUtilities.InternalServerMessage),
                    HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Deletes the given track from the given playlist. We also shuffle the
        /// track orders to make sure everything comes up Millhouse.
        /// </summary>
        /// <param name="playlist">The guid of the playlist to delete the track from</param>
        /// <param name="track">The id of the track to delete from the playlist</param>
        /// <returns>A success or error message</returns>
        public Message DeleteTrackFromStaticPlaylist(string playlist, string track)
        {
            try
            {
                // Make sure we have a valid session
                string apiKey;
                string token = WebUtilities.GetDolomiteSessionToken(out apiKey);
                string username = UserManager.GetUsernameFromSession(token, apiKey);
                UserManager.ExtendIdleTimeout(token);

                // Parse the guid
                Guid playlistGuid;
                int trackId;
                if (!Guid.TryParse(playlist, out playlistGuid))
                    throw new FormatException(String.Format("The playlist GUID supplied '{0}' is an invalid GUID.", playlistGuid));
                if (!Int32.TryParse(track, out trackId))
                    throw new FormatException(String.Format("The track ID supplied '{0}' is an invalid integer.", track));

                // Attempt to delete
                PlaylistManager.DeleteTrackFromStaticPlaylist(playlistGuid, username, trackId);
                return WebUtilities.GenerateResponse(new Response(Response.StatusValue.Success), HttpStatusCode.OK);
            }
            catch (InvalidSessionException)
            {
                return WebUtilities.GenerateUnauthorizedResponse();
            }
            catch (UnauthorizedAccessException)
            {
                string message = String.Format("The GUID supplied '{0}' refers to a playlist that is not owned by you.", playlist);
                return WebUtilities.GenerateResponse(new ErrorResponse(message), HttpStatusCode.Forbidden);
            }
            catch (FormatException fe)
            {
                return WebUtilities.GenerateResponse(new ErrorResponse(fe.Message), HttpStatusCode.BadRequest);
            }
            catch (ObjectNotFoundException)
            {
                return WebUtilities.GenerateResponse(new ErrorResponse("Failed to find track in playlist." +
                                                                       " Playlist may not exist."), HttpStatusCode.NotFound);
            }
            catch (Exception)
            {
                return WebUtilities.GenerateResponse(new ErrorResponse(WebUtilities.InternalServerMessage),
                    HttpStatusCode.InternalServerError);
            }
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
                // Make sure we have a valid session
                string apiKey;
                string token = WebUtilities.GetDolomiteSessionToken(out apiKey);
                string username = UserManager.GetUsernameFromSession(token, apiKey);
                UserManager.ExtendIdleTimeout(token);

                // Parse the guid into a Guid and attempt to delete
                PlaylistManager.DeleteStaticPlaylist(Guid.Parse(guid), username);
                return WebUtilities.GenerateResponse(new Response(Response.StatusValue.Success), HttpStatusCode.OK);
            }
            catch (InvalidSessionException)
            {
                return WebUtilities.GenerateUnauthorizedResponse();
            }
            catch (UnauthorizedAccessException)
            {
                string message = String.Format("The GUID supplied '{0}' refers to a playlist that is not owned by you.", guid);
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
                string message = String.Format("The static playlist with the specified GUID '{0}' does not exist", guid);
                return WebUtilities.GenerateResponse(new ErrorResponse(message), HttpStatusCode.NotFound);
            }
            catch (Exception)
            {
                return WebUtilities.GenerateResponse(new ErrorResponse(WebUtilities.InternalServerMessage),
                    HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Returns true just to allow the CORS preflight request via OPTIONS
        /// HTTP method to go through
        /// </summary>
        /// <returns>True</returns>
        public bool PreflyRequest()
        {
            return true;
        }
    }
}
