using System;
using System.Collections.Generic;
using System.Data.Entity.Core;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using DolomiteModel.EntityFramework;
using Pub = DolomiteModel.PublicRepresentations;

namespace DolomiteModel
{
    public class TrackDbManager
    {
        #region Singleton Instance Code

        private static TrackDbManager _instance;

        /// <summary>
        /// Singleton instance of the track database manager
        /// </summary>
        public static TrackDbManager Instance
        {
            get { return _instance ?? (_instance = new TrackDbManager()); }
        }

        /// <summary>
        /// Singleton constructor for the track database manager
        /// </summary>
        private TrackDbManager() { }

        #endregion

        #region Public Methods

        #region Creation Methods 

        /// <summary>
        /// Store a new record that links a track to the quality
        /// </summary>
        /// <param name="track">The track to add the quality to</param>
        /// <param name="quality">The object representing the quality</param>
        public void AddAvailableQualityRecord(Pub.Track track, Pub.Quality quality)
        {
            using (var context = new Entities())
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
            using (var context = new Entities())
            {
                // Fetch the original quality record
                Quality original = context.Qualities.First(q => q.Name.Equals("original", StringComparison.OrdinalIgnoreCase));
                Pub.Quality pubOriginal = new Pub.Quality {Id = original.Id};
                AddAvailableQualityRecord(track, pubOriginal);
            }
        }

        /// <summary>
        /// Stores a new art record.
        /// </summary>
        /// <param name="guid">GUID for the art item (the ID)</param>
        /// <param name="mimetype">Mimetype of the art file</param>
        /// <param name="hash">Hash of the file</param>
        public long CreateArtRecord(Guid guid, string mimetype, string hash)
        {
            using (var context = new Entities())
            {
                // Create a new art object
                Art artObj = new Art
                {
                    GuidId = guid,
                    Hash = hash,
                    Mimetype = mimetype
                };
                context.Arts.Add(artObj);
                context.SaveChanges();

                return artObj.Id;
            }
        }

        /// <summary>
        /// Create a stubbed out record for the track
        /// </summary>
        /// <param name="owner">The username for the owner of the track</param>
        /// <param name="guid">The guid of the track</param>
        /// <param name="mimetype">The mimetype of the upload</param>
        /// <returns>The internal ID of the new track</returns>
        public async Task<long> CreateInitialTrackRecordAsync(string owner, Guid guid, string mimetype)
        {
            using (var context = new Entities())
            {
                // TODO: Don't use magic numbers
                // Create the new track record
                var track = new Track
                {
                    DateAdded = DateTime.UtcNow,
                    DateLastModified = DateTime.UtcNow,
                    GuidId = guid,
                    Locked = false,
                    Owner = context.Users.First(u => u.Username == owner).Id,
                    OriginalMimetype = mimetype,
                    Status = 1        // The "Initial" status
                };
                context.Tracks.Add(track);
                await context.SaveChangesAsync();

                return track.Id;
            }
        }

        /// <summary>
        /// Stores the metadata for the given track
        /// </summary>
        /// <param name="track">The track to store the metadata of</param>
        /// <param name="metadatas">Dictionary of Metadata Tag Name => Value</param>
        /// <param name="writeOut">Whether or not the metadata change should be written to the file</param>
        public void StoreTrackMetadata(Pub.Track track, IDictionary<string, string> metadatas, bool writeOut)
        {
            using (var context = new Entities())
            {
                // Iterate over the metadatas and store new objects for each
                // Skip values that are null (ie, they should be deleted)
                foreach (var metadata in metadatas.Where(m => m.Value != null))
                {
                    // Skip metadata that doesn't have fields
                    var field = context.MetadataFields.FirstOrDefault(f => f.TagName == metadata.Key);
                    if (field == null)
                        continue;

                    Metadata md = new Metadata
                    {
                        Field = field.Id,
                        Track = track.InternalId,
                        Value = metadata.Value,
                        WriteOut = writeOut
                    };

                    context.Metadatas.Add(md);
                }

                // Commit the changes
                context.SaveChanges();
            }
        }

        #endregion

        #region Retrieval Methods

