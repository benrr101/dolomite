using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel.Channels;
using System.ServiceModel.Web;
using System.Text;
using System.Threading.Tasks;
using DolomiteModel;
using DolomiteModel.PublicRepresentations;
using Newtonsoft.Json;

namespace DolomiteWcfService
{
    class PlaylistEndpoint : IPlaylistEndpoint
    {

        #region Properties

        private PlaylistDbManager PlaylistDbManager { get; set; }

        #endregion

        public PlaylistEndpoint()
        {
            PlaylistDbManager = PlaylistDbManager.Instance;
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
                if (playlist.PlaylistType.Equals("standard", StringComparison.OrdinalIgnoreCase))
                {
                    // Create the playlist
                    Guid id = PlaylistDbManager.CreateStandardPlaylist(playlist.Name);

                    // Did they send tracks to add to the playlist?
                    if (playlist.Tracks != null && playlist.Tracks.Any())
                    {
                        foreach (Guid trackId in playlist.Tracks)
                        {
                            // TODO: Add to the playlist
                        }
                    }
                }
                else if (playlist.PlaylistType.Equals("auto", StringComparison.OrdinalIgnoreCase))
                {
                    // Try to parse it as an auto playlist
                    AutoPlaylist autoPlaylist = JsonConvert.DeserializeObject<AutoPlaylist>(bodyStr);
                    Guid id = PlaylistDbManager.CreateAutoPlaylist(autoPlaylist.Name);

                    // Did they send rules to add to the playlist?
                    if (autoPlaylist.Rules != null && autoPlaylist.Rules.Any())
                    {
                        foreach (AutoPlaylistRule rule in autoPlaylist.Rules)
                        {
                            // TODO: Add to the playlist
                        }
                    }
                }
                else
                {
                    throw new ArgumentOutOfRangeException();
                }

                string responseJson = JsonConvert.SerializeObject(new WebResponse(WebResponse.StatusValue.Success));
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
                        "A playlist with the name '{0}' already exists. Please choose a different name, or delete the existing playlist.", ex.Message);
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

        public Message GetAllPlaylists()
        {
            throw new NotImplementedException();
        }

        public Message GetPlaylist(string guid)
        {
            throw new NotImplementedException();
        }

        public Message AddToPlaylist(Stream body, string guid)
        {
            throw new NotImplementedException();
        }

        public Message DeleteFromPlaylist(string guid, string id)
        {
            throw new NotImplementedException();
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
