using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Model = DolomiteModel;

namespace DolomiteWcfService
{
    class DatabaseManager
    {

        #region Singleton Instance Code

        private static DatabaseManager _instance;

        /// <summary>
        /// Singleton instance of the database manager
        /// </summary>
        public static DatabaseManager Instance
        {
            get { return _instance ?? (_instance = new DatabaseManager()); }
        }

        /// <summary>
        /// Singleton constructor for the database manager
        /// </summary>
        private DatabaseManager() {}

        #endregion

        #region Public Methods

        /// <summary>
        /// Create a stubbed out record for the track
        /// </summary>
        /// <param name="guid">The guid of the track</param>
        public void CreateInitialTrackRecord(Guid guid)
        {
            using (var context = new Model.Entities())
            {
                // Create the new track record
                var track = new Model.Track
                    {
                        Id = guid
                    };
                context.Tracks.Add(track);
                context.SaveChanges();
            }
        }

        #endregion

    }
}
