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
        /// <param name="hash">The hash of the track</param>
        public void CreateInitialTrackRecord(Guid guid, string hash)
        {
            using (var context = new Model.Entities())
            {
                // Create the new track record
                var track = new Model.Track
                    {
                        Id = guid,
                        Hash = hash,
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
                                         Extension = q.Quality1.Extension,
                                         Href = String.Format("tracks/{0}/{1}", q.Quality1.Directory, trackId),
                                         Mimetype = q.Quality1.Mimetype,
                                         Name = q.Quality1.Name
                                     }).ToList();

                // Add the original quality
                var originalQuality = new Track.Quality
                    {
                        Bitrate = track.OriginalBitrate.ToString(),
                        Directory = "original",
                        Extension = track.OriginalExtension,
                        Href = String.Format("tracks/original/{0}", trackId),
                        Mimetype = track.OriginalMimetype,
                        Name = "Original"
                    };
                qualities.Add(originalQuality);

                // Build the art path
                string artHref = track.Art.HasValue ? String.Format("tracks/art/{0}", track.Art.Value) : null;

                return new Track
                    {
                        ArtHref = artHref,
                        ArtId = track.Art,
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

        /// <summary>
        /// Retrieves the art object from the database
        /// </summary>
        /// <param name="artGuid">GUID of the art object to retrieve</param>
        /// <exception cref="ObjectNotFoundException">Thrown if the art object with the given GUID does not exist.</exception>
        /// <returns>A art object with the given guid</returns>
        public Model.Art GetArtModelByGuid(Guid artGuid)
        {
            using (var context = new Model.Entities())
            {
                // Search for the art record
                Model.Art art = context.Arts.FirstOrDefault(a => a.Id == artGuid);
                if (art == null)
                    throw new ObjectNotFoundException(
                        String.Format("Track art with the given guid {0} could not be found.", artGuid));

                return art;
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
        /// Deletes the given metadata record from the metadatas for the
        /// given track guid
        /// </summary>
        /// <param name="trackGuid">The guid of the track to delete a metadata from</param>
        /// <param name="metadataField">The metadatafield to delete</param>
        public void DeleteMetadata(Guid trackGuid, string metadataField)
        {
            using (var context = new Model.Entities())
            {
                // Search for the metadata record for the track with the field
                var field = (from v in context.Metadatas
                             where v.Track == trackGuid && v.MetadataField.DisplayName == metadataField
                             select v).FirstOrDefault();

                // If it doesn't exist, we succeeeded in deleting it, right?
                if (field == null)
                    return;

                // Delete the record
                context.Metadatas.Remove(field);
                context.SaveChanges();
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
                                 where track.HasBeenOnboarded
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

        /// <summary>
        /// Updates the hash of the given track and marks the track for pickup
        /// by the onboarding processor.
        /// </summary>
        /// <param name="trackGuid">Guid of the track to update</param>
        /// <param name="newHash">The new hash of the track</param>
        public void MarkTrackAsNotOnboarderd(Guid trackGuid, string newHash)
        {
            using (var context = new Model.Entities())
            {
                // Reset the onboarding status
                context.ResetOnboardingStatus(trackGuid);

                // Store the new track hash
                Model.Track track = context.Tracks.First(t => t.Id == trackGuid);
                track.Hash = newHash;
                context.SaveChanges();
            }
        }

        /// <summary>
        /// Fetches the GUID for an art object with the given hash
        /// </summary>
        /// <param name="hash">The hash of the art</param>
        /// <returns>A GUID if found, empty GUID otherwise</returns>
        public Guid GetArtIdByHash(string hash)
        {
            using (var context = new Model.Entities())
            {
                return (from art in context.Arts
                    where art.Hash == hash
                    select art.Id).FirstOrDefault();
            }
        }

        /// <summary>
        /// Determines if the art that the given track uses is still in use. If
        /// it is not, the track's art is reset to null, and the art record is
        /// deleted. Whether the deletion occurred or not is returned.
        /// </summary>
        /// <param name="trackGuid">The guid for the track with art to check if in use.</param>
        /// <returns>False if the art is not in use and can be safely deleted. True otherwise.</returns>
        public bool DeleteAlbumArtByUsage(Guid trackGuid)
        {
            using (var context = new Model.Entities())
            {
                // Grab the track
                var track = context.Tracks.First(t => t.Id == trackGuid);
                if (!track.Art.HasValue)
                    return false;
                
                // Search for uses of it's image
                bool inUse = context.Tracks.Any(t => t.Art == track.Art && t.Id != track.Id);
                
                // Delete the art from the database if its not in use any more
                if (!inUse)
                {
                    // Reset the existing track to null first to avoid FK contstraints
                    Guid trackArt = track.Art.Value;
                    track.Art = null;
                    context.SaveChanges();

                    // Delete the art
                    context.Arts.Remove(context.Arts.First(a => a.Id == trackArt));
                    context.SaveChanges();
                }

                return inUse;
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
        /// Stores the original audio information for track.
        /// </summary>
        /// <param name="trackId">The GUID id of the track</param>
        /// <param name="bitrate">The bitrate for the original audio</param>
        /// <param name="samplingFrequency">The sampling frequency of the original audio</param>
        /// <param name="extension">The extension of the file</param>
        /// <param name="mimetype">The mimetype of the original file</param>
        public void StoreAudioQualityInfo(Guid trackId, int bitrate, int samplingFrequency, string mimetype, string extension)
        {
            using (var context = new Model.Entities())
            {
                // Fetch the existing record for the track
                Model.Track track = context.Tracks.First(t => t.Id == trackId);
                track.OriginalBitrate = bitrate;
                track.OriginalSampling = samplingFrequency;
                track.OriginalMimetype = mimetype;
                track.OriginalExtension = extension;
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
        /// Stores a new art record.
        /// </summary>
        /// <param name="guid">GUID for the art item (the ID)</param>
        /// <param name="mimetype">Mimetype of the art file</param>
        /// <param name="hash">Hash of the file</param>
        public void StoreArtRecord(Guid guid, string mimetype, string hash)
        {
            using (var context = new Model.Entities())
            {
                // Create a new art object
                Model.Art artObj = new Model.Art
                {
                    Id = guid,
                    Hash = hash,
                    Mimetype = mimetype
                };
                context.Arts.Add(artObj);
                context.SaveChanges();
            }
        }

        /// <summary>
        /// Sets the art record for the given track to the given artGuid. It is
        /// permissible to set it to null.
        /// </summary>
        /// <param name="trackGuid">The GUID of the track</param>
        /// <param name="artGuid">The GUID of the art record. Can be null.</param>
        public void SetTrackArt(Guid trackGuid, Guid? artGuid)
        {
            using (var context = new Model.Entities())
            {
                // Search out the track, store art
                Model.Track track = context.Tracks.First(t => t.Id == trackGuid);
                track.Art = artGuid;
                context.SaveChanges();
            }
        }

        /// <summary>
        /// Stores the metadata for the given track
        /// </summary>
        /// <param name="trackId">GUID of the track</param>
        /// <param name="metadatas">Dictionary of MetadataFieldId => Value</param>
        public void StoreTrackMetadata(Guid trackId, IDictionary<string, string> metadatas)
        {
            using (var context = new Model.Entities())
            {
                // Iterate over the metadatas and store new objects for each
                // Skip values that are null (ie, they should be deleted)
                foreach (var metadata in metadatas.Where(m => m.Value != null))
                {
                    // Skip metadata that doesn't have fields
                    var field = context.MetadataFields.FirstOrDefault(f => f.TagName == metadata.Key);
                    if (field == null)
                        continue;

                    Model.Metadata md = new Model.Metadata
                    {
                        Field = field.Id,
                        Track = trackId,
                        Value = metadata.Value
                    };

                    context.Metadatas.Add(md);
                }

                // Commit the changes
                context.SaveChanges();
            }
        }

        #endregion

    }
}
