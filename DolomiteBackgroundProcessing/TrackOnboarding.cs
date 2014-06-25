using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using DolomiteManagement;
using DolomiteModel.PublicRepresentations;
using IO = System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using DolomiteModel;
using TagLib;

namespace DolomiteBackgroundProcessing
{
    class TrackOnboarding
    {

        #region Internal Asynchronous Callback Object

        public class AsynchronousState : DolomiteManagement.Asynchronous.AzureAsynchronousState
        {
            public IO.FileStream FileStream { get; set; }
            public string OriginalPath { get; set; }
            public Guid TrackGuid { get; set; }
            public bool Delete { get; set; }
        }

        #endregion

        #region Properties and Constants

        private const int SleepSeconds = 10;

        public const string OnboardingDirectory = "onboarding";

        private static TrackDbManager DatabaseManager { get; set; }

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
            DatabaseManager = TrackDbManager.Instance;
            LocalStorageManager = LocalStorageManager.Instance;
            AzureStorageManager = AzureStorageManager.Instance;

            // Loop until the stop flag has been flown
            while (!_shouldStop)
            {                
                // Try to get a work item
                Guid? workItemId = DatabaseManager.GetOnboardingWorkItem();
                if (workItemId.HasValue)
                {
                    // We have work to do!
                    Trace.TraceInformation("Work item {0} picked up by {1}", workItemId.Value, GetHashCode());
                    
                    // Step 1: Grab the track from Azure
                    try
                    {
                        CopyFileToLocalStorage(LocalStorageManager.GetPath(workItemId.Value.ToString()), workItemId.Value);
                    }
                    catch (Exception)
                    {
                        Trace.TraceError("{1} failed to retrieve uploaded track {0} from Azure. Removing record...", workItemId, GetHashCode());
                        CancelOnboarding(workItemId.Value);
                        continue;
                    }

                    // Step 2: Grab the metadata for the track
                    try
                    {
                        StoreMetadata(workItemId.Value);
                    }
                    catch (UnsupportedFormatException)
                    {
                        // Failed to determine type. We don't want this file.
                        Trace.TraceError("{1} failed to determine the type of track {0}. Removing record...", workItemId, GetHashCode());
                        CancelOnboarding(workItemId.Value);
                        continue;
                    }
                    catch (CorruptFileException)
                    {
                        // File is corrupt for whatever reason. We don't want this file.
                        Trace.TraceError("{1} found corrupt file for track {0}. Removing record...", workItemId, GetHashCode());
                        CancelOnboarding(workItemId.Value);
                        continue;
                    }

                    // Create all the qualities for the track
                    try
                    {
                        CreateQualities(workItemId.Value);
                    }
                    catch (Exception)
                    {
                        Trace.TraceError("{1} failed to create qualities for this track {0}. Removing record...", workItemId, GetHashCode());
                        CancelOnboarding(workItemId.Value);
                        continue;
                    }

                    // Onboarding complete! Delete the temp copy! Release the lock!
                    AzureStorageManager.DeleteBlob(TrackStorageContainer, OnboardingDirectory + '/' + workItemId.Value);
                    DatabaseManager.ReleaseAndCompleteOnboardingItem(workItemId.Value);
                }
                else
                {
                    // No Work items. Sleep.
                    Trace.TraceInformation("No work items for {0}. Sleeping...", GetHashCode());
                    Thread.Sleep(TimeSpan.FromSeconds(SleepSeconds));
                }
            }
        }

        #region Onboarding Methods

