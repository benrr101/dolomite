using System;
using System.Data.Entity;
using System.Data.Entity.Core;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using DolomiteModel.EntityFramework;
using Pub = DolomiteModel.PublicRepresentations;

namespace DolomiteModel
{
    public class ArtDbManager
    {
        #region Singleton Instance Code

        private static ArtDbManager _instance;

        /// <summary>
        /// Singleton instance of the art database manager
        /// </summary>
        public static ArtDbManager Instance
        {
            get { return _instance ?? (_instance = new ArtDbManager()); }
        }

        /// <summary>
        /// Singleton constructor for the art database manager
        /// </summary>
        private ArtDbManager() { }

        #endregion

        #region Creation Methods

        /// <summary>
        /// Stores a new art record.
        /// </summary>
        /// <param name="guid">GUID for the art item (the ID)</param>
        /// <param name="mimetype">Mimetype of the art file</param>
        /// <param name="hash">Hash of the file</param>
        [Obsolete("Use CreateArtRecordAsync method")]
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
        /// Stores a new art record.
        /// </summary>
        /// <param name="guid">GUID for the art item (the ID)</param>
        /// <param name="mimetype">Mimetype of the art file</param>
        /// <param name="hash">Hash of the file</param>
        /// <returns>The internal ID of the newly created art record</returns>
        public async Task<long> CreateArtRecordAsync(Guid guid, string mimetype, string hash)
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
                await context.SaveChangesAsync();

                return artObj.Id;
            }
        }

        #endregion

        #region Retrieval Methods

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
        /// Fetches the ID for an art object with the given hash
        /// </summary>
        /// <param name="hash">The hash of the art</param>
        /// <returns>An internal id of art if found, <c>null</c> if not found</returns>
        /// TODO: Return a Pub.Art
        public long? GetArtIdByHash(string hash)
        {
            using (var context = new Entities())
            {
                Art art = context.Arts.FirstOrDefault(a => a.Hash == hash);
                return art == null ? (long?)null : art.Id;
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

        #endregion

        #region Update Methods

        /// <summary>
        /// Sets the art record for the given track to the given artGuid. It is
        /// permissible to set it to null.
        /// </summary>
        /// <param name="trackId">The ID of the track</param>
        /// <param name="artId">The ID of the art record. Can be null.</param>
        /// <param name="fileChange">Whether or not the art change should be processed to the file</param>
        [Obsolete("Use SetTrackArtAsync method.")]
        public void SetTrackArt(long trackId, long? artId, bool fileChange)
        {
            using (var context = new Entities())
            {
                // Search out the track
                Track track = TrackDbManager.GetTrackModel(trackId, context, false).FirstOrDefault();
                if (track == null)
                    throw new ObjectNotFoundException(String.Format("Track {0} does not exist.", trackId));

                track.Art = artId;
                track.ArtChange = fileChange;
                context.SaveChanges();
            }
        }

        /// <summary>
        /// Sets the art record for the given track to the given artGuid. It is permissible to set
        /// it to <c>null</c>.
        /// </summary>
        /// <param name="track">The track to update</param>
        /// <param name="artId">The ID of the art record. Can be null.</param>
        /// <param name="fileChange">Whether or not the art change should be processed to the file</param>
        public async Task SetTrackArtAsync(Pub.Track track, long? artId, bool fileChange)
        {
            using (var context = new Entities())
            {
                // Fetch down the track to make sure it exists, we'll be making changes to it soon
                Track internalTrack = await TrackDbManager.GetTrackModel(track.InternalId, context, false).FirstOrDefaultAsync();
                if (internalTrack == null)
                    throw new ObjectNotFoundException(String.Format("Track {0} does not exist.", track.InternalId));

                // Set the track's information
                internalTrack.Art = artId;
                track.ArtChange = fileChange;

                if (artId != null)
                {
                    // Before we try to save, make sure that the art exists
                    Art internalArt = await GetArtModel(artId.Value, context, true).FirstOrDefaultAsync();
                    if (internalArt == null)
                        throw new ObjectNotFoundException(String.Format("Art {0} does not exist.", artId));
                }
                else
                {
                    // Since we're removing art from a track, see if we can just remove the art
                    if (internalTrack.Art != null && internalTrack.Art1.Tracks.Count <= 1)
                    {
                        context.Arts.Remove(internalTrack.Art1);
                    }
                }

                await context.SaveChangesAsync();
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

        #endregion

        #region Private Helper Methods

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
    }
}
