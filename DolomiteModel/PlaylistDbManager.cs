using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;
using DolomiteModel.EntityFramework;
using Pub = DolomiteModel.PublicRepresentations;

namespace DolomiteModel
{
    public class PlaylistDbManager
    {

        #region Singleton Instance Code

        private static PlaylistDbManager _instance;

        /// <summary>
        /// Singleton instance of the track database manager
        /// </summary>
        public static PlaylistDbManager Instance
        {
            get { return _instance ?? (_instance = new PlaylistDbManager()); }
        }

        /// <summary>
        /// Singleton constructor for the track database manager
        /// </summary>
        private PlaylistDbManager() { }

        #endregion

        #region Public Methods

        #region Creation Methods
        
        /// <summary>
        /// Creates a new standard playlist
        /// </summary>
        /// <param name="name">The name to give to the playlist</param>
        /// <returns>The new guid id for the playlist</returns>
        public Guid CreateStandardPlaylist(string name)
        {
            using (var context = new DbEntities())
            {
                // Generate the guid of the playlist
                Guid guid = Guid.NewGuid();

                // Create a new playlist and send it to the db
                Playlist playlist = new Playlist
                {
                    Id = guid,
                    Name = name
                };
                context.Playlists.Add(playlist);
                try
                {
                    context.SaveChanges();
                }
                catch (DbUpdateException ex)
                {
                    // Check for duplicate entry error
                    SqlException sex = ex.InnerException.InnerException as SqlException;
                    if (sex != null && sex.Number == 2601)
                    {
                        throw new DuplicateNameException(name);
                    }
                    
                    // Default to rethrowing
                    throw;
                }

                return guid;
            }
        }

        /// <summary>
        /// Creates a new auto playlist
        /// </summary>
        /// <param name="name">The name to give to the playlist</param>
        /// <param name="limit">The optional limit of tracks to satisfy the playlist</param>
        /// <returns>The new guid id for the playlist</returns>
        public Guid CreateAutoPlaylist(string name, int? limit = null)
        {
            using (var context = new DbEntities())
            {
                // Generate a new playlist guid
                Guid guid = Guid.NewGuid();

                // Create the playlist and add to db
                Autoplaylist playlist = new Autoplaylist
                {
                    Id = guid,
                    // TODO: Add limit
                    Name = name
                };
                context.Autoplaylists.Add(playlist);
                context.SaveChanges();

                return guid;
            }
        }

        #endregion

        #region Update Methods

        /// <summary>
        /// Adds the track to the playlist. If necessary the order of the
        /// tracks will be updated.
        /// </summary>
        /// <param name="playlistGuid">The guid of the playlist to add the track to</param>
        /// <param name="trackGuid">The track to add to the playlist</param>
        /// <param name="position">
        /// The order of the track in the list. If not provided, then the position
        /// will be added to end of the playlist.
        /// </param>
        public void AddTrackToPlaylist(Guid playlistGuid, Guid trackGuid, int? position = null)
        {
            using (var context = new DbEntities())
            {
                // Determine what the position should be
                if (position.HasValue)
                {
                    // Increment all the existing playlist orders
                    context.IncrementPlaylistTrackOrder(playlistGuid, position);
                }
                else
                {
                    // Grab the maximum of the tracks in the playlist
                    int? maxPos = context.PlaylistTracks.Where(pt => pt.Playlist == playlistGuid).Max(pt => pt.Order);
                    position = maxPos.HasValue ? maxPos + 1 : 1;
                }

                // Create a new record for the track->playlist
                PlaylistTrack playlistTrack = new PlaylistTrack
                {
                    Order = position.Value,
                    Playlist = playlistGuid,
                    Track = trackGuid
                };
                context.PlaylistTracks.Add(playlistTrack);
                context.SaveChanges();
            }
        }

        #endregion

        #region Deletion Methods

        /// <summary>
        /// Tries to delete the playlist from the database. It doesn't matter
        /// what type of playlist it is. Non-existing playlist errors are swallowed.
        /// </summary>
        /// <remarks>
        /// Since GUIDs are supposed to be unique, we don't have to worry about
        /// accidentally deleting the wrong playlist.
        /// </remarks>
        /// <param name="playlistGuid">GUID of the playlist to delete</param>
        public void DeletePlaylist(Guid playlistGuid)
        {
            if (playlistGuid == Guid.Empty)
                return;

            using (var context = new DbEntities())
            {
                // Try to delete the playlist from the autoplaylists
                Autoplaylist autoplaylist = context.Autoplaylists.FirstOrDefault(ap => ap.Id == playlistGuid);
                if (autoplaylist != null)
                    context.Autoplaylists.Remove(autoplaylist);

                // Try to delete the playlist from the playlists
                Playlist playlist = context.Playlists.FirstOrDefault(p => p.Id == playlistGuid);
                if (playlist != null)
                    context.Playlists.Remove(playlist);

                context.SaveChanges();
            } 
        }

        #endregion

        #endregion

    }
}