        /// <summary>
        /// Utilize FFmpeg process launching to create all the necessary qualities
        /// of the track file
        /// </summary>
        /// <param name="trackGuid">The guid of the track to create a quality of</param>
        private void CreateQualities(Guid trackGuid)
        {
            // Grab the track that will be manipulated
            var track = DatabaseManager.GetTrackByGuid(trackGuid);

            // Fetch all supported qualities
            var qualitites = DatabaseManager.GetAllQualities();
            int originalQuality = track.Qualities.First(q => q.Name == "Original").BitrateKbps;

            // Figure out what qualities to use by picking all qualities with bitrates
            // lessthan or equal to the original (+/- 5kbps -- for lousy sources)
            // ReSharper disable PossibleInvalidOperationException  (this isn't possible since the fetching method does not select nulls)
            var maxQuality = qualitites.Where(q => Math.Abs(q.Bitrate - originalQuality) <= 5);
            var lessQualities = qualitites.Where(q => q.Bitrate < originalQuality);
            var requiredQualities = lessQualities.Union(maxQuality);
            // ReSharper restore PossibleInvalidOperationException

            // Generate new audio files for each quality that is required
            // @TODO Replace with parallel.foreach
            foreach (Quality quality in requiredQualities)
            {
                // Don't waste time processing the file if we have a really close match
                if (Math.Abs(quality.Bitrate - originalQuality) <= 5)
                {
                    // Copy the original file to this quality's directory
                    MoveFileToAzure(LocalStorageManager.GetPath(trackGuid.ToString()), quality.Directory, trackGuid, false);
                    continue;
                }
                
                string inputFilename = LocalStorageManager.GetPath(trackGuid.ToString());

                string outputFilename = String.Format("{0}.{1}.{2}", trackGuid, quality.Bitrate, quality.Extension);
                string outputFilePath = LocalStorageManager.GetPath(outputFilename);

                // Arguments:
                // -i {2}               - input file path
                // -vn                  - drop all video streams (including album art, as per http://stackoverflow.com/a/20202233)
                // -acodec {0}          - the codec to transcode to
                // -ab {1}000           - the bitrate in bps
                // -y {3}               - the output path
                // -map_metadata -1     - drop all metadata
                string arguments = String.Format("-i \"{2}\" -vn -acodec {0} -map_metadata -1 -ab {1}000 -y \"{3}\"", quality.Codec,
                                                 quality.Bitrate, inputFilename, outputFilePath);

                // Borrowing some code from http://stackoverflow.com/a/8726175
                ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = @"Externals\ffmpeg.exe",
                        Arguments = arguments,
                        CreateNoWindow = true,
                        ErrorDialog = false,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        RedirectStandardOutput = true,
                        RedirectStandardInput = false,
                        RedirectStandardError = true
                    };

                // Launch the process
                Trace.TraceInformation("Launching {0} {1}", psi.FileName, psi.Arguments);
                using (Process exeProcess = Process.Start(psi))
                {
                    string outString = string.Empty;
                    
                    // use ansynchronous reading for at least one of the streams
                    // to avoid deadlock
                    exeProcess.OutputDataReceived += (s, e) =>
                        {
                            outString += e.Data;
                        };
                    exeProcess.BeginOutputReadLine();
                    
                    // now read the StandardError stream to the end
                    // this will cause our main thread to wait for the
                    // stream to close (which is when ffmpeg quits)
                    string errString = exeProcess.StandardError.ReadToEnd();
                    Trace.TraceWarning(errString);
                }

                // @TODO Make sure the process call succeeded

                // Upload the file to azure
                MoveFileToAzure(outputFilePath, quality.Directory, trackGuid);

                // Store the quality record to the database
                DatabaseManager.AddAvailableQualityRecord(trackGuid, quality);
            }