        /// <summary>
        /// Fetches all tracks in the database. The objects returned are not fully fleshed out,
        /// they only contain IDs and metadata.
        /// </summary>
        /// <param name="owner">The username of the owner to get all tracks for</param>
        /// <returns>Public-ready track objects with IDs and metadata populated</returns>
        /// TODO: Return whether or not the track is ready?
        public List<Pub.Track> GetAllTracksByOwner(string owner)
        {
            using (var context = new Entities())
            {
                // Fetch ORM version of the track from the database
                // <remarks>Unfortunately, there isn't a better way to do this.
                // This is because the .ToDictionary method isn't supported in LINQ-to-EF
                // </remarks>
                // TODO: Find a better way to do this using anonymous object types
                // TODO: Don't use magic numbers
                var ormTracks = context.Tracks.AsNoTracking()
                    .Where(t => t.Status == 3 && t.User.Username == owner).ToList();

                // Parse them into the model version of the track
                return ormTracks.Select(t => new Pub.Track()
                {
                    Id = t.GuidId,
                    InternalId = t.Id,
                    Metadata = t.Metadatas.ToDictionary(o => o.MetadataField.TagName, o => o.Value)
                }).ToList();
            }
        }

        /// <summary>
        /// Fetches all supported qualities from the database
        /// </summary>
        /// <returns>A list of qualitites</returns>
        public List<Pub.Quality> GetAllQualities()
        {
            using (var context = new Entities())
            {
                // Grab all the qualities from the db
                return context.Qualities.AsNoTracking()
                    .Where(q => q.Bitrate != null && q.Codec != null)
                    .Select(q => new Pub.Quality
                    {
                        Id = q.Id,
                        Bitrate = q.Bitrate.Value,
                        Codec = q.Codec,
                        Directory = q.Directory,
                        Extension = q.Extension
                    })
                    .ToList();
            }
        }

        /// <summary>
        /// Fetch the allowed metadata fields from the database.
        /// </summary>
        /// TODO: Add caching if this is super expensive?
        /// <returns>Dictionary of metadata field names to metadata ids</returns>
        public Dictionary<string, int> GetAllowedMetadataFields()
        {
            using (var context = new Entities())
            {
                // Grab all the metadata fields
                return context.MetadataFields.Select(f => new {f.TagName, f.Id}).ToDictionary(o => o.TagName, o => o.Id);
            }
        }

        /// <summary>
        /// Grab the art object based on the art object's ID
        /// </summary>
        /// <exception cref="ObjectNotFoundException">When the art object does not exist</exception>
        /// <param name="artId">The ID of the art object</param>
        /// <returns>A public-ready art object</returns>
        public Pub.Art GetArt(long artId)
        {
            using (var context = new Entities())
            {
                Pub.Art art = GetArtModel(artId, context, true).Select(a => new Pub.Art
                {
                    Id = a.GuidId,
                    InternalId = a.Id,
                    Mimetype = a.Mimetype
                }).FirstOrDefault();

                if (art == null)
                    throw new ObjectNotFoundException(String.Format("Art with GUID {0} does not exist.", artId));

                return art;
            }
        }

        /// <summary>
        /// Grab the art object based on the art object's guid
        /// </summary>
        /// <exception cref="ObjectNotFoundException">When the art object does not exist</exception>
        /// <param name="artId">The guid of the art object</param>
        /// <returns>A public-ready art object</returns>
        public Pub.Art GetArt(Guid artId)
        {
            using (var context = new Entities())
            {
                Pub.Art art = GetArtModel(artId, context, true).Select(a => new Pub.Art
                {
                    Id = a.GuidId,
                    InternalId = a.Id,
                    Mimetype = a.Mimetype
                }).FirstOrDefault();

                if (art == null)
                    throw new ObjectNotFoundException(String.Format("Art with GUID {0} does not exist.", artId));

                return art;
            }
        }

        /// <summary>
        /// Use the stored procedure on the database to grab and lock an art writing work item.
        /// </summary>
        /// <returns>The guid of the track to process, or null if none exists</returns>
        /// TODO: Return a Track object (or just wait until the dataflow/message queue thing is ready)
        public long? GetArtWorkItem()
        {
            using (var context = new Entities())
            {
                // Call the stored proc and get a work item
                return context.GetAndLockTopArtItem().FirstOrDefault();
            }
        }

