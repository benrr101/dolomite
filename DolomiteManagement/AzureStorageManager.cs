using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using DolomiteManagement.Asynchronous;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace DolomiteManagement
{
    public class AzureStorageManager
    {

        /// <summary>
        /// The key in the cloud configuration management for the storage connection string
        /// </summary>
        public const string ConnectionStringKey = "StorageConnectionString";

        /// <summary>
        /// Internal instance of the blob client
        /// </summary>
        private CloudBlobClient BlobClient { get; set; }

        /// <summary>
        /// The storage connection string
        /// </summary>
        public static string StorageConnectionString { get; set; }

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
            // Make sure that the storage connection is set before using it
            if (String.IsNullOrWhiteSpace(StorageConnectionString))
                throw new InvalidOperationException("Azure storage string has not been initialized.");

            // Create a client for accessing the Azure storage
            CloudStorageAccount account = CloudStorageAccount.Parse(StorageConnectionString);
            BlobClient = account.CreateCloudBlobClient();
        }

        #endregion

        #region Store Methods

        /// <summary>
        /// Stores the given stream into a block blob in the given storage
        /// container.
        /// </summary>
        /// <param name="containerName">Name of the container to store the blob to</param>
        /// <param name="fileName">The path for the file to be stored</param>
        /// <param name="bytes">A stream of the bytes to store</param>
        [Obsolete]
        public void StoreBlob(string containerName, string fileName, Stream bytes)
        {
            Trace.TraceInformation("Attempting to upload block blob '{0}' to container '{1}'", fileName, containerName);
            try
            {
                // Grab the container that is being used
                CloudBlobContainer container = BlobClient.GetContainerReference(containerName);
                
                // Grab the 
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

        /// <summary>
        /// Start the upload of a blob to the blob storage
        /// </summary>
        /// <param name="containerName">Name of the container name</param>
        /// <param name="fileName">Target name of the file in Azure</param>
        /// <param name="bytes">The bytes to upload to Azure</param>
        /// <param name="callback">The callback to perform when completed</param>
        /// <param name="state">The object to pass to the callback</param>
        /// @TODO Should replace the type of the asynchronous state to some inheritance thingy
        [Obsolete]
        public void StoreBlobAsync(string containerName, string fileName, Stream bytes, AsyncCallback callback, AzureAsynchronousState state)
        {
            try
            {
                // Grab the container that is being used
                CloudBlobContainer container = BlobClient.GetContainerReference(containerName);

                Trace.TraceInformation("Attempting asynchronous upload of block blob '{0}' to container '{1}'", fileName, containerName);

                // Grab the 
                CloudBlockBlob block = container.GetBlockBlobReference(fileName);
                state.Blob = block;
                block.BeginUploadFromStream(bytes, callback, state);
            }
            catch (Exception e)
            {
                Trace.TraceError("Failed to start upload of block blob {0}: {1}", fileName, e.Message);
                throw;
            }
        }

        /// <summary>
        /// Stores a blob in Azure storage asynchronously.
        /// </summary>
        /// <param name="containerName"></param>
        /// <param name="sourceFile"></param>
        /// <param name="destFile"></param>
        /// <returns></returns>
        public async Task StoreBlobAsync(string containerName, string sourceFile, string destFile)
        {
            using (FileStream stream = File.OpenRead(sourceFile))
            {
                // Grab the container that is being used
                CloudBlobContainer container = BlobClient.GetContainerReference(containerName);

                // Grab a reference to the file that will be created
                CloudBlockBlob block = container.GetBlockBlobReference(destFile);
                await block.UploadFromStreamAsync(stream);
            }
        }

        #endregion

        #region Retrieve Methods

        /// <summary>
        /// Retrieve a specific track at a the given path.
        /// </summary>
        /// <param name="path">The path to find the track</param>
        /// <param name="containerName">The name of the container to retrieve the blob from</param>
        /// <returns>
        /// A stream that represents contains the track. 
        /// The position will be reset to the beginning.
        /// </returns>
        /// TODO: Use aync calls
        public Stream GetBlob(string containerName, string path)
        {
            try
            {
                // Retreive a reference to the track container
                Trace.TraceInformation("Attempting to retrieve '{0}' from container '{1}'", path, containerName);
                CloudBlobContainer container = BlobClient.GetContainerReference(containerName);

                // Retreive the track into a memory stream
                MemoryStream memStream = new MemoryStream();
                CloudBlockBlob blob = container.GetBlockBlobReference(path);
                if (!blob.Exists())
                {
                    throw new FileNotFoundException(String.Format("Failed to find blob {0} in container {1}", path, container));
                }
                blob.DownloadToStream(memStream);
                memStream.Position = 0;
                return memStream;
            }
            catch (Exception e)
            {
                Trace.TraceError("Failed to retrieve blob {0}: {1}", path, e.Message);
                throw;
            }
        }

        /// <summary>
        /// Asynchronously downloads a Azure blob to a file location. 
        /// </summary>
        /// <param name="containerName"></param>
        /// <param name="path"></param>
        /// <param name="outputStream"></param>
        /// <returns></returns>
        public async Task DownloadBlobAsync(string containerName, string path, FileStream outputStream)
        {
            try
            {
                CloudBlobContainer container = BlobClient.GetContainerReference(containerName);
                ICloudBlob blob = await container.GetBlobReferenceFromServerAsync(path);
                await blob.DownloadToStreamAsync(outputStream);
            }
            catch (Exception e)
            {
                Trace.TraceError("Failed to retrieve blob {0}: {1}", path, e.Message);
                throw;
            }
        }

        #endregion

        #region Deletion Methods

        /// <summary>
        /// Deletes the blob with the given path from the given container
        /// </summary>
        /// <param name="containerName">The name of the container that houses the blob</param>
        /// <param name="path">Path of the blob</param>
        public void DeleteBlob(string containerName, string path)
        {
            // Retreive a reference to the container
            Trace.TraceInformation("Attempting to delete '{0}' from container '{1}'", path, containerName);
            CloudBlobContainer container = BlobClient.GetContainerReference(containerName);

            // Create a reference to the blob and delete it
            CloudBlockBlob blob = container.GetBlockBlobReference(path);
            blob.DeleteIfExists();
        }

        #endregion

        #region Initialization Methods

        /// <summary>
        /// Attempts to create a container with the specified.
        /// </summary>
        /// <param name="containerName">Name of the container to create</param>
        public void InitializeContainer(string containerName)
        {
            // Check for the existence of the container
            CloudBlobContainer container = BlobClient.GetContainerReference(containerName);
            if (container.Exists())
                return;

            // Container does not exist. Create it
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

        #region Utility Methods

        /// <summary>
        /// Combines components of a path to build an Azure blob URL
        /// </summary>
        /// <param name="components">A list of path fragments to combine together</param>
        /// <returns>The combined fragments to make a full blob URL</returns>
        public static string CombineAzurePath(params string[] components)
        {
            return String.Join("/", components);
        }

        #endregion
    }
}
