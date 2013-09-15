using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Model = DolomiteModel;

namespace DolomiteWcfService
{
    class TrackManager
    {

        #region Constants

        public const string StorageContainerKey = "trackStorageContainer";

        private const string TempStorageDirectory = "temp/";

        #endregion

        #region Properties and Member Variables

        private AzureStorageManager AzureStorageManager { get; set; }

        private DatabaseManager DatabaseManager { get; set; }

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
            DatabaseManager = DatabaseManager.Instance;

            // Get an instance of the local storage manager
            LocalStorageManager = LocalStorageManager.Instance;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Retreives the info about the track and its stream (if requested)
        /// </summary>
        /// <param name="trackGuid">The guid of the track to retreive</param>
        /// <returns>Object representation of the track</returns>
        public Track GetTrack(Guid trackGuid)
        {
            // Retrieve the track from the database
            return DatabaseManager.GetTrackByGuid(trackGuid);
        }

        /// <summary>
        /// Retrieve an index of all the tracks in the database
        /// </summary>
        /// <returns>List of track objects in the database</returns>
        public List<Track> FetchAllTracks()
        {
            // Get the tracks from the database
            var tracks = DatabaseManager.FetchAllTracks();

            // Condense them into a list of tracks
            return tracks.ToList();
        } 

        /// <summary>
        /// Uploads the track to the system. Places the track in temporary
        /// azure storage then kicks off threads to do the rest of the work. We
        /// detect duplicate tracks based on hash here.
        /// </summary>
        /// <param name="stream">Stream of the uploaded track</param>
        /// <param name="guid">Output variable for the guid of the track</param>
        /// <param name="hash">Output variable for the hash of the track</param>
        /// <returns>The guid for identifying the track</returns>
        public void UploadTrack(Stream stream, out Guid guid, out string hash)
        {
            // Step 1: Upload the track to temporary storage in azure
            guid = Guid.NewGuid();
            hash = LocalStorageManager.StoreStream(stream, guid.ToString());

            // Step 2: Create the inital record of the track in the database
            DatabaseManager.CreateInitialTrackRecord(guid, hash);
        }

        

        #endregion

        #region Private Helper Methods

        private void UploadTrackToTempStorage(Guid guid, Stream fileStream)
        {
            string filePath = TempStorageDirectory + guid;
            AzureStorageManager.StoreBlob(StorageContainerKey, filePath, fileStream);
        }

        #endregion
    }
}
