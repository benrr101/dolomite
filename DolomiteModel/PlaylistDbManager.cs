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

        #endregion

    }
}
