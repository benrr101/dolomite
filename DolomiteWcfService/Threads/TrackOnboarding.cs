using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

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
                    string hash = CalculateHash(workItemId.Value);
                    if (DatabaseManager.GetTrackByHash(hash) != null)
                    {
                        // There was a duplicate. Delete it from storage and delete the initial record
                        Trace.TraceInformation("{1} determined track {0} was a duplicate. Removing record...",
                                               workItemId, GetHashCode());
                        DatabaseManager.DeleteTrack(workItemId.Value);
                        LocalStorageManager.DeleteFile(workItemId.Value.ToString());
                    }
                    else
                    {
                        // The file was not a duplicate, so continue processing it
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

        private void DeleteFromTemporaryStorage(Guid trackGuid)
        {
            
        }

        private void StoreMetadata(Guid trackGuid)
        {
            
        }

        #endregion

    }
}
