using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TagLib;
using DolomiteModel;
using DolomiteModel.PublicRepresentations;

namespace DolomiteWcfService
{
    class TrackManager
    {

        #region Constants

        public const string ArtDirectory = "art";

        public const string StorageContainerKey = "trackStorageContainer";

        #endregion

        #region Properties and Member Variables

        private AzureStorageManager AzureStorageManager { get; set; }

        private TrackDbManager DatabaseManager { get; set; }

        private LocalStorageManager LocalStorageManager { get; set; }

        private UserManager UserManager { get; set; }

        #endregion

        #region Singleton Instance Code

        private static TrackManager _instance;

        /// <summary>
        /// Singleton instance of the track manager
        /// </summary>
        public static TrackManager Instance
        {
            get { return _instance ?? (_instance = new TrackManager()); }
        }

        /// <summary>
        /// Singleton constructor for the Track Manager
        /// </summary>
        private TrackManager() 
        {
            // Check for the existence of the track container and store it
            if (Properties.Settings.Default[StorageContainerKey] == null)
            {
                throw new InvalidDataException("Track storage container key not set in settings.");
            }

            // Get an instance of the azure storage manager
            AzureStorageManager = AzureStorageManager.Instance;

            // Make sure the track container exists
            AzureStorageManager.InitializeContainer(StorageContainerKey);

            // Get an instance of the database manager
            DatabaseManager = TrackDbManager.Instance;

            // Get an instance of the local storage manager
            LocalStorageManager = LocalStorageManager.Instance;

            // Get an instance of the user manager
            UserManager = UserManager.Instance;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Deletes the track with the given GUID from the database and Azure storage
        /// </summary>
        /// <param name="trackGuid">The GUID of the track to delete.</param>
        /// <param name="owner">The owner of the session</param>
        public void DeleteTrack(Guid trackGuid, string owner)
        {
            // Does the track exist?
            Track track = DatabaseManager.GetTrackByGuid(trackGuid);
            if (track == null)
                throw new FileNotFoundException(String.Format("Track with guid {0} does not exist.", trackGuid));

            // Make sure the owners match
            if (track.Owner != owner)
                throw new UnauthorizedAccessException("The requested track is not owned by the session owner.");

            // TODO: Verify that we can't have inconsistent states for the database

            // Delete the track from Azure
            foreach (Track.Quality quality in track.Qualities)
            {
                string path = quality.Directory + '/' + trackGuid;
                AzureStorageManager.DeleteBlob(StorageContainerKey, path);
            }

            // Delete the record for the track in the database
            DatabaseManager.DeleteTrack(trackGuid);
        }

        /// <summary>
        /// Retreives the info about the track
        /// </summary>
        /// <param name="trackGuid">The guid of the track to retreive</param>
        /// <param name="owner">The owner of the track</param>
        /// <returns>Object representation of the track</returns>
        public Track GetTrack(Guid trackGuid, string owner)
        {
            // Retrieve the track from the database
            Track track = DatabaseManager.GetTrackByGuid(trackGuid);

            // Make sure the user owns it
            if (owner != track.Owner)
                throw new UnauthorizedAccessException("The requested track is not owned by the session owner.");

            return track;
        }

        /// <summary>
        /// Fetches the stream representing a track with a specific quality
        /// from the database.
        /// </summary>
        /// <param name="track">The track to fetch</param>
        /// <param name="quality">The quality of the track to fetch</param>
        /// <returns>A stream for the matching guid and quality</returns>
        public Track.Quality GetTrackStream(Track track, string quality)
        {
            // Find the correct quality
            var qualityObj = track.Qualities.FirstOrDefault(q => q.Directory == quality);
            if (qualityObj == null)
                throw new UnsupportedFormatException(
                    String.Format("The track with guid {0} and quality {1} does not exist", track.Id, quality));

            // Fetch the file from Azure
            qualityObj.FileStream = AzureStorageManager.GetBlob(StorageContainerKey, qualityObj.Directory + "/" + track.Id);
            return qualityObj;
        }

        /// <summary>
        /// Retrieves the stream and mimetype of the track art object
        /// </summary>
        /// <param name="artGuid">The guid of the art object</param>
        /// <param name="mimetype">The mimetype of the art is returned via this param</param>
        /// <returns>The stream for the art from Azure</returns>
        public Stream GetTrackArt(Guid artGuid, out string mimetype)
        {
            // Grab the art object from the database
            var art = DatabaseManager.GetArtByGuid(artGuid);

            mimetype = art.Mimetype;
            string path = ArtDirectory + "/" + art.Id;
            return AzureStorageManager.GetBlob(StorageContainerKey, path);
        }

        /// <summary>
        /// Retrieve an index of all the tracks in the database
        /// </summary>
        /// <returns>List of track objects in the database</returns>
        public List<Track> FetchAllTracksByOwner(string username)
        {
            // Get the tracks from the database
            var tracks = DatabaseManager.GetAllTracksByOwner(username);

            // Condense them into a list of tracks
            return tracks.ToList();
        }

        /// <summary>
        /// Replaces the track with the given guid with the given stream.
        /// This essentially deletes all the blobs in Azure storage for the
        /// old track and relaunches the onboarding process.
        /// </summary>
        /// <remarks>The hash could be returned, but I opted to use an out param
        /// b/c it follows the precedent of the uploader.</remarks>
        /// <param name="stream">The bytes to replace the track with</param>
        /// <param name="guid">The guid of the track to replace</param>
        /// <param name="owner">The username of the owner of the track</param>
        /// <param name="hash">The hash of the stream to be calculated by this method</param>
        public void ReplaceTrack(Stream stream, Guid guid, string owner, out string hash)
        {
            // Step 0: Fetch the track
            Track track = DatabaseManager.GetTrackByGuid(guid);

            // Step 0.5: Make sure the owners match
            if (track.Owner != owner)
                throw new UnauthorizedAccessException("The requested track is not owned by the session owner.");

            // Step 1: Upload the track to temporary storage
            hash = LocalStorageManager.StoreStream(stream, guid.ToString(), null);

            // Step 2: Delete existing blobs from azure
            foreach (Track.Quality quality in track.Qualities)
            {
                string path = quality.Directory + '/' + guid;
                AzureStorageManager.DeleteBlob(StorageContainerKey, path);
            }

            // Step 3: Mark the track as not onboarded
            DatabaseManager.MarkTrackAsNotOnboarderd(guid, hash);
        }

        /// <summary>
        /// Deletes and reinserts all the metadata in the dictionary of metadatas
        /// </summary>
        /// <param name="guid">GUID for the track to replace metadata for</param>
        /// <param name="owner">The username for the owner of the session</param>
        /// <param name="metadata">The metadata to replace the existing records with</param>
        /// <param name="clearAll">Whether to delete everything and start over (true) or to
        /// only replace the values that are provided (false, default)</param>
        public void ReplaceMetadata(Guid guid, string owner, Dictionary<string, string> metadata, bool clearAll = false)
        {
            // Grab the track
            Track track = DatabaseManager.GetTrackByGuid(guid);

            // Make sure the owners match
            if(track.Owner != owner)
                throw new UnauthorizedAccessException("The requested track is not owned by the session owner.");

            // Only delete the fields that need to be deleted
            IEnumerable<string> fieldsToDelete = clearAll ? track.Metadata.Keys : metadata.Keys;
            foreach (string field in fieldsToDelete)
            {
                DatabaseManager.DeleteMetadata(guid, field);
            }

            // Store the new values
            DatabaseManager.StoreTrackMetadata(guid, metadata, true);
        }

        /// <summary>
        /// Replaces the art for a given track. This also performs checks
        /// to see if the old track art has been freed. If it has been freed
        /// then it can safely be deleted.
        /// </summary>
        /// <param name="guid">The guid of the track to set the art for</param>
        /// <param name="owner">The owner of the session</param>
        /// <param name="stream">The stream representing the art file</param>
        public void ReplaceTrackArt(Guid guid, string owner, Stream stream)
        {
            // Fetch the track and verify its owner
            Track track = DatabaseManager.GetTrackByGuid(guid);
            if (track.Owner != owner)
                throw new UnauthorizedAccessException("The requested track is not owned by the session owner.");

            // Does the track have art?
            if (track.ArtId.HasValue && !DatabaseManager.DeleteArtByUsage(track.Id))
            {
                // Delete the file from Azure -- it's been deleted from the db already
                string path = ArtDirectory + "/" + track.ArtId.Value;
                AzureStorageManager.DeleteBlob(StorageContainerKey, path);
            }

            // Was there even an art file attached?
            if (stream.Length == 0)
            {
                DatabaseManager.SetTrackArt(guid, null);
                return;
            }

            // Determine the type of the file
            string mimetype = MimetypeDetector.GetImageMimetype(stream);
            if(mimetype == null)
                throw new UnsupportedFormatException("The image format provided was either invalid or not supported.");

            // Calculate the hash
            string hash = LocalStorageManager.CalculateHash(stream, null);
            var artGuid = DatabaseManager.GetArtIdByHash(hash);
            if (artGuid == Guid.Empty)
            {
                // Create a new guid for the art (not sure if the track's guid
                // would suffice or cause conflicts)
                artGuid = Guid.NewGuid();

                // We need to store the art and create a new db record for it
                string artPath = ArtDirectory + "/" + artGuid;
                AzureStorageManager.StoreBlob(StorageContainerKey, artPath, stream);
                DatabaseManager.CreateArtRecord(artGuid, mimetype, hash);
            }

            // Store the art record to the track
            DatabaseManager.SetTrackArt(guid, artGuid);
        }

        /// <summary>
        /// Uploads the track to the system. Places the track in temporary
        /// azure storage then kicks off threads to do the rest of the work. We
        /// detect duplicate tracks based on hash here.
        /// </summary>
        /// <param name="stream">Stream of the uploaded track</param>
        /// <param name="owner">The username of the owner of the track</param>
        /// <param name="guid">Output variable for the guid of the track</param>
        /// <param name="hash">Output variable for the hash of the track</param>
        /// <returns>The guid for identifying the track</returns>
        public void UploadTrack(Stream stream, string owner, out Guid guid, out string hash)
        {
            // Step 1: Upload the track to temporary storage in azure
            guid = Guid.NewGuid();
            hash = LocalStorageManager.StoreStream(stream, guid.ToString(), owner);

            // Step 2: Create the inital record of the track in the database
            DatabaseManager.CreateInitialTrackRecord(owner, guid, hash);
        }

        #endregion
    }
}
