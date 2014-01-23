using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using IO = System.IO;
using DolomiteModel;
using DolomiteModel.PublicRepresentations;
using TagLib;

namespace DolomiteWcfService.Threads
{
    class MetadataWriting
    {

        #region Properties and Constants

        private const int SleepSeconds = 10;

        private static TrackDbManager DatabaseManager { get; set; }

        private static LocalStorageManager LocalStorageManager { get; set; }

        private static AzureStorageManager AzureStorageManager { get; set; }

        #endregion

        #region Start/Stop Logic

        private volatile bool _shouldStop;

        /// <summary>
        /// Sets the stop flag on the thread loop
        /// </summary>
        public void Stop()
        {
            _shouldStop = true;
        }

        #endregion

        public void Run()
        {
            // Set up the thread with some managers
            DatabaseManager = TrackDbManager.Instance;
            LocalStorageManager = LocalStorageManager.Instance;
            AzureStorageManager = AzureStorageManager.Instance;

            // Loop until the stop flag has been flown
            while (!_shouldStop)
            {
                // Try to get a work item
                Guid? workItemId = DatabaseManager.GetMetadataWorkItem();
                if (workItemId.HasValue)
                {
                    // We have work to do!
                    Trace.TraceInformation("Work item {0} picked up by {1}", workItemId.Value, GetHashCode());

                    try
                    {
                        // Step 1: Get the track from the db and the metadata to write to the file
                        Track track = DatabaseManager.GetTrackByGuid(workItemId.Value);
                        var metadata = DatabaseManager.GetMetadataToWriteOut(track.Id);

                        // Step 2: Store the track's original stream to local storage
                        string azurePath = IO.Path.Combine(new[]
                        {
                            "original",
                            track.Id.ToString()
                        });
                        string localPath = CopyToLocalStorage(track, azurePath);

                        // Step 3: Update the ID3 of the file in local storage
                        UpdateLocalFile(localPath, metadata);

                        // Step 4: Move the file back to azure storage
                        CopyToAzureStorage(localPath, azurePath);

                        // Step 5: Release the lock and unflag the metadata
                        // If there was metadata that isn't file-supported, this will be
                        // still be set in the DB, but there's no need to flag it any more.
                        LocalStorageManager.DeleteFile(localPath);
                    }
                    catch (Exception e)
                    {
                        Trace.TraceError("Failed to update metadata on original file: {0}", e.Message);
                    }
                    DatabaseManager.ReleaseAndCompleteMetadataItem(workItemId.Value);
                }
                else
                {
                    Trace.TraceInformation("No metadata items for {0}. Sleeping...", GetHashCode());
                    Thread.Sleep(TimeSpan.FromSeconds(SleepSeconds));
                }
            }
        }

        /// <summary>
        /// Copies the original track file from Azure storage to the local storage for
        /// processing by other methods.
        /// </summary>
        /// <param name="workItem">A track object to process</param>
        /// <param name="azurePath">The path of the original path in azure.</param>
        /// <returns>The path to the file in local storage</returns>
        public string CopyToLocalStorage(Track workItem, string azurePath)
        {
            // Get the stream from Azure
            IO.Stream origStream = AzureStorageManager.GetBlob(TrackManager.StorageContainerKey, azurePath);

            // Copy the stream to local storage
            string localPath = String.Format("{0}.{1}",
                LocalStorageManager.GetPath(workItem.Id.ToString()),
                workItem.Metadata["Original Format"]);
            IO.Stream localFile = IO.File.Create(localPath);
            origStream.CopyTo(localFile);

            // We only need the path for future ops, so close the stream
            origStream.Close();
            localFile.Close();

            return localPath;
        }

        /// <summary>
        /// Updates the metadata for the track in local storage using TagLib
        /// </summary>
        /// <param name="filePath">Path to the file in local storage</param>
        /// <param name="metadata">The metadata that needs to change</param>
        public void UpdateLocalFile(string filePath, Dictionary<string, string> metadata)
        {
            // Generate a TagLib file for writing the tags
            File file = File.Create(filePath);

            // Use reflection to get/set the appropriate tags in the file
            Type tagType = file.Tag.GetType();
            foreach (var md in metadata)
            {
                PropertyInfo property = tagType.GetProperty(md.Key);
                property.SetValue(file.Tag, Convert.ChangeType(md.Value, property.PropertyType));
            }

            // Write the changes
            file.Save();
            file.Dispose();
        }

        /// <summary>
        /// Copies the track in local storage back to Azure
        /// </summary>
        /// <param name="localPath">The local path to the file to copy</param>
        /// <param name="remotePath">The path to copy the file to in azure storage</param>
        public void CopyToAzureStorage(string localPath, string remotePath)
        {
            // Create a handle to the file
            IO.Stream stream = IO.File.OpenRead(localPath);

            // Copy the file to azure
            AzureStorageManager.StoreBlob(TrackManager.StorageContainerKey, remotePath, stream);
            stream.Close();
        }

    }
}
