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

            LocalStorageManager.Instance.InitializeStorageDirectory(OnboardingDirectory);

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
                    
                    // Step 1: Grab the track from Azure
                    try
                    {
                        CopyFileToLocalStorage(trackFilePath, track.Id).Wait();
                    }
                    catch (Exception)
                    {
                        Trace.TraceError("{1} failed to retrieve uploaded track {0} from Azure. Removing record...", workItemId, GetHashCode());
                        CancelOnboarding(track);
                        continue;
                    }

                    // Step 2: Grab the metadata for the track
                    TrackMetadata metadata = null;
                    try
                    {
                        using (IO.FileStream fs = LocalStorageManager.Instance.RetrieveReadableFile(trackFilePath))
                        {
                            metadata = new TrackMetadata(fs, track.OriginalMimetype);
                        }
                        StoreMetadata(track, metadata).Wait();
                        StoreOriginalQuality(track, metadata).Wait();
                        StoreAlbumArt(track, metadata).Wait();
                    }
                    catch (UnsupportedFormatException)
                    {
                        // Failed to determine type. We don't want this file.
                        Trace.TraceError("{1} failed to determine the type of track {0}. Removing record...", workItemId, GetHashCode());
                        CancelOnboarding(track);
                        continue;
                    }
                    catch (CorruptFileException)
                    {
                        // File is corrupt for whatever reason. We don't want this file.
                        Trace.TraceError("{1} found corrupt file for track {0}. Removing record...", workItemId, GetHashCode());
                        CancelOnboarding(track);
                        continue;
                    }

                    // Create all the qualities for the track
                    try
                    {
                        var qualities = DetermineQualitiesToGenerate(track, metadata).Result;
                        foreach (var quality in qualities)
                        {
                            GenerateQuality(track, quality).Wait();
                        } 
                    }
                    catch (Exception)
                    {
                        Trace.TraceError("{1} failed to create qualities for this track {0}. Removing record...", workItemId, GetHashCode());
                        CancelOnboarding(track);
                        continue;
                    }

                    // Onboarding complete! Delete the temp copy! Release the lock!
                    LocalStorageManager.Instance.DeleteFile(trackFilePath);
                    AzureStorageManager.DeleteBlob(TrackStorageContainer, AzureStorageManager.CombineAzurePath(OnboardingDirectory, track.Id.ToString()));
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

        private async Task StoreMetadata(Track track, TrackMetadata metadata)
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

        private async Task StoreOriginalQuality(Track track, TrackMetadata metadata)
        {
            await TrackDbManager.Instance.SetAudioQualityInfoAsync(track.InternalId, metadata.BitrateKbps, metadata.Mimetype,
                metadata.Extension);
        }

        private async Task StoreAlbumArt(Track track, TrackMetadata metadata)
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

        private async Task<List<Quality>> DetermineQualitiesToGenerate(Track track, TrackMetadata metadata)
        {
            // Step 1) Fetch all the supported qualities from the DB
            List<Quality> allQualities = await TrackDbManager.Instance.GetAllQualitiesAsync();

            // Step 2) Determine which qualities to use by picking all qualities with bitrates less
            // than or equal to the original (+/- 5kbps to handle lousy sources)
            var maxQuality = allQualities.Where(q => Math.Abs(q.Bitrate - metadata.BitrateKbps) <= 5);
            var lesserQualities = allQualities.Where(q => q.Bitrate < metadata.BitrateKbps);

            return lesserQualities.Union(maxQuality).ToList();
        }

        private async Task GenerateQuality(Track track, Quality quality)
        {
            // Determine the paths of the input and output
            string inputRelativeToLocalStoragePath = IO.Path.Combine(OnboardingDirectory, track.Id.ToString());
            string inputFilePath = LocalStorageManager.Instance.GetPath(inputRelativeToLocalStoragePath);
            string outputFilename = String.Format("{0}.{1}.{2}", track.Id, quality.Directory, quality.Extension);
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
            await LaunchProcessAsync(psi);

            // @TODO Make sure the process call succeeded

            // Upload the file to Azure and delete it when we're done
            await MoveFileToAzure(outputLocalFilePath, quality.Directory);
            LocalStorageManager.Instance.DeleteFile(outputLocalRelative);

            // Store the quality record to the database
            DatabaseManager.AddAvailableQualityRecord(track, quality);
        }

        /// <summary>
        /// Moves the file to azure asynchronously
        /// </summary>
        /// <param name="tempPath">Path of the file in local storage to move</param>
        /// <param name="directory">Directory in Azure to store the file</param>
        /// <param name="trackGuid">GUID of the track that is being stored</param>
        /// <param name="deleteOnComplete">Whether or not to delete the original file when the move has completed</param>
        private async Task MoveFileToAzure(string sourcePath, string destDir)
        {
            // Construct the target destination
            string destPath = AzureStorageManager.CombineAzurePath(destDir, IO.Path.GetFileName(sourcePath));

            // Start the upload of the file to azure
            await AzureStorageManager.StoreBlobAsync(TrackStorageContainer, sourcePath, destPath);
        }

        private Task LaunchProcessAsync(ProcessStartInfo psi)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            
            Process process = new Process
            {
                StartInfo = psi,
                EnableRaisingEvents = true
            };

            process.Exited += (sender, args) =>
            {
                tcs.SetResult(true);
                process.Dispose();
            };
            // TODO: capture the output

            process.Start();

            return tcs.Task;
        }

        #endregion

        /// <summary>
        /// Cancels the onboarding process by deleting all created files and
        /// removing the record of the track.
        /// </summary>
        /// <param name="workItem">The track that was being onboarded</param>
        private void CancelOnboarding(Track workItem)
        {
            // Delete the original file from the local storage
            DeleteFileWithWait(workItem.Id.ToString());
            AzureStorageManager.DeleteBlob(TrackStorageContainer, String.Format("original/{0}", workItem.Id));

            // Delete the original file from azure
            AzureStorageManager.DeleteBlob(TrackStorageContainer, OnboardingDirectory + '/' + workItem.Id);

            // Iterate over the qualities and delete them from local storage and azure
            foreach (Quality quality in DatabaseManager.GetAllQualities())
            {
                // Delete the local storage instance
                string fileName = String.Format("{0}.{1}.{2}", workItem.Id, quality.Bitrate, quality.Extension);
                DeleteFileWithWait(fileName);

                // Delete the file from Azure
                string azurePath = String.Format("{0}/{1}", quality.Directory, workItem.Id);
                AzureStorageManager.DeleteBlob(TrackStorageContainer, azurePath);
            }

            // Delete the track from the database
            DatabaseManager.DeleteTrack(workItem.InternalId);
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