        /// <summary>
        /// Fetches the ID for an art object with the given hash
        /// </summary>
        /// <param name="hash">The hash of the art</param>
        /// <returns>An internal id of art if found, default(long) if not found</returns>
        /// TODO: Return art object
        public long GetArtIdByHash(string hash)
        {
            using (var context = new Entities())
            {
                return (from art in context.Arts
                        where art.Hash == hash
                        select art.Id).FirstOrDefault();
            }
        }

        /// <summary>
        /// Retrieves metadata and metada field names that need to be written
        /// out and can be written out.
        /// </summary>
        /// <param name="trackGuid">The track id to get the metadata to write out</param>
        /// <returns>
        /// A dictionary of tagname => new value. Or an empty dictionary if there
        /// isn't any eligible metadata to write out.
        /// </returns>
        /// TODO: use the message queue stuff when ready
        public Pub.MetadataChange[] GetMetadataToWriteOut(Guid trackGuid)
        {
            using (var context = new Entities())
            {
                // We want to make sure that we only fetch the metadata that /can/
                // be written to a file. If there isn't anything, we'll just return an
                // empty dictionary
                var items = from md in context.Metadatas
                    where md.WriteOut && md.MetadataField.FileSupported
                    select new Pub.MetadataChange
                    {
                        TagName = md.MetadataField.TagName,
                        Value = md.Value,
                        Array = md.MetadataField.TagLibArray
                    };

                return items.Any()
                    ? items.ToArray()
                    : new Pub.MetadataChange[] {};
            }
        } 

        /// <summary>
        /// Use the stored procedure on the database to grab and lock an
        /// metadata writing work item.
        /// </summary>
        /// <returns>The internal id of the track to process, or null if none exists</returns>
        /// TODO: Use the message queue
        public long? GetMetadataWorkItem()
        {
            using (var context = new Entities())
            {
                // Call the stored proc and get a work item
                return context.GetAndLockTopMetadataItem().FirstOrDefault();
            }
        }

        /// <summary>
        /// Use the stored procedure on the database to grab and lock an
        /// onboarding work item.
        /// </summary>
        /// <returns>The guid of the track to process, or null if none exists</returns>
        /// TODO: use message queues when ready
        public long? GetOnboardingWorkItem()
        {
            using (var context = new Entities())
            {
                // Call the stored procedure and hopefully it'll give us a work item
                return context.GetAndLockTopOnboardingItem().FirstOrDefault();
            }
        }

        /// <summary>
        /// Fetches the track with the given guid from the database and returns
        /// it in a public-friendly object.
        /// </summary>
        /// <exception cref="ObjectNotFoundException">When a track with the given guid does not exist.</exception>
        /// <param name="trackGuid">The Guid of the track to look up</param>
        /// <param name="owner">The purported owner of the track</param>
        /// <returns>A public-ready object representation of the track</returns>
        public Pub.Track GetTrack(Guid trackGuid, string owner)
        {
            using (var context = new Entities())
            {
                // Find the track
                Track track = GetTrackModel(trackGuid, owner, context, true).FirstOrDefault();
                if (track == null)
                    throw new ObjectNotFoundException(String.Format("Failed to find track with GUID {0}", trackGuid));

                return new Pub.Track(track);
            }
        }

        /// <summary>
        /// Fetches the track with the given guid from the database and returns
        /// it in a public-friendly object.
        /// </summary>
        /// <exception cref="ObjectNotFoundException">When a track with the given guid does not exist.</exception>
        /// <param name="trackId">The internal ID of the track to look up</param>
        /// <returns>A public-ready object representation of the track</returns>
        public Pub.Track GetTrack(long trackId)
        {
            using (var context = new Entities())
            {
                // Find the track
                Track track = GetTrackModel(trackId, context, true).FirstOrDefault();
                if (track == null)
                    throw new ObjectNotFoundException(String.Format("Failed to find track with id {0}", trackId));

                return new Pub.Track(track);
            }
        }

