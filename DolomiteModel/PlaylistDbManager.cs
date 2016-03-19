using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity.Core;
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

        /// <summary>
        /// The connection string to the database
        /// </summary>
        public static string SqlConnectionString { get; set; }

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
        /// <param name="owner">The username of the owner of the playlist</param>
        /// <returns>The new guid id for the playlist</returns>
        public Guid CreateStandardPlaylist(string name, string owner)
        {
            using (var context = new Entities(SqlConnectionString))
            {
                // Generate the guid of the playlist
                // TODO: Remove, let the user put the guid
                Guid guid = Guid.NewGuid();

                // Create a new playlist and send it to the db
                Playlist playlist = new Playlist
                {
                    GuidId = guid,
                    Name = name,
                    User = context.Users.First(u => u.Username == owner)
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

        #endregion

        #region Retrieval Methods

        /// <summary>
        /// Creates a list of all the static playlists in the database. Does 
        /// not return the tracks of the playlist
        /// </summary>
        /// <param name="owner">The username of the owner playlists to select</param>
        /// <returns>The list of static playlists without tracks</returns>
        public List<Pub.Playlist> GetAllStaticPlaylists(string owner)
        {
            using (var context = new Entities(SqlConnectionString))
            {
                return (from p in context.Playlists
                        where p.User.Username == owner
                        select new Pub.Playlist
                        {
                            Id = p.GuidId,
                            Name = p.Name,
                            Owner = p.User.Username,
                            Type = Pub.Playlist.PlaylistType.Static
                        }).ToList();
            }
        }

        /// <summary>
        /// Attempts to find a static playlist with the given id.
        /// <exception cref="ObjectNotFoundException">
        /// Thrown if a static playlist with the given guid could not be found
        /// </exception>
        /// </summary>
        /// <param name="playlistGuid">The guid of the playlist to find</param>
        /// <returns>A public-ready static playlist object</returns>
        public Pub.Playlist GetStaticPlaylist(Guid playlistGuid)
        {
            using (var context = new Entities(SqlConnectionString))
            {
                // Try to retrieve the playlist as a standard playlist
                Playlist playlist = context.Playlists.FirstOrDefault(p => p.GuidId == playlistGuid);
                if (playlist == null)
                    throw new ObjectNotFoundException(String.Format("A playlist with id {0} could not be found", playlistGuid));

                Pub.Playlist pubPlaylist = new Pub.Playlist
                {
                    Id = playlist.GuidId,
                    InternalId = playlist.Id,
                    Name = playlist.Name,
                    Owner = playlist.User.Username,
                    Tracks = playlist.PlaylistTracks.OrderBy(spt => spt.Order).Select(spt => spt.Track1.GuidId).ToList(),
                    Type = Pub.Playlist.PlaylistType.Static
                };
                return pubPlaylist;
            }
        }

        #endregion

        #region Update Methods

        /// <summary>
        /// Adds the track to the playlist. If necessary the order of the
        /// tracks will be updated.
        /// </summary>
        /// <param name="playlist">The playlist to add the track to</param>
        /// <param name="track">The track to add to the playlist</param>
        /// <param name="position">
        /// The order of the track in the list. If not provided, then the position
        /// will be added to end of the playlist.
        /// </param>
        public void AddTrackToPlaylist(Pub.Playlist playlist, Pub.Track track, int? position = null)
        {
            using (var context = new Entities(SqlConnectionString))
            {
                // Determine what the position should be
                if (position.HasValue)
                {
                    // Increment all the existing playlist orders
                    context.IncrementPlaylistTrackOrder(playlist.InternalId, position);
                }
                else
                {
                    // Grab the maximum of the tracks in the playlist
                    int? maxPos = context.PlaylistTracks.Where(pt => pt.Playlist == playlist.InternalId).Max(pt => pt.Order);
                    position = maxPos.HasValue ? maxPos + 1 : 1;
                }

                // Create a new record for the track->playlist
                PlaylistTrack playlistTrack = new PlaylistTrack
                {
                    Order = position.Value,
                    Playlist = playlist.InternalId,
                    Track = track.InternalId
                };
                context.PlaylistTracks.Add(playlistTrack);
                context.SaveChanges();
            }
        }

        /// <summary>
        /// Deletes a track from a static playlist. Also decrements the order 
        /// of the tracks in the playist to keep the indices correct.
        /// </summary>
        /// <param name="playlist">The playlist to remove the track from</param>
        /// <param name="trackId">ID of the track to remove from the playlist</param>
        public void DeleteTrackFromPlaylist(Pub.Playlist playlist, int trackId)
        {
            using (var context = new Entities(SqlConnectionString))
            {
                // Find the playlist<->track object
                var playlistTrack = context.PlaylistTracks.FirstOrDefault(
                    t => t.Playlist == playlist.InternalId && t.Order == trackId);
                if (playlistTrack == null)
                    throw new ObjectNotFoundException("Failed to find track in playlist.");

                // Temp store the order for future processes
                var order = playlistTrack.Order;

                // Delete the track from the playlist
                context.PlaylistTracks.Remove(playlistTrack);
                context.SaveChanges();

                context.DecrementPlaylistTrackOrder(playlist.InternalId, order);
            }
        }

        #endregion

        #region Deletion Methods

        /// <summary>
        /// Tries to delete a static playlist from the database.
        /// </summary>
        /// <param name="playlistGuid">GUID of static playlist to delete</param>
        public void DeleteStaticPlaylist(Guid playlistGuid)
        {
            if (playlistGuid == Guid.Empty)
                return;

            using (var context = new Entities(SqlConnectionString))
            {
                // Try to delete the static playlist from the playlists
                Playlist playlist = context.Playlists.FirstOrDefault(p => p.GuidId == playlistGuid);
                if (playlist == null)
                    throw new ObjectNotFoundException("Static playlist with the given GUID not found.");
                    
                context.Playlists.Remove(playlist);
                context.SaveChanges();
            }
        }

        #endregion

        #endregion
    }
}
