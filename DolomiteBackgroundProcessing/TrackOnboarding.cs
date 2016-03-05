using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using DolomiteManagement;
using DolomiteModel.PublicRepresentations;
using Newtonsoft.Json;
using IO = System.IO;
using System.Linq;
using System.Threading;
using DolomiteModel;

namespace DolomiteBackgroundProcessing
{
    class TrackOnboarding
    {
        #region Properties and Constants

        private const int SleepSeconds = 10;

        public const string OnboardingDirectory = "onboarding";

        private static TrackDbManager DatabaseManager { get; set; }

        private static LocalStorageManager LocalStorageManager { get; set; }

        private static AzureStorageManager AzureStorageManager { get; set; }

        public static string TrackStorageContainer { private get; set; }

        private List<Process> _launchedProcesses; 

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

            LocalStorageManager.Instance.InitializeStorageDirectory(OnboardingDirectory);

            _launchedProcesses = new List<Process>();

            // Loop until the stop flag has been flown
            while (!_shouldStop)
            {                
                // Try to get a work item
                long? workItemId = DatabaseManager.GetOnboardingWorkItem();
                if (workItemId.HasValue)
                {
                    // We have work to do!
                    Track track = DatabaseManager.GetTrack(workItemId.Value);
                    Trace.TraceInformation("Work item {0} picked up by {1}", workItemId.Value, GetHashCode());
                    string trackFilePath = IO.Path.Combine(OnboardingDirectory, track.Id.ToString());

                    try
                    {
                        // Step 1: Grab the track from Azure
                        CopyFileToLocalStorage(trackFilePath, track.Id).Wait();

                        // Step 2: Grab the metadata for the track
                        TrackMetadata metadata;
                        using (IO.FileStream fs = LocalStorageManager.Instance.RetrieveReadableFile(trackFilePath))
                        {
                            metadata = new TrackMetadata(fs, track.OriginalMimetype);
                        }

                        // Step 3: Store the metadata to the database
                        Task[] metadataTasks =
                        {
                            StoreMetadata(track, metadata),
                            StoreOriginalQuality(track, metadata),
                            StoreAlbumArt(track, metadata)
                        };
                        Task.WaitAll(metadataTasks);

                        // Step 4: Create all the qualities for the track
                        var qualities = DetermineQualitiesToGenerate(metadata).Result;
                        Task.WaitAll(qualities.Select(q => GenerateQuality(track, q)).ToArray());

                        // Onboarding complete! Delete the temp copy! Release the lock!
                        LocalStorageManager.Instance.DeleteFile(trackFilePath);
                        string trackOnboardingAzurePath = AzureStorageManager.CombineAzurePath(OnboardingDirectory,
                            track.Id.ToString());
                        AzureStorageManager.DeleteBlobAsync(TrackStorageContainer, trackOnboardingAzurePath).Wait();
                        DatabaseManager.ReleaseAndCompleteOnboardingItem(workItemId.Value);
                    }
                    catch (Exception e)
                    {
                        CancelOnboarding(track, "something went wrong", e.Message).Wait();
                    }
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
        /// Based on the list of qualities in the database, determine what qualities are lower
        /// than the original quality of the track.
        /// </summary>
        /// <param name="metadata">Metadata from the track to convert.</param>
        private static async Task<List<Quality>> DetermineQualitiesToGenerate(TrackMetadata metadata)
        {
            // Step 1) Fetch all the supported qualities from the DB
            List<Quality> allQualities = await TrackDbManager.Instance.GetAllQualitiesAsync();

            // Step 2) Determine which qualities to use by picking all qualities with bitrates less
            // than or equal to the original (+/- 5kbps to handle lousy sources)
            var maxQuality = allQualities.Where(q => Math.Abs(q.Bitrate - metadata.BitrateKbps) <= 5);
            var lesserQualities = allQualities.Where(q => q.Bitrate < metadata.BitrateKbps);

            // Step 3) Make sure that some qualities will be created
            var qualitiesToCreate = lesserQualities.Union(maxQuality).ToList();
            if (qualitiesToCreate.Count == 0)
            {
                // TODO: Better exceptions
                throw new Exception("Quality too low! No qualities to generate!");
            }

            return qualitiesToCreate;
        }

        /// <summary>
        /// Launches an instance ffmpeg to create lower quality version of the track that is being
        /// onboarded. The lower quality track will be uploaded to Azure.
        /// </summary>
        /// <param name="track">The track to generate a lower quality of</param>
        /// <param name="quality">The quality to convert the track to</param>
        private async Task GenerateQuality(Track track, Quality quality)
        {
            // Determine the paths of the input and output
            string inputRelativeToLocalStoragePath = IO.Path.Combine(OnboardingDirectory, track.Id.ToString());
            string inputFilePath = LocalStorageManager.Instance.GetPath(inputRelativeToLocalStoragePath);
            string outputFilename = GetQualityFileName(track, quality);
            string outputLocalRelative = IO.Path.Combine(OnboardingDirectory, outputFilename);
            string outputLocalFilePath = LocalStorageManager.Instance.GetPath(IO.Path.Combine(OnboardingDirectory, outputFilename));

            // @TODO: Figure out what to do if the quality to generate is the same as the original file

            // Arguments:
            // -i {2}               - input file path
            // -vn                  - drop all video streams (including album art, as per http://stackoverflow.com/a/20202233)
            // -y {3}               - the output path
            // -map_metadata -1     - drop all metadata
            string arguments = String.Format(@"-i ""{0}"" -vn {1} -map_metadata -1 -y ""{2}""",
                inputFilePath, quality.FfmpegArgs, outputLocalFilePath);

            // Launch the process
            await LaunchProcessAsync(@"Externals\ffmpeg.exe", arguments);

            // Upload the file to Azure and delete it when we're done
            await MoveFileToAzure(outputLocalFilePath, quality.Directory);
            LocalStorageManager.Instance.DeleteFile(outputLocalRelative);

            // Store the quality record to the database
            DatabaseManager.AddAvailableQualityRecord(track, quality);
        }

        /// <summary>
        /// Stores the album art record in the database, then uploads it to Azure.
        /// </summary>
        /// <param name="track">The track to store the album art for</param>
        /// <param name="metadata">The metadata of the track to store album art for</param>
        private static async Task StoreAlbumArt(Track track, TrackMetadata metadata)
        {
            // Step 0) Make sure there is art to store
            if (metadata.ImageBytes.Length <= 0)
            {
                return;
            }

            // Step 1) Calculate a hash of the album art
            IO.MemoryStream artStream = new IO.MemoryStream(metadata.ImageBytes);
            string hash = await LocalStorageManager.CalculateMd5HashAsync(artStream);

            // Step 2) Determine if the album art has already been uploaded
            long? artId = TrackDbManager.Instance.GetArtIdByHash(hash);
            if (!artId.HasValue)
            {
                // Art has not been uploaded before, start the upload!
                // Create a new guid to prevent conflicts
                Guid artGuid = Guid.NewGuid();

                // Start a task for uploading the art
                string artPath = AzureStorageManager.CombineAzurePath(TrackManager.ArtDirectory, artGuid.ToString());

                // Setup tasks for uploading the art to blob storage and creating the record for it
                // in the DB
                var uploadTask = AzureStorageManager.Instance.StoreBlobAsync(TrackStorageContainer, artStream, artPath);
                var createTask = TrackDbManager.Instance.CreateArtRecordAsync(artGuid, metadata.ImageMimetype, hash);

                await Task.WhenAll(new[] { uploadTask, createTask });

                artId = createTask.Result;
            }

            // Step 3) Set the track's art in the DB
            await TrackDbManager.Instance.SetTrackArtAsync(track, artId, false);
        }

        /// <summary>
        /// Stores the metadata of the track into the database
        /// </summary>
        /// <param name="track">The track to store the metadata for</param>
        /// <param name="metadata">The metadata to store</param>
        private static async Task StoreMetadata(Track track, TrackMetadata metadata)
        {
            // Step 1) Inject the metadata we extracted into the track
            track.Metadata = new Dictionary<string, string>
            {
                // Step 1.1) Insert the well known track metadata
                {@"Artist", metadata.Artist},
                {@"AlbumArtist", metadata.AlbumArtist},
                {@"Album", metadata.Album},
                {@"Composer", metadata.Composer},
                {@"Performer", metadata.Performer},
                {@"Date", metadata.Date},
                {@"Genre", metadata.Genre},
                {@"Title", metadata.Title},
                {@"DiscNumber", metadata.DiscNumber},
                {@"TotalDiscs", metadata.TotalDiscs},
                {@"TrackNumber", metadata.TrackNumber},
                {@"TotalTracks", metadata.TotalTracks},
                {@"Copyright", metadata.Copyright},
                {@"Comment", metadata.Comment},
                {@"Publisher", metadata.Publisher},
                {@"DurationMilli", metadata.Duration.ToString(NumberFormatInfo.InvariantInfo)},

                // Step 1.2) Insert the Dolomite-specific metadata
                {
                    @"Dol:DateAdded", 
                    Math.Round((DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds).ToString(NumberFormatInfo.InvariantInfo)
                },
                {@"Dol:PlayCount", "0"},
                {@"Dol:SkipCount", "0"},
                {@"Dol:OriginalBitrate", metadata.BitrateKbps.ToString(NumberFormatInfo.InvariantInfo)},
                {@"Dol:OriginalCodec", metadata.Codec},
                
            };

            // Step 1.3) Add custom frames if they exist
            if (metadata.CustomFrames.Count > 0)
            {
                track.Metadata.Add(@"Dol:Custom", JsonConvert.SerializeObject(metadata.CustomFrames));
            }

            // Step 1.4) Remove any fields that are null
            track.Metadata = track.Metadata.Where(kvp => kvp.Value != null)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // Step 2) Send the metadata to the database
            await TrackDbManager.Instance.StoreTrackMetadataAsync(track, false);
        }

        /// <summary>
        /// Store the original information about the track in the database.
        /// </summary>
        /// <param name="track">The track to store the information about</param>
        /// <param name="metadata">The information to store about the track</param>
        private static async Task StoreOriginalQuality(Track track, TrackMetadata metadata)
        {
            await TrackDbManager.Instance.SetAudioQualityInfoAsync(track.InternalId, metadata.BitrateKbps, metadata.Mimetype,
                metadata.Extension);
        }

        #endregion

        /// <summary>
        /// Cleans up after a botched onboarding process by deleting any files that were generated
        /// locally and in Azure. The track record is placed in the an error state.
        /// </summary>
        /// <param name="workItem">The track that was being onboarded</param>
        /// <param name="userError">
        /// Error message that will be stored in the db and returned to the user. Do not submit any
        /// private information or internal info via this message.
        /// </param>
        /// <param name="adminError">
        /// Error message that will be stored in the db and provided for admin debugging purposes.
        /// </param>
        private async Task CancelOnboarding(Track workItem, string userError, string adminError)
        {
            try
            {
                // Make sure there aren't any processes still running that might be tying up the files
                // we're about to delete
                foreach (Process process in _launchedProcesses.Where(p => !p.HasExited))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch (Exception e)
                    {
                        Trace.TraceWarning("Failed to kill process {0}: {1}", process.Id, e.Message);
                    }
                }

                // Fetch the qualities from the DB to make this faster
                List<Quality> qualities = await TrackDbManager.Instance.GetAllQualitiesAsync();

                // Delete the original file and any generated qualities from azure storage
                List<Task> cleanupTasks = new List<Task>();

                string originalAzurePath = AzureStorageManager.CombineAzurePath(OnboardingDirectory,
                    workItem.Id.ToString());
                cleanupTasks.Add(AzureStorageManager.Instance.DeleteBlobAsync(TrackStorageContainer, originalAzurePath));
                cleanupTasks.AddRange(qualities.Select(q =>
                {
                    string azureFilename = GetQualityFileName(workItem, q);
                    string azureFilePath = AzureStorageManager.CombineAzurePath(q.Directory, azureFilename);
                    return AzureStorageManager.Instance.DeleteBlobAsync(TrackStorageContainer, azureFilePath);
                }));

                // Delete the original file and any generated qualities from local storage
                LocalStorageManager.Instance.DeleteFile(IO.Path.Combine(OnboardingDirectory, workItem.Id.ToString()));
                foreach (Quality quality in qualities)
                {
                    string localFilename = GetQualityFileName(workItem, quality);
                    string relativeFilePath = IO.Path.Combine(OnboardingDirectory, localFilename);
                    LocalStorageManager.Instance.DeleteFile(relativeFilePath);
                }

                // Delete the qualities and metadata from the database
                cleanupTasks.Add(TrackDbManager.Instance.DeleteAllMetadataAsync(workItem.Id));

                // Remove the art from the track
                // TODO: Fix this
                cleanupTasks.Add(TrackDbManager.Instance.SetTrackArtAsync(workItem, null, false));

                // Put the track into an error state
                await Task.WhenAll(cleanupTasks);

            }
            catch (Exception e)
            {
                Trace.TraceWarning("Failed to properly clean up after onboarding failure of {0}: {1}",
                    workItem.InternalId, e.Message);
            }
            finally
            {
                TrackDbManager.Instance.SetTrackErrorStateAsync(workItem, userError, adminError).Wait();
            }
        }

        #region Private Helper Methods

        /// <summary>
        /// Copies the file from azure to the local, temporary storage
        /// </summary>
        /// <param name="tempPath">Path for the file in local storage</param>
        /// <param name="trackGuid">The guid of the track to pull from azure</param>
        private static async Task CopyFileToLocalStorage(string tempPath, Guid trackGuid)
        {
            // Get the stream from Azure
            string azurePath = AzureStorageManager.CombineAzurePath(OnboardingDirectory, trackGuid.ToString());
            using (IO.FileStream localStream = LocalStorageManager.CreateFile(tempPath))
            {
                await AzureStorageManager.Instance.DownloadBlobAsync(TrackStorageContainer, azurePath, localStream);
            }
        }

        /// <summary>
        /// Generates the filename for the given quality. This is to ensure we use the same names
        /// everywhere in the process.
        /// </summary>
        /// <param name="track">The track to generate the filename for</param>
        /// <param name="quality">The quality of the track to generate a filename for</param>
        /// <returns>The filename of the specified quality of the track</returns>
        private static string GetQualityFileName(Track track, Quality quality)
        {
            return String.Format("{0}.{1}.{2}", track.Id, quality.Directory, quality.Extension);
        }

        /// <summary>
        /// Launches an process that will complete asynchronously. The process will be stored in
        /// <see cref="_launchedProcesses"/> in order to allow for killing of processes in the
        /// event of a failure.
        /// </summary>
        /// <param name="exeName">The name of the command line program to launch</param>
        /// <param name="arguments">The arguments to pass to the process</param>
        /// <returns>An awaitable task for the process to complete running</returns>
        private Task LaunchProcessAsync(string exeName, string arguments)
        {
            // Create a task completion source
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = exeName,
                Arguments = arguments,
                CreateNoWindow = true,
                ErrorDialog = false,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardInput = false,
                RedirectStandardError = true
            };

            // Create the process the we're going to run
            Process process = new Process
            {
                StartInfo = psi,
                EnableRaisingEvents = true
            };
            _launchedProcesses.Add(process);

            // Add an event that marks the task completion source as completed. This will complete
            // the task that is returned to the consumer of this method.
            process.Exited += (sender, args) =>
            {
                tcs.SetResult(true);
                _launchedProcesses.Remove(process);
                process.Dispose();
            };
            // @TODO Make sure the process call succeeded

            // Start up the process
            process.Start();

            return tcs.Task;
        }

        /// <summary>
        /// Moves the file to azure
        /// </summary>
        /// <param name="sourcePath">Path of the file in local storage to move</param>
        /// <param name="destDir">Directory in Azure to store the file</param>
        private static async Task MoveFileToAzure(string sourcePath, string destDir)
        {
            // Construct the target destination
            string destPath = AzureStorageManager.CombineAzurePath(destDir, IO.Path.GetFileName(sourcePath));

            // Start the upload of the file to azure
            await AzureStorageManager.StoreBlobAsync(TrackStorageContainer, sourcePath, destPath);
        }

        #endregion
    }
}