        /// <summary>
        /// Fetches a minimalistic track object based on the hash of the track
        /// </summary>
        /// <param name="hash">The hash of the track to find</param>
        /// <param name="owner">The owner of the track to search for</param>
        /// <returns>A public track object with the id set. Null if a matching track can't be found</returns>
        public Pub.Track GetTrack(string hash, string owner)
        {
            using (var context = new Entities())
            {
                return GetTrackModel(hash, owner, context, true)
                    .Select(t => new Pub.Track {InternalId = t.Id, Id = t.GuidId})
                    .FirstOrDefault();
            }
        }

        /// <summary>
        /// Fetches a minimalistic track object based on the hash of the track
        /// </summary>
        /// <param name="hash">The hash of the track to find</param>
        /// <param name="owner">The owner of the track to search for</param>
        /// <returns>A public track object with the id set. Null if a matching track can't be found</returns>
        public Pub.Track GetTrackByHash(string hash, string owner)
        {
            using (var context = new Entities())
            {
                // Search for the track
                return (from track in context.Tracks
                    where track.Hash == hash && track.User.Username == owner
                    select new Pub.Track {InternalId = track.Id, Id = track.GuidId}).FirstOrDefault();
            }
        }

        /// <summary>
        /// Determines if any tracks are using the art given by the ID
        /// </summary>
        /// <param name="artId">The ID of the art file to look up</param>
        /// <returns>True if the art is still in use by a track, false otherwise.</returns>
        public bool IsArtInUse(long artId)
        {
            using (var context = new Entities())
            {
                return context.Tracks.Any(t => t.Art == artId);
            }
        }

        /// <summary>
        /// Determines whether or not a track exists based on GUID.
        /// </summary>
        /// <remarks>
        /// This does NOT check owners, so be careful with choosing when to use this.
        /// </remarks>
        /// <param name="trackGuid">The GUID of the track to lookup</param>
        /// <returns>True if the track exists, false if the track does not exist</returns>
        public bool TrackExists(Guid trackGuid)
        {
            using (var context = new Entities())
            {
                // Search for the track

                return context.Tracks.Any(t => t.GuidId == trackGuid);
            }
        }

        /// <summary>
        /// Determines if a track exists based on the GUID and the owner's username
        /// </summary>
        /// <param name="trackGuid">The GUID of the track to lookup</param>
        /// <param name="owner">The username of the owner</param>
        /// <returns>True if the track exists, false otherwise</returns>
        public bool TrackExists(Guid trackGuid, string owner)
        {
            using (var context = new Entities())
            {
                // Search for the track
                return context.Tracks.Any(t => t.GuidId == trackGuid && t.User.Username == owner);
            }
        }

        /// <summary>
        /// Searches the track database using the search criteria for matching
        /// tracks. Using "all" allows searching all fields that have searching
        /// enabled. Searches using LIKE %value%.
        /// </summary>
        /// <param name="owner">The username of the track owners</param>
        /// <param name="searchCriteria">
        /// A list of criteria to search using. [tagname=>value]. Fields
        /// and values are not case case sensitivite. "all" is a suitable tagname.
        /// </param>
        /// <returns>A list of guids that match the search criteria</returns>
        public List<Guid> SearchTracks(string owner, Dictionary<string, string> searchCriteria)
        {
            using(var context = new Entities()) 
            {
                // Build a result set -- using hashset prevents defaults to work
                HashSet<Guid> hashSet = new HashSet<Guid>();

                // Loop over the search fields
                foreach (var criterion in searchCriteria)
                {
                    IQueryable<Guid> trackSearch;
                    // Check if we're searching all fields
                    if (criterion.Key == "all")
                    {
                        // Search all metadata fields
                        trackSearch = from md in context.Metadatas
                            where md.Value.Contains(criterion.Value)
                            && md.Track1.User.Username == owner
                            && md.MetadataField.Searchable
                            select md.Track1.GuidId;
                    }
                    else
                    {
                        // Search only the specified metadata field
                        trackSearch = from md in context.Metadatas
                            where md.Value.Contains(criterion.Value)
                                  && md.MetadataField.TagName == criterion.Key
                                  && md.Track1.User.Username == owner
                                  && md.MetadataField.Searchable
                            select md.Track1.GuidId;
                    }
                    hashSet.UnionWith(trackSearch);
                }

                return hashSet.ToList();
            }
        } 

