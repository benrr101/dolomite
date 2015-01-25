using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using IO = System.IO;
using DolomiteManagement;
using DolomiteModel;
using DolomiteModel.PublicRepresentations;
using TagLib;

namespace DolomiteBackgroundProcessing
{
    class MetadataWriting
    {

        #region Properties and Constants

        private const int SleepSeconds = 10;

        private static TrackDbManager TrackDatabaseManager { get; set; }

        private static LocalStorageManager LocalStorageManager { get; set; }

        private static AzureStorageManager AzureStorageManager { get; set; }

        public static string TrackStorageContainer { private get; set; }

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
            TrackDatabaseManager = TrackDbManager.Instance;
            LocalStorageManager = LocalStorageManager.Instance;
            AzureStorageManager = AzureStorageManager.Instance;

            // Loop until the stop flag has been flown
            while (!_shouldStop)
            {
                // Check for metadata work items
                long? workItemId = TrackDatabaseManager.GetMetadataWorkItem();
                if (workItemId.HasValue)
                {
                    // We have work to do!
                    Trace.TraceInformation("Metadata work item {0} picked up by {1}", workItemId, GetHashCode());

                    try
                    {
                        // Step 1: Get the track from the db and the metadata to write to the file
                        Track track = TrackDatabaseManager.GetTrackByInternalId(workItemId.Value);
                        var metadata = TrackDatabaseManager.GetMetadataToWriteOut(track.Id);

                        // Step 2: Store the track's original stream to local storage
                        string azurePath = IO.Path.Combine(new[]
                        {
                            "original",
                            track.Id.ToString()
                        });
                        string localTrackPath = String.Format("{0}.{1}",
                            LocalStorageManager.GetPath(track.Id.ToString()),
                            track.Metadata["OriginalFormat"]);
                        CopyToLocalStorage(azurePath, localTrackPath);

                        // Step 3: Update the ID3 of the file in local storage
                        UpdateLocalFileMetadata(localTrackPath, metadata);

                        // Step 4:  Process an art change if there is one
                        // We do this here to avoid pulling the file from azure multiple times
                        if (track.ArtChange)
                            ProcessArtChange(track, false);

                        // Step 4: Move the file back to azure storage
                        CopyToAzureStorage(localTrackPath, azurePath);

                        // Step 5: Remove the file from local storage
                        // If there was metadata that isn't file-supported, this will be
                        // still be set in the DB, but there's no need to flag it any more.
                        LocalStorageManager.DeleteFile(localTrackPath);

                        // Step 6: Purge empty tags from the database. There's no reason to keep them.
                        TrackDatabaseManager.DeleteEmptyMetadata(track.Id);
                    }
                    catch (Exception e)
                    {
                        Trace.TraceError("Failed to update metadata on original file: {0}", e.Message);
                    }
                    TrackDatabaseManager.ReleaseAndCompleteMetadataItem(workItemId.Value);
                    TrackDatabaseManager.ReleaseAndCompleteArtItem(workItemId.Value);
                }
                else
                {
                    Trace.TraceInformation("No metadata items for {0}.", GetHashCode());
                }

                // Check for art work items
                long? artWorkItem = TrackDatabaseManager.GetArtWorkItem();
                if (artWorkItem.HasValue)
                {
                    try
                    {
                        // Get the track, process it, release it to the wild
                        Track track = TrackDatabaseManager.GetTrackByInternalId(artWorkItem.Value);
                        ProcessArtChange(track, true);

                        TrackDatabaseManager.ReleaseAndCompleteArtItem(artWorkItem.Value);
                    }
                    catch (Exception e)
                    {
                        Trace.TraceError("Failed to update album art on original file: {0}", e.Message);
                    }
                }   
                else
                {
                    Trace.TraceInformation("No art items for {0}. Sleeping...", GetHashCode());
                    Thread.Sleep(TimeSpan.FromSeconds(SleepSeconds));
                }
            }
        }

