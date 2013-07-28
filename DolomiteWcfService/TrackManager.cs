using System;
using System.IO;

namespace DolomiteWcfService
{
    class TrackManager
    {

        #region Constants

        private const string StorageContainerKey = "trackStorageContainer";

        private const string TempStorageDirectory = "temp/";

        #endregion

        #region Properties and Member Variables

        private AzureStorageManager AzureStorageManager { get; set; }

        private readonly string _trackContainerName;

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
            _trackContainerName = Properties.Settings.Default[StorageContainerKey].ToString();

            // Get an instance of the azure storage manager
            AzureStorageManager = AzureStorageManager.Instance;

            // Make sure the track container exists
            AzureStorageManager.InitializeContainer(_trackContainerName);
        }

        #endregion

        #region Public Methods

        public void UploadTrack(Stream stream)
        {
            // Step 1: Upload the track to temporary storage in azure
            string filePath = TempStorageDirectory + Guid.NewGuid();
            AzureStorageManager.StoreBlob(filePath, _trackContainerName, stream);

            // TODO: Step 2: Read the metadata from the track

            // TODO: Step 3: Create a track object for the track

            // TODO: Step 4: Create the various qualities of the track
        }

        #endregion

    }
}
