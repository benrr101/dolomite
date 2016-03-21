using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Core;
using System.Linq;
using System.Threading.Tasks;
using DolomiteModel.EntityFramework;
using Pub = DolomiteModel.PublicRepresentations;

namespace DolomiteModel
{
    public class QualityDbManager
    {

        #region Constants
        
        /// <summary>
        /// The ID of the original track record.
        /// NOTE: Although this is a magic number, it's a relatively safe one. Qualities are hard
        /// coded into the database metadata, so they shouldn't change very often.
        /// </summary>
        private const int OriginalId = 1;

        /// <summary>
        /// The amount of time to wait before invalidating the cache
        /// </summary>
        private readonly static TimeSpan CacheExpirationInterval = TimeSpan.FromHours(1);

        #endregion

        #region Singleton Instance Code

        /// <summary>
        /// The connection string to the database
        /// </summary>
        public static string SqlConnectionString { get; set; }

        private static QualityDbManager _instance;

        /// <summary>
        /// Singleton instance of the quality database manager
        /// </summary>
        public static QualityDbManager Instance
        {
            get { return _instance ?? (_instance = new QualityDbManager()); }
        }

        /// <summary>
        /// Singleton constructor for the quality database manager
        /// </summary>
        private QualityDbManager() { }

        #endregion

        #region Cache Logic

        /// <summary>
        /// The date when the cache is expired
        /// </summary>
        private DateTime CacheExpirationDate { get; set; }

        /// <summary>
        /// Internal storage of all qualities
        /// </summary>
        private Pub.Quality[] _cachedQualities;

        /// <summary>
        /// All qualities from the db. This performs the caching logic
        /// </summary>
        private IEnumerable<Pub.Quality> AllQualities
        {
            get
            {
                // If we don't have a chached version, go get one
                if (_cachedQualities == null || DateTime.UtcNow > CacheExpirationDate)
                {
                    using (var context = new Entities(SqlConnectionString))
                    {
                        _cachedQualities = context.Qualities.AsNoTracking()
                            .Select(q => new Pub.Quality(q))
                            .ToArray();
                    }

                    CacheExpirationDate = DateTime.UtcNow + CacheExpirationInterval;
                }

                return _cachedQualities;
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// All qualities that should be created from the original track
        /// </summary>
        public Pub.Quality[] CreatedQualities
        {
            get { return AllQualities.Where(q => q.Id != OriginalId).ToArray(); }
        }

        /// <summary>
        /// The quality for the original copy of the track
        /// </summary>
        public Pub.Quality OriginalQuality
        {
            get { return AllQualities.First(q => q.Id == OriginalId); }
        }

        #endregion

        #region Creation Methods

        /// <summary>
        /// Store a new record that links a track to the quality
        /// </summary>
        /// <param name="track">The track to add the quality to</param>
        /// <param name="quality">The object representing the quality</param>
        public async Task AddAvailableQualityRecordAsync(Pub.Track track, Pub.Quality quality)
        {
            using (var context = new Entities(SqlConnectionString))
            {
                // Insert a new AvailableQuality record that ties the track to the quality
                AvailableQuality aq = new AvailableQuality
                {
                    Quality = quality.Id,
                    Track = track.InternalId
                };
                context.AvailableQualities.Add(aq);
                await context.SaveChangesAsync();
            }
        }

        #endregion

        #region Update Methods

        /// <summary>
        /// Stores the original audio information for track.
        /// </summary>
        /// <param name="trackId">The GUID id of the track</param>
        /// <param name="bitrate">The bitrate for the original audio</param>
        /// <param name="extension">The extension of the file</param>
        /// <param name="mimetype">The mimetype of the original file</param>
        public async Task SetAudioQualityInfoAsync(long trackId, int bitrate, string mimetype, string extension)
        {
            using (var context = new Entities(SqlConnectionString))
            {
                // Fetch the existing record for the track
                Track track = await TrackDbManager.GetTrackModel(trackId, context, false).FirstOrDefaultAsync();
                if (track == null)
                    throw new ObjectNotFoundException(String.Format("Track {0} does not exist.", trackId));

                track.OriginalMimetype = mimetype;
                track.OriginalExtension = extension;
                await context.SaveChangesAsync();
            }
        }

        #endregion

        #region Deletion Methods

        /// <summary>
        /// Deletes all qualities of a track from the database
        /// </summary>
        /// <param name="trackId">ID of the track whose qualities to delete</param>
        public async Task DeleteAllTrackQualitiesAsync(long trackId)
        {
            using (var context = new Entities(SqlConnectionString))
            {
                var qualitiesToRemove = context.AvailableQualities.Where(tq => tq.Track == trackId);
                context.AvailableQualities.RemoveRange(qualitiesToRemove);
                await context.SaveChangesAsync();
            }
        }

        #endregion

    }
}