        #endregion

        #region Update Methods

        /// <summary>
        /// Updates the hash of the given track and marks the track for pickup
        /// by the onboarding processor.
        /// </summary>
        /// <param name="trackGuid">Guid of the track to update</param>
        /// <param name="newHash">The new hash of the track</param>
        /// <param name="owner">The owner of the track</param>
        public void TransitionTrackToPendingOnboarding(Guid trackGuid, string newHash, string owner)
        {
            using (var context = new Entities())
            {
                // Fetch the track that is to be marked as not onboarded
                Track track = GetTrackModel(trackGuid, owner, context, false).FirstOrDefault();

                // TODO: Do these checks in the sproc to allow for passing in an id and that's it
                // Verify that the track exists
                if (track == null)
                    throw new ObjectNotFoundException(String.Format("Track {0} does not exist.", trackGuid));

                // Verify that the track is currently not locked
                if (track.Locked)
                {
                    string message = String.Format("Track {0} is locked and cannot be modified at this time.", trackGuid);
                    throw new AccessViolationException(message);
                }

                // Reset the onboarding status
                context.ResetOnboardingStatus(track.Id);

                // Store the new track hash
                track.Hash = newHash;
                context.SaveChanges();
            }
        }

        /// <summary>
        /// Moves a track into the error state and stores the errors
        /// </summary>
        /// <param name="trackGuid">Guid of the track to update</param>
        /// /// <param name="owner">The owner of the track</param>
        /// <param name="userError">The error to report back to the user</param>
        /// <param name="adminError">The error for internal debugging</param>
        public void TransitionTrackToError(Guid trackGuid, string owner, string userError, string adminError)
        {
            using (var context = new Entities())
            {
                // Fetch the track to be marked as error
                Track track = GetTrackModel(trackGuid, owner, context, true).First();

                // Execute the sproc for setting error state
                context.MarkTrackAsError(track.Id, userError, adminError);
            }
        }

        /// <summary>
        /// Releases the lock on the work item and completes the art change
        /// process via a stored procedure
        /// </summary>
        /// <param name="workItem">The work item to release</param>
        public void ReleaseAndCompleteArtItem(long workItem)
        {
            using (var context = new Entities())
            {
                // Call the stored procedure to complete the track onboarding
                context.ReleaseAndCompleteArtChange(workItem);
            }
        }

        /// <summary>
        /// Releases the lock on the work item and completes the onboarding
        /// process via a stored procedure
        /// </summary>
        /// <param name="workItem">The work item to release</param>
        public void ReleaseAndCompleteOnboardingItem(long workItem)
        {
            using (var context = new Entities())
            {
                // Call the stored procedure to complete the track onboarding
                context.ReleaseAndCompleteOnboardingItem(workItem);
            }
        }

