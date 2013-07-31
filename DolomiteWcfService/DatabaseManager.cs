using System;
using System.Collections.Generic;
using System.Data;
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
                        Id = guid,
                        TrackInTempStorage = true,
                        HasBeenOnboarded = false,
                        Locked = false
                    };
                context.Tracks.Add(track);
                context.SaveChanges();
            }
        }

        public Track GetTrackByHash(string hash)
        {
            using (var context = new Model.Entities())
            {
                // Search for the track
                return (from track in context.Tracks
                        where track.Hash == hash
                        select new Track {Id = track.Id}).FirstOrDefault();
            }
        }

        /// <summary>
        /// Fetches all tracks in the database. This auto-converts all the fields
        /// </summary>
        /// <returns></returns>
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

        #region Onboarding Methods

        /// <summary>
        /// Use the stored procedure on the database to grab and lock an
        /// onboarding work item.
        /// </summary>
        /// <returns>The guid of the track to process, or null if none exists</returns>
        public Guid? GetOnboardingWorkItem()
        {
            using (var context = new Model.Entities())
            {
                // Call the stored procedure and hopefully it'll give us a work item
                return context.GetAndLockTopOnboardingItem().FirstOrDefault();
            }
        }

        /// <summary>
        /// Sets the hash value of the given track
        /// </summary>
        /// <param name="trackId">The track to set the hash of</param>
        /// <param name="hash">The hash to set for the track</param>
        /// TODO: Replace with a stored proc call
        public void SetTrackHash(Guid trackId, string hash)
        {
            using (var context = new Model.Entities())
            {
                // Call the stored procedure
                context.SetTrackHash(trackId, hash);
            }
        }

        #endregion

    }
}
