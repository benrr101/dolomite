using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        private void CalculateHash(Guid trackGuid)
        {
            // Grab an instance of the track
            Track track = TrackManager.GetTrack(trackGuid.ToString());
            
            // Calculate the hash and save it
            RIPEMD160 hashCalculator = RIPEMD160Managed.Create();
            hashCalculator.ComputeHash(track.FileStream);
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