            // Upload the original file
            MoveFileToAzure(LocalStorageManager.GetPath(trackGuid.ToString()), "original", trackGuid);
            DatabaseManager.AddAvailableOriginalQualityRecord(trackGuid);
        }

        /// <summary>
        /// Moves the file to azure asnynchronously
        /// </summary>
        /// <param name="tempPath">Path of the file in local storage to move</param>
        /// <param name="directory">Directory in Azure to store the file</param>
        /// <param name="trackGuid">GUID of the track that is being stored</param>
        /// <param name="deleteOnComplete">Whether or not to delete the original file when the move has completed</param>
        private void MoveFileToAzure(string tempPath, string directory, Guid trackGuid, bool deleteOnComplete = true)
        { 
            // Open a file stream to the file to move
            IO.FileStream file = LocalStorageManager.RetrieveReadableFile(tempPath);

            // Construct the target destination
            string targetPath = directory + '/' + trackGuid;

            // Start the upload of the file to azure
            AsynchronousState state = new AsynchronousState
                {
                    FileStream = file,
                    Delete = deleteOnComplete,
                    OriginalPath = tempPath,
                    TrackGuid = trackGuid
                };
            AzureStorageManager.StoreBlobAsync(TrackStorageContainer, targetPath, file, CompleteMoveFileToAzure, state);
        }

        /// <summary>
        /// Copies the file from azure to the local, temporary storage
        /// </summary>
        /// <param name="tempPath">Path for the file in local storage</param>
        /// <param name="trackGuid">The guid of the track to pull from azure</param>
        private void CopyFileToLocalStorage(string tempPath, Guid trackGuid)
        {
            // Get the stream from Azure
            string azurePath = OnboardingDirectory + '/' + trackGuid;
            IO.Stream origStream = AzureStorageManager.GetBlob(TrackStorageContainer, azurePath);

            // Copy the stream to local storage
            IO.Stream localFile = IO.File.Create(tempPath);
            origStream.CopyTo(localFile);

            // We only need the path for future ops, so close the stream
            origStream.Close();
            localFile.Close();
        }

        /// <summary>
        /// Callback for when the upload has completed.
        /// </summary>
        /// <param name="state">The state object from the callback</param>
        private void CompleteMoveFileToAzure(IAsyncResult state)
        {
            // Make sure we have the correct info back
            AsynchronousState asyncState = state.AsyncState as AsynchronousState;
            if (asyncState == null)
            {
                // Something really went wrong. Cancel the onboarding.
                throw new IO.InvalidDataException("Expected AsynchronousState object.");
            }

            // Close the upload
            asyncState.Blob.EndUploadFromStream(state);

            // Close the file and delete it
            asyncState.FileStream.Close();
            if (asyncState.Delete)
                DeleteFileWithWait(IO.Path.GetFileName(asyncState.OriginalPath));
        }

        /// <summary>
        /// Strips the metadata from the track and stores it to the database
        /// Also retrieves the mimetype in the process.
        /// </summary>
        /// <param name="trackGuid">The guid of the track to store metadata of</param>
        private void StoreMetadata(Guid trackGuid)
        {
            Trace.TraceInformation("{0} is retrieving metadata from {1}", GetHashCode(), trackGuid);

            // Generate the mimetype of the track
            // Why? b/c tag lib isn't smart enough to figure it out for me,
            // except for determining it based on extension -- which is silly.
            IO.FileStream localFile = LocalStorageManager.RetrieveReadableFile(trackGuid.ToString());
            string mimetype = MimetypeDetector.GetAudioMimetype(localFile);
            if (mimetype == null)
            {
                localFile.Close();
                throw new UnsupportedFormatException(String.Format("The mimetype of {0} could not be determined from the file header.", trackGuid));
            }

            // Retrieve the file from temporary storage
            File file = File.Create(LocalStorageManager.GetPath(trackGuid.ToString()), mimetype, ReadStyle.Average);
            
            Dictionary<string, string> metadata = new Dictionary<string, string>();
            
            // Use reflection to iterate over the properties in the tag
            PropertyInfo[] properties = typeof (Tag).GetProperties();
            foreach (PropertyInfo property in properties)
            {
                string name = property.Name;
                object value = property.GetValue(file.Tag);

                // Strip off "First" from the tag names
                name = name.Replace("First", string.Empty);

                // We really only want strings to store and ints
                if(value is string)
                    metadata.Add(name, (string)value);
                else if(value is uint && (uint)value != 0)
                    metadata.Add(name, ((uint)value).ToString(CultureInfo.CurrentCulture));
            }

            // Grab some extra data from the file
            metadata.Add("Duration", Math.Round(file.Properties.Duration.TotalSeconds).ToString(CultureInfo.CurrentCulture));
            metadata.Add("DateAdded",
                Math.Round((DateTime.Now - new DateTime(1970, 1, 1).ToLocalTime()).TotalSeconds)
                    .ToString(CultureInfo.CurrentCulture));
            metadata.Add("PlayCount", "0");
            metadata.Add("OriginalBitrate", file.Properties.AudioBitrate.ToString(CultureInfo.CurrentCulture));
            string extension = MimetypeDetector.GetExtension(file.MimeType);
            metadata.Add("OriginalFormat", extension);

            // Send the metadata to the database
            DatabaseManager.StoreTrackMetadata(trackGuid, metadata, false);

            // Store the audio metadata to the database
            DatabaseManager.SetAudioQualityInfo(trackGuid, file.Properties.AudioBitrate,
                file.Properties.AudioSampleRate, file.MimeType, extension);

            // Rip out the album art (or whatever is the first art in the file)
            if (file.Tag.Pictures.Length > 0)
            {
                StoreAlbumArt(trackGuid, file.Tag.Pictures[0]);
            }
        }

        #endregion

        /// <summary>
        /// Stores the album art for the track using the IPicture object from
        /// the taglib file. If it does not exist (based on hash of the file)
        /// the file is stored to azure and a new record is added to the database.
        /// </summary>
        /// <param name="trackGuid">The guid of the track to store the art for</param>
        /// <param name="art">The art object to store</param>
        private void StoreAlbumArt(Guid trackGuid, IPicture art)
        {
            // Grab the info
            var artMime = art.MimeType;
            var artFile = new IO.MemoryStream(art.Data.ToArray());

            // Calculate the hash of the album art
            string hash = LocalStorageManager.CalculateHash(artFile, null);
            var artGuid = DatabaseManager.GetArtIdByHash(hash);
            if (artGuid == Guid.Empty)
            {
                // The art guid is a brand new guid to avoid conflicts.
                artGuid = Guid.NewGuid();

                // We need to store the art and create a new db record for it
                string artPath = TrackManager.ArtDirectory + "/" + artGuid;
                AzureStorageManager.StoreBlob(TrackStorageContainer, artPath, artFile);

                // Create a new record for the art
                DatabaseManager.CreateArtRecord(artGuid, artMime, hash);
            }

            // Store the art record to the track
            DatabaseManager.SetTrackArt(trackGuid, artGuid, false);
        }

        /// <summary>
        /// Cancels the onboarding process by deleting all created files and
        /// removing the record of the track.
        /// </summary>
        /// <param name="workItemGuid">The GUID of the onboarding work item</param>
        private void CancelOnboarding(Guid workItemGuid)
        {
            // Delete the original file from the local storage
            DeleteFileWithWait(workItemGuid.ToString());
            AzureStorageManager.DeleteBlob(TrackStorageContainer, String.Format("original/{0}", workItemGuid));

            // Delete the original file from azure
            AzureStorageManager.DeleteBlob(TrackStorageContainer, OnboardingDirectory + '/' + workItemGuid);

            // Iterate over the qualities and delete them from local storage and azure
            foreach (Quality quality in DatabaseManager.GetAllQualities())
            {
                // Delete the local storage instance
                string fileName = String.Format("{0}.{1}.{2}", workItemGuid, quality.Bitrate, quality.Extension);
                DeleteFileWithWait(fileName);

                // Delete the file from Azure
                string azurePath = String.Format("{0}/{1}", quality.Directory, workItemGuid);
                AzureStorageManager.DeleteBlob(TrackStorageContainer, azurePath);
            }

            // Delete the track from the database
            DatabaseManager.DeleteTrack(workItemGuid);
        }
            
        /// <summary>
        /// Attempts to delete the file. If it fails, the thread sleeps until
        /// the file has been unlocked.
        /// </summary>
        /// <param name="fileName">The name of the file in onboarding storage to delete</param>
        private static void DeleteFileWithWait(string fileName)
        {
            while (true)
            {
                try
                {
                    LocalStorageManager.DeleteFile(fileName);
                    break;
                }
                catch(IO.IOException)
                {
                    Trace.TraceWarning("The file '{0}' is still in use. Sleeping until it is unlocked.", fileName);
                    Thread.Sleep(TimeSpan.FromSeconds(5));
                }
            } 
        }
    }
}
