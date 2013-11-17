using System;
using System.Collections.Generic;
using System.Linq;
using DolomiteModel;
using DolomiteModel.PublicRepresentations;

namespace DolomiteWcfService
{
    class PlaylistManager
    {

        #region Properties

        private PlaylistDbManager PlaylistDbManager { get; set; }

        private TrackDbManager TrackDbManager { get; set; }

        #endregion

        #region Singleton Instance Code

        private static PlaylistManager _instance;

        /// <summary>
        /// Singleton instance of the track database manager
        /// </summary>
        public static PlaylistManager Instance
        {
            get { return _instance ?? (_instance = new PlaylistManager()); }
        }

        /// <summary>
        /// Singleton constructor for the track database manager
        /// </summary>
        private PlaylistManager()
        {
            PlaylistDbManager = PlaylistDbManager.Instance;
            TrackDbManager = TrackDbManager.Instance;
        }

        #endregion

        #region Public Methods

        #region Create Methods

        /// <summary>
        /// Sends the calls to the database to add the playlist to the db and
        /// adds the rules to the playlist if they were part of the playlist.
        /// If the insertion fails, the playlist will be deleted.
        /// </summary>
        /// <param name="playlist">The playlist object parsed from the request</param>
        /// <returns>The guid of the newly created playlist</returns>
        public Guid CreateAutoPlaylist(AutoPlaylist playlist)
        {
            Guid id = Guid.Empty;
            try
            {
                id = PlaylistDbManager.CreateAutoPlaylist(playlist.Name);

                // Did they send rules to add to the playlist?
                if (playlist.Rules != null && playlist.Rules.Any())
                {
                    foreach (AutoPlaylistRule rule in playlist.Rules)
                    {
                        PlaylistDbManager.AddRuleToAutoplaylist(id, rule);
                    }
                }

                return id;
            }
            catch (Exception)
            {
                // Delete the playlist
                PlaylistDbManager.DeletePlaylist(id);
                throw;
            }
        }

        /// <summary>
        /// Sends the calls to the database to add the playlist to the db and
        /// adds the tracks to the playlist if they were part of the playlist.
        /// If the insertion fails, the playlist will be deleted.
        /// </summary>
        /// <param name="playlist">The playlist object parsed from the request</param>
        /// <returns>The guid of the newly created playlist</returns>
        public Guid CreateStandardPlaylist(Playlist playlist)
        {
            Guid id = Guid.Empty;
            try
            {
                // Create the playlist
                id = PlaylistDbManager.CreateStandardPlaylist(playlist.Name);

                // Did they send tracks to add to the playlist?
                if (playlist.Tracks != null && playlist.Tracks.Any())
                {
                    foreach (Guid trackId in playlist.Tracks)
                    {
                        AddTrackToPlaylist(id, trackId);
                    }
                }

                return id;
            }
            catch (Exception)
            {
                // Delete the playlist
                PlaylistDbManager.DeletePlaylist(id);
                throw;
            }
        }

        #endregion

        #region Retrieve Methods

        /// <summary>
        /// Retrieves a list of all playlists from the database
        /// </summary>
        /// <returns>List of all playlists</returns>
        public List<Playlist> GetAllPlaylists()
        {
            // Simple, pass it off to the db wrangler
            return PlaylistDbManager.GetAllPlaylists();
        }

        /// <summary>
        /// Retrieves the requested playlist from the database
        /// </summary>
        /// <param name="playlistGuid">GUID of the playlist to look up</param>
        /// <returns>A public representation of the playlist</returns>
        public Playlist GetPlaylist(Guid playlistGuid)
        {
            // Simple, pass it off to the db manager
            return PlaylistDbManager.GetPlaylist(playlistGuid);
        }

        #endregion

        #region Update Methods

        /// <summary>
        /// Adds the specified rule to the auto playlist with the given guid
        /// </summary>
        /// <param name="playlistGuid">Guid of the auto playlist to add the rule to</param>
        /// <param name="rule">The rule to add the playlist</param>
        public void AddRuleToAutoPlaylist(Guid playlistGuid, AutoPlaylistRule rule)
        {
            // Check to see if the playlist exists
            PlaylistDbManager.GetPlaylist(playlistGuid);

            // Add the rule to the playlist
            PlaylistDbManager.AddRuleToAutoplaylist(playlistGuid, rule);
        }

        /// <summary>
        /// Adds the given track to the given playlist
        /// </summary>
        /// <param name="playlistGuid">The guid of the playlist</param>
        /// <param name="trackGuid">The guid of the track</param>
        public void AddTrackToPlaylist(Guid playlistGuid, Guid trackGuid)
        {
            // Check to see if the track and playlist exists
            TrackDbManager.GetTrackByGuid(trackGuid);
            PlaylistDbManager.GetPlaylist(playlistGuid);

            // Add the track to the playlist
            PlaylistDbManager.AddTrackToPlaylist(playlistGuid, trackGuid);
        }

        #endregion

        #region Delete Methods

        /// <summary>
        /// Sends the deletion request to the database
        /// </summary>
        /// <param name="guid">The guid of the playlist to delete</param>
        public void DeletePlaylist(Guid guid)
        {
            // Send the call to the database
            PlaylistDbManager.DeletePlaylist(guid);
        }

        #endregion

        #endregion

    }
}
