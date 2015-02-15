using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DolomiteManagement.Asynchronous;
using DolomiteManagement.Exceptions;
using TagLib;
using DolomiteModel;
using DolomiteModel.PublicRepresentations;

namespace DolomiteManagement
{
    public class TrackManager
    {

        #region Constants

        public const string ArtDirectory = "art";

        public const string OnboardingDirectory = "onboarding";

        #endregion

        #region Properties and Member Variables

        private AzureStorageManager AzureStorageManager { get; set; }

        private TrackDbManager DatabaseManager { get; set; }

        public static string TrackStorageContainer { private get; set; }

        private LocalStorageManager LocalStorageManager { get; set; }

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
            // Get an instance of the azure storage manager
            AzureStorageManager = AzureStorageManager.Instance;

            // Make sure the track container exists
            AzureStorageManager.InitializeContainer(TrackStorageContainer);

            // Get an instance of the database manager
            DatabaseManager = TrackDbManager.Instance;

            // Get an instance of the local storage manager
            LocalStorageManager = LocalStorageManager.Instance;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Deletes the track with the given GUID from the database and Azure storage
        /// </summary>
        /// <
        /// <param name="trackGuid">The GUID of the track to delete.</param>
        /// <param name="owner">The owner of the session</param>
        public void DeleteTrack(Guid trackGuid, string owner)
        {
            // Does the track exist?
            Track track = DatabaseManager.GetTrack(trackGuid, owner);

            // TODO: Verify that we can't have inconsistent states for the database

            // Delete the track from Azure
            foreach (string path in track.Qualities.Select(quality => quality.Directory + '/' + trackGuid))
            {
                AzureStorageManager.DeleteBlob(TrackStorageContainer, path);
            }

            // Delete the record for the track in the database
            DatabaseManager.DeleteTrack(trackGuid, owner);
        }

        /// <summary>
        /// Gets total count of tracks and total play time of the tracks
        /// </summary>
        /// <param name="owner">The username of the owner to get the tracks of</param>
        /// <returns>Dictionary of count of tracks and total time in seconds</returns>
        public Dictionary<string, string> GetTotalTrackInfo(string owner)
        {
            // Retrieve the list of tracks from the database
            List<Track> tracks = DatabaseManager.GetAllTracksByOwner(owner);

            // Build the list of attributes to return
            return new Dictionary<string, string>
            {
                {"Count", tracks.Count.ToString(CultureInfo.CurrentCulture)},
                {"TotalTime", tracks.Sum(t => int.Parse(t.Metadata["Duration"])).ToString(CultureInfo.CurrentCulture)}
            };
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
            Track track = DatabaseManager.GetTrack(trackGuid, owner);

            return track;
        }

        /// <summary>
        /// Fetches the stream representing a track with a specific quality
        /// from the database.
        /// </summary>
        /// <exception cref="TrackNotReadyException">
        /// When the track's ready flag hasn't been set
        /// </exception>
        /// <exception cref="UnsupportedFormatException">
        /// When the track does not have the requested quality
        /// </exception>
        /// <param name="trackGuid">The GUID of the track to fetch</param>
        /// <param name="quality">The quality of the track to fetch</param>
        /// <param name="owner">The username of the track's owner</param>
        /// <returns>A stream for the matching guid and quality</returns>
        /// TODO: Eliminate refetching the track, or find some way to place the track stream in the track object
        public Track.Quality GetTrackStream(Guid trackGuid, string quality, string owner)
        {
            // Find the correct track and make sure it's ready
            Track track = DatabaseManager.GetTrack(trackGuid, owner);
            if (!track.Ready)
                throw new TrackNotReadyException();

            // Find the correct quality
            Track.Quality qualityObj = track.Qualities.FirstOrDefault(q => q.Directory == quality);
            if (qualityObj == null)
            {
                string message = String.Format("The track with guid {0} and quality {1} does not exist", 
                    track.Id, quality);
                throw new UnsupportedFormatException(message);
            }

            // Fetch the file from Azure
            qualityObj.FileStream = AzureStorageManager.GetBlob(TrackStorageContainer, qualityObj.Directory + "/" + track.Id);
            return qualityObj;
        }