        /// <summary>
        /// Handles art changes to files. This will (optionally) copy the original
        /// track file down from Azure, pull down a memory stream of the new art
        /// from Azure, update the track file's art, and (optionally) copy the
        /// file back to Azure and delete it from local storage.
        /// </summary>
        /// <param name="track">The track object that has art to update.</param>
        /// <param name="copyFromAzure">
        /// Whether or not to copy the track file down from Azure. If false, it is
        /// assumed the file is stored in the onboarding storage folder. If true,
        /// the file is also copied back to Azure when finished and removed from
        /// the onboarding storage.
        /// </param>
        private static void ProcessArtChange(Track track, bool copyFromAzure)
        {
            try
            {
                // Determine the local path
                string localTrackPath = String.Format("{0}.{1}", LocalStorageManager.GetPath(track.Id.ToString()),
                        track.Metadata["OriginalFormat"]);
                string azureTrackPath = IO.Path.Combine(new[] { "original", track.Id.ToString() });

                // Step 1: Copy from azure, if necessary
                if (copyFromAzure)
                {
                    CopyToLocalStorage(azureTrackPath, localTrackPath);
                }              

                // Step 2: Decide what to do
                if (!track.ArtId.HasValue)
                {
                    // Step 2a: Delete existing picture
                    UpdateLocalFileArt(localTrackPath, null);
                }
                else
                {
                    // Step 2b1: Get the art file from azure
                    string azureArtPath = IO.Path.Combine(new[] {"art", track.ArtId.ToString()});
                    IO.Stream artStream = AzureStorageManager.GetBlob(TrackStorageContainer, azureArtPath);

                    // Step 2b2: Store the art in the original file
                    UpdateLocalFileArt(localTrackPath, artStream);
                }

                if (copyFromAzure)
                {
                    // Write the file back to azure
                    CopyToAzureStorage(localTrackPath, azureTrackPath);

                    // Delete the local file. We're done with it.
                    LocalStorageManager.DeleteFile(localTrackPath);
                }
            }
            catch (Exception e)
            {
                Trace.TraceError("Failed to update album art on original file: {0}", e.Message);
            }
        }

        /// <summary>
        /// Copies the file from Azure storage to the local storage for
        /// processing by other methods.
        /// </summary>
        /// <param name="azurePath">The path of the original path in azure.</param>
        /// <param name="localPath">The path to store the file locally</param>
        private static void CopyToLocalStorage(string azurePath, string localPath)
        {
            // Get the stream from Azure
            IO.Stream origStream = AzureStorageManager.GetBlob(TrackStorageContainer, azurePath);

            // Copy the stream to local storage
            IO.Stream localFile = IO.File.Create(localPath);
            origStream.CopyTo(localFile);

            // We only need the path for future ops, so close the stream
            origStream.Close();
            localFile.Close();
        }

        /// <summary>
        /// Updates the metadata for the track in local storage using TagLib
        /// </summary>
        /// <param name="filePath">Path to the file in local storage</param>
        /// <param name="metadata">The metadata that needs to change</param>
        private static void UpdateLocalFileMetadata(string filePath, IEnumerable<MetadataChange> metadata)
        {
            // Generate a TagLib file for writing the tags
            File file = File.Create(filePath);

            // Use reflection to get/set the appropriate tags in the file
            Type tagType = file.Tag.GetType();
            foreach (var md in metadata)
            {
                string tagName = md.Array ? md.TagName + "s" : md.TagName;

                PropertyInfo property = tagType.GetProperty(tagName);
                property.SetValue(file.Tag, md.Array
                    ? new[] {String.IsNullOrWhiteSpace(md.Value) ? null : md.Value}
                    : String.IsNullOrWhiteSpace(md.Value) ? null : Convert.ChangeType(md.Value, property.PropertyType));
            }

            // Write the changes
            file.Save();
            file.Dispose();
        }

        /// <summary>
        /// Performs an art update to a local file.
        /// </summary>
        /// <param name="localTrackPath">The location of the file to update in local storage</param>
        /// <param name="artStream">The a stream that contains the art file to store in the file</param>
        private static void UpdateLocalFileArt(string localTrackPath, IO.Stream artStream)
        {
            // Generate a TagLib file for writing the image
            File file = File.Create(localTrackPath);

            // Clean the pictures if the art is null, otherwise store it as a picture
            file.Tag.Pictures = artStream == null ? new IPicture[0] : new IPicture[] {new Picture(ByteVector.FromStream(artStream)) };

            // Write 'dem changes
            file.Save();
            file.Dispose();
        }

        /// <summary>
        /// Copies the track in local storage back to Azure
        /// </summary>
        /// <param name="localPath">The local path to the file to copy</param>
        /// <param name="remotePath">The path to copy the file to in azure storage</param>
        private static void CopyToAzureStorage(string localPath, string remotePath)
        {
            // Create a handle to the file
            IO.Stream stream = IO.File.OpenRead(localPath);

            // Copy the file to azure
            AzureStorageManager.StoreBlob(TrackStorageContainer, remotePath, stream);
            stream.Close();
        }

    }
}
