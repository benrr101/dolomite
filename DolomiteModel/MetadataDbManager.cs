using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DolomiteModel.EntityFramework;
using Pub = DolomiteModel.PublicRepresentations;

namespace DolomiteModel
{
    public class MetadataDbManager
    {
        #region Singleton Instance Code

        private static MetadataDbManager _instance;

        /// <summary>
        /// Singleton instance of the metadata database manager
        /// </summary>
        public static MetadataDbManager Instance
        {
            get { return _instance ?? (_instance = new MetadataDbManager()); }
        }

        /// <summary>
        /// Singleton constructor for the metadata database manager
        /// </summary>
        private MetadataDbManager() { }

        #endregion

        #region Creation Methods

        /// <summary>
        /// Stores the metadata for the given track
        /// </summary>
        /// <param name="track">The track to store the metadata of</param>
        /// <param name="writeOut">Whether or not the metadata change should be written to the file</param>
        public async Task StoreTrackMetadataAsync(Pub.Track track, bool writeOut)
        {
            using (var context = new Entities())
            {
                // Iterate over the metadatas and store new objects for each
                // Skip values that are null (ie, they should be deleted)
                foreach (var metadata in track.Metadata.Where(m => m.Value != null))
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
                await context.SaveChangesAsync();
            }
        }

        #endregion

        #region Retrieval Methods

        /// <summary>
        /// Fetch the allowed metadata fields from the database.
        /// </summary>
        /// TODO: Add caching
        /// <returns>Dictionary of metadata field names to metadata ids</returns>
        public Dictionary<string, int> GetAllowedMetadataFields()
        {
            using (var context = new Entities())
            {
                // Grab all the metadata fields
                return context.MetadataFields.Select(f => new { f.TagName, f.Id }).ToDictionary(o => o.TagName, o => o.Id);
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
                                Value = md.Value
                            };

                return items.Any()
                    ? items.ToArray()
                    : new Pub.MetadataChange[] { };
            }
        }

        #endregion

        #region Update Methods

        #endregion

        #region Deletion Methods

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

        /// <summary>
        /// Deletes all metadata for a given track. Should only be used when marking a track into
        /// an error state.
        /// </summary>
        /// <param name="trackGuid">GUID of the track to delete the metadata for</param>
        public async Task DeleteAllMetadataAsync(Guid trackGuid)
        {
            using (var context = new Entities())
            {
                // Delete all the metadata for the given track
                var trackMetadatas = context.Metadatas.Where(m => m.Track1.GuidId == trackGuid);
                context.Metadatas.RemoveRange(trackMetadatas);
                await context.SaveChangesAsync();
            }
        }

        #endregion
    }
}
