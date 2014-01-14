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
        /// <param name="owner">The username of the owner of the playlist</param>
        /// <returns>The guid of the newly created playlist</returns>
        public Guid CreateAutoPlaylist(AutoPlaylist playlist, string owner)
        {
            Guid id = Guid.Empty;
            try
            {
                id = PlaylistDbManager.CreateAutoPlaylist(playlist.Name, owner, playlist.MatchAll);

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
                PlaylistDbManager.DeleteAutoPlaylist(id);
                throw;
            }
        }

        /// <summary>
        /// Sends the calls to the database to add the playlist to the db and
        /// adds the tracks to the playlist if they were part of the playlist.
        /// If the insertion fails, the playlist will be deleted.
        /// </summary>
        /// <param name="playlist">The playlist object parsed from the request</param>
        /// <param name="owner">The username of the owner of the playlist</param>
        /// <returns>The guid of the newly created playlist</returns>
        public Guid CreateStaticPlaylist(Playlist playlist, string owner)
        {
            Guid id = Guid.Empty;
            try
            {
                // Create the playlist
                id = PlaylistDbManager.CreateStandardPlaylist(playlist.Name, owner);

                // Did they send tracks to add to the playlist?
                if (playlist.Tracks != null && playlist.Tracks.Any())
                {
                    foreach (Guid trackId in playlist.Tracks)
                    {
                        AddTrackToPlaylist(id, trackId, owner);
                    }
                }

                return id;
            }
            catch (Exception)
            {
                // Delete the playlist
                PlaylistDbManager.DeleteStaticPlaylist(id);
                throw;
            }
        }

        #endregion

        #region Retrieve Methods

        /// <summary>
        /// Retrieves a list of all auto playlists from the database
        /// </summary>
        /// <param name="owner">The username of the owner of the playlists to retrieve</param>
        /// <returns>List of all auto playlists</returns>
        public List<Playlist> GetAllAutoPlaylists(string owner)
        {
            return PlaylistDbManager.GetAllAutoPlaylists(owner);
        }

        /// <summary>
        /// Retrieves a list of all static playlists from the database
        /// </summary>
        /// <param name="owner">The username of the owner of the playlists to retrieve</param>
        /// <returns>List of all static playlists</returns>
        public List<Playlist> GetAllStaticPlaylists(string owner)
        {
            // Simple, pass it off to the db wrangler
            return PlaylistDbManager.GetAllStaticPlaylists(owner);
        }

        /// <summary>
        /// Retrieves the requested auto playlist from the database
        /// </summary>
        /// <param name="playlistGuid">GUID of the playlist to look up</param>
        /// <returns>A public representation of the autoplaylist</returns>
        public AutoPlaylist GetAutoPlaylist(Guid playlistGuid)
        {
            // Simple, pass it off to the db manager
            return PlaylistDbManager.GetAutoPlaylist(playlistGuid);
        }

        /// <summary>
        /// Get a static playlist by it's guid
        /// </summary>
        /// <param name="playlistGuid">Guid of the playlist to lookup</param>
        /// <returns>A public-ready static playlist object</returns>
        public Playlist GetStaticPlaylist(Guid playlistGuid)
        {
            return PlaylistDbManager.GetStaticPlaylist(playlistGuid);
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
            // Add the rule to the playlist
            PlaylistDbManager.AddRuleToAutoplaylist(playlistGuid, rule);
        }

        /// <summary>
        /// Adds the given track to the given playlist.
        /// </summary>
        /// <param name="playlistGuid">The guid of the playlist</param>
        /// <param name="trackGuid">The guid of the track</param>
        /// <param name="owner">The username of the owner of the track</param>
        /// <param name="position">The position to insert the track in the playlist</param>
        public void AddTrackToPlaylist(Guid playlistGuid, Guid trackGuid, string owner, int? position = null)
        {
            // Check to see if the track and playlist exists, verify the owners of both
            Track track = TrackDbManager.GetTrackByGuid(trackGuid);
            if (track.Owner != owner)
            {
                string mess1 = String.Format(
                    "The track {0} cannot be added to playlist {1} " +
                    "because the track is not owned by the session owner.",
                    trackGuid, playlistGuid);
                throw new UnauthorizedAccessException(mess1);
            }

            Playlist playlist = PlaylistDbManager.GetStaticPlaylist(playlistGuid);
            if (playlist.Owner != owner)
            {
                string mess1 = String.Format(
                    "The track {0} cannot be added to playlist {1} " +
                    "because the playlist is not owned by the session owner.",
                    trackGuid, playlistGuid);
                throw new UnauthorizedAccessException(mess1);
            }

            // Add the track to the playlist
            PlaylistDbManager.AddTrackToPlaylist(playlistGuid, trackGuid, position);
        }

        /// <summary>
        /// Deletes a rule from a given autoplaylist
        /// </summary>
        /// <param name="playlistGuid">GUID of the playlist to delete the rule from</param>
        /// <param name="ruleId">Id of the rule to delete</param>
        public void DeleteRuleFromAutoPlaylist(Guid playlistGuid, int ruleId)
        {
            // Pass the call to the database
            PlaylistDbManager.DeleteRuleFromAutoplaylist(playlistGuid, ruleId);
        }

        /// <summary>
        /// Sends the call to the database to delete the track from the playlist
        /// </summary>
        /// <param name="playlistGuid">The guid of the playlist to remove the track from</param>
        /// <param name="trackId">The guid of the track to remove</param>
        public void DeleteTrackFromStaticPlaylist(Guid playlistGuid, Guid trackId)
        {
            PlaylistDbManager.DeleteTrackFromPlaylist(playlistGuid, trackId);
        }

        #endregion

        #region Delete Methods

        /// <summary>
        /// Sends the deletion request to the database
        /// </summary>
        /// <param name="guid">The guid of the playlist to delete</param>
        public void DeleteAutoPlaylist(Guid guid)
        {
            // Send the call to the database
            PlaylistDbManager.DeleteAutoPlaylist(guid);
        }

        /// <summary>
        /// Sends the deletion request to the database
        /// </summary>
        /// <param name="guid">The guid of the playlist to delete</param>
        public void DeleteStaticPlaylist(Guid guid)
        {
            // Send the call to the database
            PlaylistDbManager.DeleteStaticPlaylist(guid);
        }

        #endregion

        #endregion

    }
}
