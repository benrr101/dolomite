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
        /// <param name="retreiveStream">
        /// Whether or not Azure should be contacted to retreive the file's stream
        /// </param>
        /// <returns>Object representation of the track</returns>
        public Track GetTrack(string trackGuid, bool retreiveStream = true)
        {
            // TODO: Remove the temp location stuff. Replace with real quality logic
            string trackPath = "temp/" + trackGuid;

            return new Track
                {
                    Id = Guid.Parse(trackGuid),
                    // TODO: Replace with metadata retrieval logic
                    Metadata = new Dictionary<string, string>
                        {
                            {"Title", "Hello World"},
                            {"Artist", "Yo Sup"}
                        },
                    FileStream = retreiveStream ? AzureStorageManager.GetBlob(StorageContainerKey, trackPath) : null
                };
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
        /// Checks to see a track exists based on its hash
        /// </summary>
        /// <param name="hash">The hash to search with</param>
        /// <returns>True if the track exists, false otherwise</returns>
        public bool TrackExists(string hash)
        {
            return DatabaseManager.GetTrackByHash(hash) != null;
        }

        /// <summary>
        /// Uploads the track to the system. Places the track in temporary
        /// azure storage then kicks off threads to do the rest of the work.
        /// </summary>
        /// <param name="stream">Stream of the uploaded track</param>
        /// <returns>The guid for identifying the track</returns>
        public Guid UploadTrack(Stream stream)
        {
            // Step 1: Upload the track to temporary storage in azure
            Guid trackGuid = Guid.NewGuid();
            LocalStorageManager.StoreStream(stream, trackGuid.ToString());

            // Step 2: Create the inital record of the track in the database
            DatabaseManager.CreateInitialTrackRecord(trackGuid);

            // TODO: Step 2: Read the metadata from the track

            // TODO: Step 3: Create a track object for the track

            // TODO: Step 4: Create the various qualities of the track

            // Return the guid to the calling system
            return trackGuid;
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
