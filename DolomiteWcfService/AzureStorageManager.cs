using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace DolomiteWcfService
{
    class AzureStorageManager
    {

        /// <summary>
        /// Internal instance of the blob client
        /// </summary>
        private CloudBlobClient BlobClient { get; set; }

        private string TrackContainerName { get; set; } 

        #region Singleton Instance Code

        private static AzureStorageManager _instance;

        /// <summary>
        /// Singleton instance of the Azure Storage manager
        /// </summary>
        public static AzureStorageManager Instance
        {
            get { return _instance ?? (_instance = new AzureStorageManager()); }
        }

        /// <summary>
        /// Singleton constructor for the AzureStorageManager
        /// </summary>
        private AzureStorageManager() 
        {
            // Create a client for accessing the Azure storage
            CloudStorageAccount account = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("StorageConnectionString"));
            BlobClient = account.CreateCloudBlobClient();

            // Check for the existence of the track container
            TrackContainerName = Properties.Settings.Default["trackStorageContainer"].ToString();
            CloudBlobContainer trackContainer = BlobClient.GetContainerReference(TrackContainerName);
            if (!trackContainer.Exists())
            {
                InitializeContainer(trackContainer);
            }
        }

        #endregion

        #region Store Methods

        public void StoreTrack(string fileName, Stream bytes)
        {
            Trace.TraceInformation("Attempting to upload track to tracks/{0}", fileName);
            StoreBlob(fileName, BlobClient.GetContainerReference(TrackContainerName), bytes);
        }

        private void StoreBlob(string fileName, CloudBlobContainer container, Stream bytes)
        {
            // Get a reference to the blob to upload, and create/overwrite
            try
            {
                CloudBlockBlob block = container.GetBlockBlobReference(fileName);
                block.UploadFromStream(bytes);
                Trace.TraceInformation("Successfully stored block blob {0}", fileName);
            }
            catch (Exception e)
            {
                Trace.TraceError("Failed to upload block {0}: {1}", fileName, e.Message);
                throw;
            }
        }

        #endregion

        #region Retrieve Methods

        /// <summary>
        /// Retrieve a list of the URIs to all the files in the track container
        /// </summary>
        /// <remarks>
        /// This shouldn't be used in production version. This is really only for testing purposes.
        /// </remarks>
        /// TODO: Remove this method when uploading works flawlessly.
        /// <returns>List of URIs to the files in the track container</returns>
        public List<string> GetAllTracks()
        {
            // Retrieve reference to a previously created container.
            CloudBlobContainer container = BlobClient.GetContainerReference(TrackContainerName);

            // Loop over items within the container and output the length and URI.
            return container.ListBlobs().Select(item => item.Uri.ToString()).ToList();
        }

        /// <summary>
        /// Retreive a specific track at a the given path.
        /// </summary>
        /// <param name="path">The path to find the track</param>
        /// <returns>
        /// A stream that represents contains the track. 
        /// The position will be reset to the beginning.
        /// </returns>
        public Stream GetTrack(string path)
        {
            // Retreive a reference to the track container
            CloudBlobContainer container = BlobClient.GetContainerReference(TrackContainerName);

            // Retreive the track into a memory stream
            MemoryStream memStream = new MemoryStream();
            container.GetBlockBlobReference(path).DownloadToStream(memStream);
            memStream.Position = 0;
            return memStream;
        }

        #endregion

        #region Initialization Methods

        /// <summary>
        /// Attempts to create a container with the specified.
        /// </summary>
        /// <param name="container">Name of the container to create</param>
        private static void InitializeContainer(CloudBlobContainer container)
        {
            try
            {
                Trace.TraceWarning("Container '{0}' does not exist. Attempting to create...", container.Name);
                container.Create();
                Trace.TraceInformation("Container '{0}' created successfully", container.Name);
            }
            catch (Exception e)
            {
                Trace.TraceError("Failed to create container '{0}': {1}", container.Name, e.Message);
                throw;
            }
        }

        #endregion
    }
}
