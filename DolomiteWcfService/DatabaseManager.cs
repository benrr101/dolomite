﻿using System;
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

        /// <summary>
        /// Deletes the track with the given guid from the database.
        /// </summary>
        /// <exception cref="ObjectNotFoundException">The track with the given guid does not exist in the database</exception>
        /// <param name="trackGuid">The guid of the track to delete</param>
        public void DeleteTrack(Guid trackGuid)
        {
            using (var context = new Model.Entities())
            {
                // Fetch the track
                // TODO: Move this to a helper method
                Model.Track track = context.Tracks.FirstOrDefault(t => t.Id == trackGuid);
                if (track == null)
                {
                    throw new ObjectNotFoundException(String.Format("Track with guid {0} could not be found", trackGuid));
                }

                // Delete it
                context.Tracks.Remove(track);
                context.SaveChanges();
            }
        }

        public Track GetTrackByGuid(Guid trackId)
        {
            using (var context = new Model.Entities())
            {
                // Search for the track
                Model.Track track = context.Tracks.FirstOrDefault(t => t.Id == trackId);
                if (track == null)
                    throw new ObjectNotFoundException(String.Format("Failed to find track with id {0}", trackId));

                // Build the track with the necessary information
                var metadata = (from m in context.Metadatas
                                where m.Track == trackId
                                select new {Name = m.MetadataField.DisplayName, m.Value});

                var dbQualities = context.AvailableQualities.Where(q => q.Track == trackId && q.Quality1.Bitrate != null).ToList();
                var qualities = (from q in dbQualities
                                 where q.Track == trackId
                                 select new Track.Quality
                                     {
                                         Bitrate = q.Quality1.Bitrate.ToString(),
                                         Directory = q.Quality1.Directory,
                                         Href = String.Format("tracks/{0}/{1}", q.Quality1.Directory, trackId),
                                         Mimetype = q.Quality1.Mimetype,
                                         Name = q.Quality1.Name
                                     }).ToList();

                // Add the original quality
                var originalQuality = new Track.Quality()
                    {
                        Bitrate = track.OriginalBitrate.ToString(),
                        Directory = "original",
                        Href = String.Format("tracks/original/{0}", trackId),
                        Mimetype = track.OriginalMimetype,
                        Name = "Original"
                    };
                qualities.Add(originalQuality);

                return new Track
                    {
                        Id = trackId,
                        Metadata = metadata.ToDictionary(o => o.Name, o => o.Value),
                        Qualities = qualities
                    };
            }
        }

        public Model.Track GetTrackModelByGuid(Guid trackId)
        {
            using (var context = new Model.Entities())
            {
                // Search for the track
                return context.Tracks.First(t => t.Id == trackId);
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
                                Metadata = t.Metadatas.AsEnumerable().ToDictionary(o => o.MetadataField.DisplayName, o => o.Value)
                            }).ToList();
            }
        }

        #endregion

        #region Constant Fetching Methods

        /// <summary>
        /// Fetch the allowed metadata fields from the database.
        /// </summary>
        /// TODO: Add caching if this is super expensive?
        /// <returns>Dictionary of metadata field names to metadata ids</returns>
        public Dictionary<string, int> GetAllowedMetadataFields()
        {
            using (var context = new Model.Entities())
            {
                // Grab all the metadata fields
                return (from field in context.MetadataFields
                        select new { field.TagName, field.Id }).ToDictionary(o => o.TagName, o => o.Id);
            }
        }

        /// <summary>
        /// Fetches all supported qualities from the database
        /// </summary>
        /// <returns>A list of qualitites</returns>
        public List<Model.Quality> GetAllQualities()
        {
            using (var context = new Model.Entities())
            {
                // Grab all the qualities from the db
                return (context.Qualities.Where(quality => quality.Bitrate != null && quality.Codec != null)).ToList();
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
        /// Releases the lock on the work item and completes the onboarding
        /// process via a stored procedure
        /// </summary>
        /// <param name="workItem">The work item to release</param>
        public void ReleaseAndCompleteWorkItem(Guid workItem)
        {
            using (var context = new Model.Entities())
            {
                // Call the stored procedure to complete the track onboarding
                context.ReleaseAndCompleteOnboardingItem(workItem);
            }
        }

        /// <summary>
        /// Sets the hash value of the given track
        /// </summary>
        /// <param name="trackId">The track to set the hash of</param>
        /// <param name="hash">The hash to set for the track</param>
        public void SetTrackHash(Guid trackId, string hash)
        {
            using (var context = new Model.Entities())
            {
                // Call the stored procedure
                context.SetTrackHash(trackId, hash);
            }
        }

        /// <summary>
        /// Stores the original audio information for track.
        /// </summary>
        /// <param name="trackId">The GUID id of the track</param>
        /// <param name="bitrate">The bitrate for the original audio</param>
        /// <param name="samplingFrequency">The sampling frequency of the original audio</param>
        /// <param name="mimetype">The mimetype of the original file</param>
        public void StoreAudioQualityInfo(Guid trackId, int bitrate, int samplingFrequency, string mimetype)
        {
            using (var context = new Model.Entities())
            {
                // Fetch the existing record for the track
                Model.Track track = context.Tracks.First(t => t.Id == trackId);
                track.OriginalBitrate = bitrate;
                track.OriginalSampling = samplingFrequency;
                track.OriginalMimetype = mimetype;
                context.SaveChanges();
            }
        }

        /// <summary>
        /// Store a new record that links a track to the quality
        /// </summary>
        /// <param name="trackId">The GUID of the track</param>
        /// <param name="quality">The object representing the quality</param>
        public void StoreAudioQualityRecord(Guid trackId, Model.Quality quality)
        {
            using (var context = new Model.Entities())
            {
                // Insert a new AvailableQuality record that ties the track to the quality
                Model.AvailableQuality aq = new Model.AvailableQuality
                    {
                        Quality = quality.Id,
                        Track = trackId
                    };
                context.AvailableQualities.Add(aq);
                context.SaveChanges();
            }
        }

        public void StoreOriginalQualityRecord(Guid trackId)
        {
            using (var context = new Model.Entities())
            {
                // Fetch the original quality record
                Model.Quality original = context.Qualities.First(q => q.Name.Equals("original", StringComparison.OrdinalIgnoreCase));
                StoreAudioQualityRecord(trackId, original);
            }
        }

        /// <summary>
        /// Stores the metadata for the given track
        /// </summary>
        /// <param name="trackId">GUID of the track</param>
        /// <param name="metadata">Dictionary of MetadataFieldId => Value</param>
        public void StoreTrackMetadata(Guid trackId, IDictionary<int, string> metadata)
        {
            using (var context = new Model.Entities())
            {
                // Iterate over the metadatas and store new objects for each
                foreach (Model.Metadata md in metadata.Select(data => new Model.Metadata
                        {
                            Field = data.Key,
                            Track = trackId,
                            Value = data.Value
                        }))
                {
                    context.Metadatas.Add(md);
                }

                // Commit the changes
                context.SaveChanges();
            }
        }

        #endregion

    }
}
