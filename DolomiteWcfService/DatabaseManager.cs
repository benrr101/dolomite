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

        public List<Track> FetchAllTracks()
        {
            using (var context = new Model.Entities())
            {
                // Fetch ORM version of the track from the database
                // <remarks>Unfortunately, there isn't a better way to do this</remarks>
                var ormTracks = (from track in context.Tracks
                                 select track).ToList();

                // Parse them into the model version of the track
                return (from t in ormTracks
                        select new Track
                            {
                                Id = t.Id,
                                Metadata = t.Metadatas.AsEnumerable().ToDictionary(o => o.MetadataField.Field, o => o.Value)
                            }).ToList();
            }
        }

        #endregion

    }
}
