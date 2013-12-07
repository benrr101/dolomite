using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Net;
using System.ServiceModel.Channels;
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
                string bodyStr = Encoding.Default.GetString(body.ToByteArray());
                AutoPlaylist autoPlaylist = JsonConvert.DeserializeObject<AutoPlaylist>(bodyStr);
                Guid id = PlaylistManager.CreateAutoPlaylist(autoPlaylist);

                return WebUtilities.GenerateResponse(new PlaylistCreationSuccessResponse(id), HttpStatusCode.Created);
            }
            catch (JsonSerializationException)
            {
                // The guid was probably incorrect
                object payload = new ErrorResponse("The supplied static playlist object is invalid.");
                return WebUtilities.GenerateResponse(payload, HttpStatusCode.BadRequest);
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
            List<Playlist> playlists = PlaylistManager.GetAllStaticPlaylists();
            return WebUtilities.GenerateResponse(playlists, HttpStatusCode.OK);
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
                Playlist playlist = PlaylistManager.GetStaticPlaylist(Guid.Parse(guid));
                return WebUtilities.GenerateResponse(playlist, HttpStatusCode.OK);
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
                // Read the body of the request and convert it to the guid of the track to add
                //TODO: Add support for batch adding tracks
                string bodyStr = Encoding.Default.GetString(body.ToByteArray());
                Guid trackGuid = Guid.Parse(bodyStr);

                // Success! Now, add the track to the playlist
                Guid playlistId = Guid.Parse(guid);
                PlaylistManager.AddTrackToPlaylist(playlistId, trackGuid);

                // Send a happy return message
                return WebUtilities.GenerateResponse(new WebResponse(WebResponse.StatusValue.Success), HttpStatusCode.OK);
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
                return WebUtilities.GenerateResponse(new WebResponse(WebResponse.StatusValue.Success), HttpStatusCode.OK);
            }
            catch (FormatException)
            {
                // The guid was probably incorrect
                string message = String.Format("The GUID supplied '{0}' is an invalid GUID.", guid);
                return WebUtilities.GenerateResponse(new ErrorResponse(message), HttpStatusCode.BadRequest);
            }
            catch (FileNotFoundException)
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
    }
}
