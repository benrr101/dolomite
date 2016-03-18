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

        #region Creation Methods

        /// <summary>
        /// Store a new record that links a track to the quality
        /// </summary>
        /// <param name="track">The track to add the quality to</param>
        /// <param name="quality">The object representing the quality</param>
        public void AddAvailableQualityRecord(Pub.Track track, Pub.Quality quality)
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
                context.SaveChanges();
            }
        }

        /// <summary>
        /// Creates an available audio quality record to the given track for
        /// the original audio quality.
        /// </summary>
        /// <param name="track">The track to add the record to</param>
        public void AddAvailableOriginalQualityRecord(Pub.Track track)
        {
            using (var context = new Entities(SqlConnectionString))
            {
                // Fetch the original quality record
                Quality original = context.Qualities.First(q => q.Name.Equals("original", StringComparison.OrdinalIgnoreCase));
                Pub.Quality pubOriginal = new Pub.Quality { Id = original.Id };
                AddAvailableQualityRecord(track, pubOriginal);
            }
        }

        #endregion

        #region Retrieval Methods
        
        /// <summary>
        /// Fetches all supported qualities from the database
        /// </summary>
        /// <returns>A list of qualities</returns>
        /// TODO: Add caching
        public async Task<List<Pub.Quality>> GetAllQualitiesAsync()
        {
            using (var context = new Entities(SqlConnectionString))
            {
                return await context.Qualities.AsNoTracking()
                    .Where(q => q.Bitrate != null)
                    .Select(q => new Pub.Quality
                    {
                        Id = q.Id,
                        Bitrate = q.Bitrate.Value,
                        FfmpegArgs = q.FfmpegArgs,
                        Directory = q.Directory,
                        Extension = q.Extension
                    }).ToListAsync();
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
