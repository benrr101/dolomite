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

namespace DolomiteWcfService
{
    class AutoPlaylistEndpoint : IAutoPlaylistEndpoint
    {

        #region Properties

        private PlaylistManager PlaylistManager { get; set; }

        private UserManager UserManager { get; set; }

        #endregion

        public AutoPlaylistEndpoint()
        {
            PlaylistManager = PlaylistManager.Instance;
            UserManager = UserManager.Instance;
        }

        /// <summary>
        /// Handles requests to create a new auto playlist. Deserializes an auto
        /// playlist object and feeds it to the playlist manager.
        /// </summary>
        /// <param name="body">The body of the request. Should be an autoplaylist object</param>
        /// <returns>A message of success or failure</returns>
        public Message CreateAutoPlaylist(Stream body)
        {
            try
            {
                // Make sure the session is valid
                string api;
                string token = WebUtilities.GetDolomiteSessionToken(out api);
                string username = UserManager.GetUsernameFromSession(token, api);
                UserManager.ExtendIdleTimeout(token);

                // Process the object we're send
                string bodyStr = Encoding.Default.GetString(body.ToByteArray());
                AutoPlaylist playlist = JsonConvert.DeserializeObject<AutoPlaylist>(bodyStr);

                // Determine what type of processing to do
                Guid id = PlaylistManager.CreateAutoPlaylist(playlist, username);

                return WebUtilities.GenerateResponse(new PlaylistCreationSuccessResponse(id), HttpStatusCode.Created);
            }
            catch (InvalidSessionException)
            {
                return WebUtilities.GenerateUnauthorizedResponse();
            }
            catch (JsonReaderException)
            {
                // The guid was probably incorrect
                object payload = new ErrorResponse("The supplied autoplaylist object is invalid.");
                return WebUtilities.GenerateResponse(payload, HttpStatusCode.BadRequest);
            }
            catch (DuplicateNameException ex)
            {
                // The name is a duplicate
                string message = String.Format("An autoplaylist with the name '{0}' already exists. " +
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
        public Message GetAllAutoPlaylists()
        {
            try
            {
                // Make sure we have a valid session
                string apiKey;
                string token = WebUtilities.GetDolomiteSessionToken(out apiKey);
                string username = UserManager.GetUsernameFromSession(token, apiKey);
                UserManager.ExtendIdleTimeout(token);

                List<Playlist> playlists = PlaylistManager.GetAllAutoPlaylists(username);
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
        /// Attempts to retrieve a specific auto playlist from the database.
        /// </summary>
        /// <param name="guid">The guid of the auto playlist to lookup</param>
        /// <returns>A json serialized version of the playlist</returns>
        public Message GetAutoPlaylist(string guid)
        {
            try
            {
                // Make sure we have a valid session
                string apiKey;
                string token = WebUtilities.GetDolomiteSessionToken(out apiKey);
                string username = UserManager.GetUsernameFromSession(token, apiKey);
                UserManager.ExtendIdleTimeout(token);

                // Parse the guid into a Guid and attempt to delete
                Playlist playlist = PlaylistManager.GetAutoPlaylist(Guid.Parse(guid), username);
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
                string message = String.Format("The autoplaylist with the specified GUID '{0}' does not exist", guid);
                return WebUtilities.GenerateResponse(new ErrorResponse(message), HttpStatusCode.NotFound);
            }
            catch (Exception)
            {
                return WebUtilities.GenerateResponse(new ErrorResponse(WebUtilities.InternalServerMessage),
                    HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Attempts to add a rule to an autoplaylist. The rule object is the
        /// body of the request.
        /// </summary>
        /// <param name="body">
        /// The payload of the request. Must be a AutoPlaylistRule object.
        /// </param>
        /// <param name="guid">The guid for the playlist to add the rule to</param>
        /// <returns>Message of success or failure.</returns>
        public Message AddRuleToAutoPlaylist(Stream body, string guid)
        {
            try
            {
                // Make sure we have a valid session
                string apiKey;
                string token = WebUtilities.GetDolomiteSessionToken(out apiKey);
                string username = UserManager.GetUsernameFromSession(token, apiKey);
                UserManager.ExtendIdleTimeout(token);

                // Process the guid into a playlist guid
                Guid playlistId = Guid.Parse(guid);

                // Process the object we're send, attempt to deserialize it as a rule
                string bodyStr = Encoding.Default.GetString(body.ToByteArray());
                AutoPlaylistRule rule = JsonConvert.DeserializeObject<AutoPlaylistRule>(bodyStr);
                // Success! Now, add the rule to the playlist
                PlaylistManager.AddRuleToAutoPlaylist(playlistId, username, rule);

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
                const string message = "The body of the request was not a valid AutoPlaylistRule object";
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
        /// Attempts to delete a rule with the given id from the given playlist
        /// </summary>
        /// <param name="guid">GUID of the playlist to remove the rule from</param>
        /// <param name="id">The id of the rule to remove</param>
        /// <returns>A success or failure message</returns>
        public Message DeleteRuleFromAutoPlaylist(string guid, string id)
        {
            try
            {
                // Make sure we have a valid session
                string apiKey;
                string token = WebUtilities.GetDolomiteSessionToken(out apiKey);
                string username = UserManager.GetUsernameFromSession(token, apiKey);
                UserManager.ExtendIdleTimeout(token);

                // Parse the guid and id into sensible types
                Guid playlistGuid;
                if (!Guid.TryParse(guid, out playlistGuid))
                {
                    string message = String.Format("The autoplaylist GUID provided {0} was invalid.", guid);
                    return WebUtilities.GenerateResponse(new ErrorResponse(message), HttpStatusCode.BadRequest);
                }

                int ruleId;
                if (!Int32.TryParse(id, out ruleId))
                {
                    string message = String.Format("The autoplaylist rule id {0} is not a valid integer.", id);
                    return WebUtilities.GenerateResponse(new ErrorResponse(message), HttpStatusCode.BadRequest);
                }

                // Send the request to the db manager
                PlaylistManager.DeleteRuleFromAutoPlaylist(playlistGuid, username, ruleId);
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
            catch (ObjectNotFoundException)
            {
                string message = String.Format("The autoplaylist with the specified GUID '{0}' does not exist " +
                                               "or a rule with id {1} does not exist.", guid, id);
                return WebUtilities.GenerateResponse(new ErrorResponse(message), HttpStatusCode.NotFound);
            }
            catch (Exception)
            {
                return WebUtilities.GenerateResponse(new ErrorResponse(WebUtilities.InternalServerMessage),
                    HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Attempts to delete an autoplaylist with the given guid
        /// </summary>
        /// <param name="guid">Guid of the autoplaylist to delete</param>
        /// <returns>Success or error message.</returns>
        public Message DeleteAutoPlaylist(string guid)
        {
            try
            {
                // Make sure we have a valid session
                string apiKey;
                string token = WebUtilities.GetDolomiteSessionToken(out apiKey);
                string username = UserManager.GetUsernameFromSession(token, apiKey);
                UserManager.ExtendIdleTimeout(token);

                // Parse the guid into a Guid and attempt to delete
                PlaylistManager.DeleteAutoPlaylist(Guid.Parse(guid), username);
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
                string message = String.Format("The autoplaylist with the specified GUID '{0}' does not exist", guid);
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