        /// <summary>
        /// Retrieves the stream and mimetype of the track art object
        /// </summary>
        /// <param name="artGuid">The guid of the art object</param>
        /// <returns>An art object with the stream for the art from Azure</returns>
        public Art GetTrackArt(Guid artGuid)
        {
            // Grab the art object from the database
            var art = DatabaseManager.GetArt(artGuid);
            
            string path = ArtDirectory + "/" + art.Id;
            art.ArtStream = AzureStorageManager.GetBlob(TrackStorageContainer, path);
            return art;
        }

        /// <summary>
        /// Retrieve an index of all the tracks in the database
        /// </summary>
        /// <returns>List of track objects in the database</returns>
        public List<Track> FetchAllTracksByOwner(string username)
        {
            return DatabaseManager.GetAllTracksByOwner(username);
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
            // Step 0: Fetch the track (to ensure it exists and the owner is correct)
            Track track = DatabaseManager.GetTrack(guid, owner);

            // Step 0.5: Calculate hash to determine if the track is a duplicate
            hash = LocalStorageManager.CalculateHash(stream, owner);

            // Step 1: Upload the track to temporary storage in azure, asynchronously
            string azurePath = OnboardingDirectory + '/' + guid;
            UploadAsynchronousState state = new ReplaceAsynchronousState
            {
                Owner = owner,
                Stream = stream,
                TrackId = track.InternalId,
                TrackHash = hash
            };
            AzureStorageManager.StoreBlobAsync(TrackStorageContainer, azurePath, stream, ReplaceTrackAsyncCallback, state);
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
            Track track = DatabaseManager.GetTrack(guid, owner);

            // Only delete the fields that need to be deleted
            IEnumerable<string> fieldsToDelete = clearAll ? track.Metadata.Keys : metadata.Keys;
            foreach (string field in fieldsToDelete)
            {
                DatabaseManager.DeleteMetadata(guid, field);
            }

            // Store the new values
            DatabaseManager.StoreTrackMetadata(track, metadata, true);
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
            // Step 0) Fetch the track and verify its owner
            Track track = DatabaseManager.GetTrack(guid, owner);
            long? oldArtId = track.ArtId;

            // Step 1) Determine what the new art id's should be
            long? newArtId;
            if (stream.Length > 0)
            {
                // We have art! Have we already uploaded it?
                // TODO: This is kinda broken since it does a collision check on the track table.
                string hash = LocalStorageManager.CalculateHash(stream, owner);
                newArtId = DatabaseManager.GetArtIdByHash(hash);
                if (newArtId == default(long))
                {
                    // Art was not found, we need to upload it!
                    // Step 1) Determine if it is a valid mimetype
                    string mimetype = MimetypeDetector.GetImageMimetype(stream);
                    if(mimetype == null)
                        throw new UnsupportedFormatException("The image format provided was either invalid or not supported.");

                    // Step 2) Pick a guid for the art
                    Guid newArtGuid = Guid.NewGuid();

                    // Step 3) Decide where the art is going to live and upload it
                    string artPath = String.Format("{0}/{1}", ArtDirectory, newArtGuid);
                    AzureStorageManager.StoreBlob(TrackStorageContainer, artPath, stream);
                    newArtId = DatabaseManager.CreateArtRecord(newArtGuid, mimetype, hash);
                }
            }
            else
            {
                // We don't have art! Set everything to null.
                newArtId = null;
            }

            // Step 2) Reset the track's art
            DatabaseManager.SetTrackArt(track.InternalId, newArtId, true);

            // Step 3) Did we have an old art file that we may need to blow away?
            if (oldArtId.HasValue && !DatabaseManager.IsArtInUse(oldArtId.Value))
            {
                // Get the art so we can have it's guid
                Art oldArt = DatabaseManager.GetArt(oldArtId.Value);
                string oldArtPath = String.Format("{0}/{1}", ArtDirectory, oldArt.Id);
                AzureStorageManager.DeleteBlob(TrackStorageContainer, oldArtPath);

                // Delete the art from the database
                DatabaseManager.DeleteArt(oldArtId.Value);
            }
        }