        /// <summary>
        /// Calls the stored proc to unlock the given track, and remove any flag
        /// on metadata for the track to show that the metadata needs to be
        /// written out to file.
        /// </summary>
        /// <param name="workItem">The track to release</param>
        public void ReleaseAndCompleteMetadataItem(long workItem)
        {
            using (var context = new Entities())
            {
                context.ReleaseAndCompleteMetadataUpdate(workItem);
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
        public void SetAudioQualityInfo(long trackId, int bitrate, int samplingFrequency, string mimetype, string extension)
        {
            using (var context = new Entities())
            {
                // Fetch the existing record for the track
                Track track = GetTrackModel(trackId, context, false).FirstOrDefault();
                if (track == null)
                    throw new ObjectNotFoundException(String.Format("Track {0} does not exist.", trackId));

                track.OriginalBitrate = bitrate;
                track.OriginalSampling = samplingFrequency;
                track.OriginalMimetype = mimetype;
                track.OriginalExtension = extension;
                context.SaveChanges();
            }
        }

        /// <summary>
        /// Sets the art record for the given track to the given artGuid. It is
        /// permissible to set it to null.
        /// </summary>
        /// <param name="trackId">The ID of the track</param>
        /// <param name="artId">The ID of the art record. Can be null.</param>
        /// <param name="fileChange">Whether or not the art change should be processed to the file</param>
        public void SetTrackArt(long trackId, long? artId, bool fileChange)
        {
            using (var context = new Entities())
            {
                // Search out the track
                Track track = GetTrackModel(trackId, context, false).FirstOrDefault();
                if (track == null)
                    throw new ObjectNotFoundException(String.Format("Track {0} does not exist.", trackId));

                track.Art = artId;
                track.ArtChange = fileChange;
                context.SaveChanges();
            }
        }

        #endregion

        #region Deletion Methods

        /// <summary>
        /// Deletes a given art from the database
        /// </summary>
        /// <param name="artId">ID of the art to delete</param>
        /// TODO: Move this to a sproc to avoid possible race conditions between getting and removing the art
        public void DeleteArt(long artId)
        {
            using (var context = new Entities())
            {
                // Find the art
                Art art = GetArtModel(artId, context, false).FirstOrDefault();
                if (art == null)
                    throw new ObjectNotFoundException(String.Format("Art {0} does not exist.", artId));

                // Delete it
                context.Arts.Remove(art);
                context.SaveChanges();
            }
        }

        /// <summary>
        /// Deletes the track with the given guid from the database.
        /// </summary>
        /// <exception cref="ObjectNotFoundException">
        /// Thrown when the track does not exist or does not belong to the owner
        /// </exception>
        /// <param name="trackGuid">The guid of the track to delete</param>
        /// <param name="owner">The username of the owner of the track to delete</param>
        public void DeleteTrack(Guid trackGuid, string owner)
        {
            using (var context = new Entities())
            {
                // Fetch the track
                Track track = GetTrackModel(trackGuid, owner, context, false).FirstOrDefault();
                if(track == null)
                    throw new ObjectNotFoundException(String.Format("Track {0} does not exist.", trackGuid));

                // Delete it
                context.Tracks.Remove(track);
                context.SaveChanges();
            }
        }

        /// <summary>
        /// Deletes the track with the given id from the database.
        /// </summary>
        /// <exception cref="ObjectNotFoundException">Thrown when the track does not exist</exception>
        /// <param name="trackId">The guid of the track to delete</param>
        public void DeleteTrack(long trackId)
        {
            using (var context = new Entities())
            {
                // Fetch the track
                Track track = GetTrackModel(trackId, context, false).FirstOrDefault();
                if (track == null)
                    throw new ObjectNotFoundException(String.Format("Track {0} does not exist.", trackId));

                // Delete it
                context.Tracks.Remove(track);
                context.SaveChanges();
            }
        }

        /// <summary>
        /// Deletes all metadata records for the given track that are empty.
        /// Used by the MetadataWriting background process to keep the db tidy.
        /// </summary>
        /// <param name="trackId">The ID of the track to remove blank metadata for.</param>
        public void DeleteEmptyMetadata(long trackId)
        {
            using (var context = new Entities())
            {
                // Find all the metadata for the track that is empty
                var emptyTags = context.Metadatas.Where(
                    m => m.Track == trackId && (m.Value == null || m.Value.Trim() == String.Empty));

                // Now go through and delete them
                context.Metadatas.RemoveRange(emptyTags);
                context.SaveChanges();
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
            using (var context = new Entities())
            {
                // Search for the metadata record for the track with the field
                var field = context.Metadatas.FirstOrDefault(
                    m => m.Track1.GuidId == trackGuid && m.MetadataField.TagName == metadataField);

                // If it doesn't exist, we succeeeded in deleting it, right?
                if (field == null)
                    return;

                // Delete the record
                context.Metadatas.Remove(field);
                context.SaveChanges();
            }
        }

        #endregion

        #endregion

        #region Private Methods

        #region Track Lookup Methods

        /// <summary>
        /// Builds a query to get a specific track based on owner and guid
        /// </summary>
        /// <param name="trackGuid">GUID for looking up the track</param>
        /// <param name="owner">Owner of the track</param>
        /// <param name="context">Database context</param>
        /// <param name="readOnly">Whether or not the retrieval is ready only</param>
        /// <returns>Query for looking up the desired track</returns>
        private static IQueryable<Track> GetTrackModel(Guid trackGuid, string owner, Entities context, bool readOnly)
        {
            return GetTrackModelGeneric(context, readOnly, t => t.GuidId == trackGuid && t.User.Username == owner);
        }

        /// <summary>
        /// Builds a query to get a specific track based on owner and guid
        /// </summary>
        /// <param name="trackId">Internal ID for looking up the track</param>
        /// <param name="owner">Owner of the track</param>
        /// <param name="context">Database context</param>
        /// <param name="readOnly">Whether or not the retrieval is ready only</param>
        /// <returns>Query for looking up the desired track</returns>
        private static IQueryable<Track> GetTrackModel(long trackId, string owner, Entities context, bool readOnly)
        {
            return GetTrackModelGeneric(context, readOnly, t => t.Id == trackId && t.User.Username == owner);
        }

        /// <summary>
        /// Builds a query to get a specific track based on id
        /// </summary>
        /// <param name="trackId">Internal ID for looking up the track</param>
        /// <param name="context">Database context</param>
        /// <param name="readOnly">Whether or not the retrieval is ready only</param>
        /// <returns>Query for looking up the desired track</returns>
        private static IQueryable<Track> GetTrackModel(long trackId, Entities context, bool readOnly)
        {
            return GetTrackModelGeneric(context, readOnly, t => t.Id == trackId);
        }

        /// <summary>
        /// Builds a query to get a specific track based on hash
        /// </summary>
        /// <param name="hash">Hash of the track to look for</param>
        /// <param name="owner">The owner of the track</param>
        /// <param name="context">Database context</param>
        /// <param name="readOnly">Whether or not the retrieval is ready only</param>
        /// <returns>Query for looking up the desired track</returns>
        private static IQueryable<Track> GetTrackModel(string hash, string owner, Entities context, bool readOnly)
        {
            return GetTrackModelGeneric(context, readOnly, t => t.Hash == hash && t.User.Username == owner);
        }

        /// <summary>
        /// Super-internal method for looking up a track. Should only be used by other private methods
        /// </summary>
        /// <param name="context">Database context</param>
        /// <param name="readOnly">Whether the lookup should have tracking data or not</param>
        /// <param name="predicate">Lambda for determining if a track matches</param>
        /// <returns>Query that will be used to lookup the track</returns>
        private static IQueryable<Track> GetTrackModelGeneric(Entities context, bool readOnly, Expression<Func<Track, bool>> predicate)
        {
            var tracks = readOnly ? context.Tracks.AsNoTracking() : context.Tracks;
            return tracks.Where(predicate);
        }

        #endregion

        #region Art Lookup Methods

        private IQueryable<Art> GetArtModel(Guid artGuid, Entities context, bool readOnly)
        {
            return GetArtModelGeneric(context, readOnly, a => a.GuidId == artGuid);
        }

        private IQueryable<Art> GetArtModel(long artId, Entities context, bool readOnly)
        {
            return GetArtModelGeneric(context, readOnly, a => a.Id == artId);
        }

        private IQueryable<Art> GetArtModel(string hash, Entities context, bool readOnly)
        {
            return GetArtModelGeneric(context, readOnly, a => a.Hash == hash);
        }

        /// <summary>
        /// Super-internal method for looking up an art. Should only be used by other private methods
        /// </summary>
        /// <param name="context">Database context</param>
        /// <param name="readOnly">Whether the lookup should have tracking data or not</param>
        /// <param name="predicate">Lambda for determining if an art matches</param>
        /// <returns>Query that will be used to lookup the art</returns>
        private static IQueryable<Art> GetArtModelGeneric(Entities context, bool readOnly, Expression<Func<Art, bool>> predicate)
        {
            var farts = readOnly ? context.Arts.AsNoTracking() : context.Arts;
            return farts.Where(predicate);
        }

        #endregion

        #endregion

    }
}
