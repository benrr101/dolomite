using System;
using System.Collections.Generic;
using System.Data.Entity;
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

        #region Creation Methods

        /// <summary>
        /// Create a stubbed out record for the track
        /// </summary>
        /// <param name="owner">The username for the owner of the track</param>
        /// <param name="guid">The guid of the track</param>
        /// <param name="mimetype">The mimetype of the upload</param>
        /// <param name="originalFilename">
        /// A friendly identifier for the track, usually the name of the file on the user's machine.
        /// </param>
        /// <returns>The internal ID of the new track</returns>
        public async Task<long> CreateInitialTrackRecordAsync(string owner, Guid guid, string mimetype, string originalFilename)
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
                    OriginalFileName = originalFilename,
                    Status = 1        // The "Initial" status
                };
                context.Tracks.Add(track);
                await context.SaveChangesAsync();

                return track.Id;
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
        /// TODO: DUPE!
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
        /// TODO: DUPE!
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
        /// Determines whether or not a track exists based on GUID.
        /// </summary>
        /// <remarks>
        /// This does NOT check owners, so be careful with choosing when to use this.
        /// </remarks>
        /// <param name="trackGuid">The GUID of the track to lookup</param>
        /// <returns>True if the track exists, false if the track does not exist</returns>
        /// TODO: Why have two methods for this?
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
        /// and values are not case case sensitive. "all" is a suitable tagname.
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
        /// TODO: Make this a sproc
        /// </summary>
        /// <param name="track"></param>
        /// <param name="userError"></param>
        /// <param name="adminError"></param>
        /// <returns></returns>
        public async Task SetTrackErrorStateAsync(Pub.Track track, string userError, string adminError)
        {
            using (var context = new Entities())
            {
                // Create a new error info
                ErrorInfo ei = new ErrorInfo
                {
                    AdminError = adminError,
                    UserError = userError
                };
                context.ErrorInfoes.Add(ei);
                await context.SaveChangesAsync();

                // Link the error info to the track
                Track internalTrack = await GetTrackModel(track.InternalId, context, false).FirstOrDefaultAsync();
                if (internalTrack == null)
                {
                    throw new ObjectNotFoundException(String.Format("Track {0} does not exist.", track.InternalId));
                }
                internalTrack.ErrorInfo = ei.Id;

                // Unlock the track
                internalTrack.Locked = false;
                internalTrack.Status = 5;

                await context.SaveChangesAsync();
            }
        }

        #endregion

        #region Deletion Methods

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
        internal static IQueryable<Track> GetTrackModel(Guid trackGuid, string owner, Entities context, bool readOnly)
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
        internal static IQueryable<Track> GetTrackModel(long trackId, string owner, Entities context, bool readOnly)
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
        internal static IQueryable<Track> GetTrackModel(long trackId, Entities context, bool readOnly)
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
        internal static IQueryable<Track> GetTrackModel(string hash, string owner, Entities context, bool readOnly)
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

        #endregion

    }
}