        /// <summary>
        /// Does a search for tracks that match the criteria
        /// </summary>
        /// <param name="owner">Username of the owner of the tracks to search</param>
        /// <param name="criteria">A list of criteria to search with</param>
        /// <returns>A list of track guids that match the criteria</returns>
        public List<Guid> SearchTracks(string owner, Dictionary<string, string> criteria)
        {
            return DatabaseManager.SearchTracks(owner, criteria);
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
            // Step 0: Calculate hash to determine if the track is a duplicate
            hash = LocalStorageManager.CalculateHash(stream, owner);

            // Step 1: Upload the track to temporary storage in azure, asynchronously
            guid = Guid.NewGuid();
            string azurePath = OnboardingDirectory + '/' + guid;
            UploadAsynchronousState state = new NewTrackAsynchronousState
            {
                Owner = owner,
                Stream = stream,
                TrackGuid = guid,
                TrackHash = hash
            };

            AzureStorageManager.StoreBlobAsync(TrackStorageContainer, azurePath, stream, UploadTrackAsyncCallback, state);
        }

        /// <summary>
        /// Callback for when the upload to Azure has completed
        /// </summary>
        /// <param name="state">The result from the async call</param>
        private void UploadTrackAsyncCallback(IAsyncResult state)
        {
            // Verify the async state object
            NewTrackAsynchronousState asyncState = state.AsyncState as NewTrackAsynchronousState;
            if (asyncState == null)
            {
                // Something really went wrong.
                throw new InvalidDataException("Expected UploadAsynchronousState object.");
            }

            // Create the initial DB record for the track in the DB
            DatabaseManager.CreateInitialTrackRecord(asyncState.Owner, asyncState.TrackGuid, asyncState.TrackHash);

            // Close the stream
            asyncState.Stream.Close();
        }

        /// <summary>
        /// Callback for when the replacement track upload has completed. This
        /// will delete all existing qualities for the track and reset the
        /// onboarding status for the track
        /// </summary>
        /// <param name="state">The result from the async call</param>
        private void ReplaceTrackAsyncCallback(IAsyncResult state)
        {
            // Verify the async state object
            ReplaceAsynchronousState asyncState = state.AsyncState as ReplaceAsynchronousState;
            if (asyncState == null)
            {
                // Something really went wrong.
                throw new InvalidDataException("Expected UploadAsynchronousState object.");
            }

            // Close the stream, we're done with it
            asyncState.Stream.Close();

            // Delete existing blobs for the track in azure
            Track track = DatabaseManager.GetTrack(asyncState.TrackId);
            foreach (Track.Quality quality in track.Qualities)
            {
                string path = quality.Directory + '/' + track.Id;
                AzureStorageManager.DeleteBlob(TrackStorageContainer, path);
            }

            // Delete the album art if it is no longer in use
            if (track.ArtId.HasValue && !DatabaseManager.IsArtInUse(track.ArtId.Value))
            {
                // Delete the art from the database
                DatabaseManager.DeleteArt(track.ArtId.Value);

                // Delete the file from Azure
                string path = ArtDirectory + "/" + track.ArtId.Value;
                AzureStorageManager.DeleteBlob(TrackStorageContainer, path);
            }

            // Mark the track a needing re-onboarding
            DatabaseManager.MarkTrackAsNotOnboarderd(track.Id, asyncState.TrackHash, asyncState.Owner);
        }

        #endregion
    }
}
