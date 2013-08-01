using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using DolomiteWcfService.Exceptions;
using TagLib;

namespace DolomiteWcfService.Threads
{
    class TrackOnboarding
    {

        #region Properties and Constants

        private const int SleepSeconds = 10;

        private static DatabaseManager DatabaseManager { get; set; }

        private static LocalStorageManager LocalStorageManager { get; set; }

        private static TrackManager TrackManager { get; set; }

        #endregion

        #region Start/Stop Logic

        private volatile bool _shouldStop = false;

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
            DatabaseManager = DatabaseManager.Instance;
            LocalStorageManager = LocalStorageManager.Instance;
            TrackManager = TrackManager.Instance;

            // Loop until the stop flag has been flown
            while (!_shouldStop)
            {                
                // Try to get a work item
                Guid? workItemId = DatabaseManager.GetOnboardingWorkItem();
                if (workItemId.HasValue)
                {
                    // We have work to do!
                    Trace.TraceInformation("Work item {0} picked up by {1}", workItemId.Value.ToString(), GetHashCode());

                    // Calculate the hash and look for a duplicate
                    try
                    {
                        string hash = CalculateHash(workItemId.Value);
                    }
                    catch (DuplicateNameException)
                    {
                        // There was a duplicate. Delete it from storage and delete the initial record
                        Trace.TraceError("{1} determined track {0} was a duplicate. Removing record...", workItemId, GetHashCode());
                        DatabaseManager.DeleteTrack(workItemId.Value);
                        LocalStorageManager.DeleteFile(workItemId.Value.ToString());
                        continue;
                    }
                    
                    // The file was not a duplicate, so continue processing it
                    // Grab the metadata for the track
                    try
                    {
                        StoreMetadata(workItemId.Value);
                    }
                    catch (UnsupportedFormatException)
                    {
                        // Failed to determine type. We don't want this file.
                        Trace.TraceError("{1} failed to determine the type of track {0}. Removing record...", workItemId, GetHashCode());
                        DatabaseManager.DeleteTrack(workItemId.Value);
                        LocalStorageManager.DeleteFile(workItemId.Value.ToString());
                        continue;
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
        /// Calculates the RIPEMD160 hash of the track with the given guid and
        /// stores it to the database.
        /// </summary>
        /// <param name="trackGuid">The track to calculate the hash for</param>
        /// <returns>The hah of the file</returns>
        private string CalculateHash(Guid trackGuid)
        {
            // Grab an instance of the track
            try
            {
                Trace.TraceInformation("{0} is calculating hash for {1}", GetHashCode(), trackGuid);
                FileStream track = LocalStorageManager.RetrieveFile(trackGuid.ToString());

                // Calculate the hash and save it
                RIPEMD160 hashCalculator = RIPEMD160Managed.Create();
                byte[] hashBytes = hashCalculator.ComputeHash(track);
                string hashString = BitConverter.ToString(hashBytes);
                hashString = hashString.Replace("-", String.Empty);

                // CLOSE THE STREAM!
                track.Close();

                // Is the track a duplicate?
                if (DatabaseManager.GetTrackByHash(hashString) != null)
                {
                    // The track is a duplicate!
                    throw new DuplicateNameException(String.Format("Track {0} is a duplicate as determined by hash comparison", trackGuid));
                }

                // Store that hash to the database
                DatabaseManager.SetTrackHash(trackGuid, hashString);

                return hashString;
            }
            catch (Exception e)
            {
                Trace.TraceError("Exception from {0} while calculating hash of {1}: {2}", GetHashCode(), trackGuid, e.Message);
                throw;
            }
        }

        private void CreateQualities(Guid trackGuid)
        {
            
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
            string mimetype = MimetypeDetector.GetMimeType(LocalStorageManager.RetrieveFile(trackGuid.ToString()));

            // Retrieve the file from temporary storage
            TagLib.File file = TagLib.File.Create(LocalStorageManager.GetPath(trackGuid.ToString()), mimetype, ReadStyle.Average);
        }

        #endregion

    }
}
