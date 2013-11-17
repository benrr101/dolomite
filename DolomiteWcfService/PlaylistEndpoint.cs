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
    class PlaylistEndpoint : IPlaylistEndpoint
    {

        #region Properties

        private PlaylistManager PlaylistManager { get; set; }

        #endregion

        public PlaylistEndpoint()
        {
            PlaylistManager = PlaylistManager.Instance;
        }

        /// <summary>
        /// Handles requests to create a new playlist. Since the best way to
        /// differentiate between auto playlists and standard ones is in the method,
        /// we do that here, instead of with a uri scheme.
        /// </summary>
        /// <param name="body">The body of the request</param>
        /// <returns>A message of success or failure</returns>
        public Message CreatePlaylist(Stream body)
        {
            try
            {
                // Process the object we're send
                string bodyStr = Encoding.Default.GetString(ToByteArray(body));
                Playlist playlist = JsonConvert.DeserializeObject<Playlist>(bodyStr);

                // Determine what type of processing to do
                Guid id;
                if (playlist.PlaylistType.Equals("standard", StringComparison.OrdinalIgnoreCase))
                {
                    id = PlaylistManager.CreateStandardPlaylist(playlist);
                }
                else if (playlist.PlaylistType.Equals("auto", StringComparison.OrdinalIgnoreCase))
                {
                    // Try to parse it as an auto playlist
                    AutoPlaylist autoPlaylist = JsonConvert.DeserializeObject<AutoPlaylist>(bodyStr);
                    id = PlaylistManager.CreateAutoPlaylist(autoPlaylist);
                }
                else
                {
                    throw new ArgumentOutOfRangeException();
                }

                string responseJson = JsonConvert.SerializeObject(new PlaylistCreationSuccessResponse(id));
                return WebOperationContext.Current.CreateTextResponse(responseJson, "application/json", Encoding.UTF8);
            }
            catch (JsonSerializationException)
            {
                // The guid was probably incorrect
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                string responseJson =
                    JsonConvert.SerializeObject(new ErrorResponse("The supplied playlist object is invalid."));
                return WebOperationContext.Current.CreateTextResponse(responseJson, "application/json", Encoding.UTF8);
            }
            catch (ArgumentOutOfRangeException)
            {
                // The type of the playlist was invalid
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                string message =
                    String.Format("The supplied playlist type '{0}' is not valid. Valid types are 'standard' and 'auto'");
                string responseJson = JsonConvert.SerializeObject(new ErrorResponse(message));
                return WebOperationContext.Current.CreateTextResponse(responseJson, "application/json", Encoding.UTF8);
            }
            catch (DuplicateNameException ex)
            {
                // The name is a duplicate
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.Conflict;
                string message =
                    String.Format(
                        "A playlist with the name '{0}' already exists. Please choose a different name, or delete the existing playlist.",
                        ex.Message);
                string responseJson = JsonConvert.SerializeObject(new ErrorResponse(message));
                return WebOperationContext.Current.CreateTextResponse(responseJson, "application/json", Encoding.UTF8);
            }
            catch (InvalidExpressionException iee)
            {
                // The rule is invalid
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                string responseJson =
                    JsonConvert.SerializeObject(new ErrorResponse("Could not add playlist: " + iee.Message));
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
        public Message GetAllPlaylists()
        {
            List<Playlist> playlists = PlaylistManager.GetAllPlaylists();
            string playlistsJson = JsonConvert.SerializeObject(playlists);
            return WebOperationContext.Current.CreateTextResponse(playlistsJson, "application/json", Encoding.UTF8);
        }

        /// <summary>
        /// Attempts to retrieve a specific playlist from the database. This
        /// makes no differentiation about standard or auto playlists.
        /// </summary>
        /// <param name="guid">The guid of the playlist to lookup</param>
        /// <returns>A json serialized version of the playlist</returns>
        public Message GetPlaylist(string guid)
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
                string message = String.Format("The playlist with the specified GUID '{0}' does not exist", guid);
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

        public Message AddToPlaylist(Stream body, string guid)
        {
            throw new NotImplementedException();
        }

        public Message DeleteFromPlaylist(string guid, string id)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public Message DeletePlaylist(string guid)
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
                string message = String.Format("The playlist with the specified GUID '{0}' does not exist", guid);
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
