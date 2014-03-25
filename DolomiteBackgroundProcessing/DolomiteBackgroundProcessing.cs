using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using DolomiteManagement;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace DolomiteBackgroundProcessing
{
    public class DolomiteBackgroundProcessing : RoleEntryPoint
    {
        #region Constants

        private const string TrackContainerKey = "TrackStorageContainer";

        #endregion

        #region Member Variables

        public static List<Thread> OnboardingThreads;

        public static List<Thread> MetadataThreads;

        #endregion

        /// <summary>
        /// Executed after the on start method executes successfully. This
        /// will provide an infinite loop to keep the role alive.
        /// </summary>
        public override void Run()
        {
            // This is a sample worker implementation. Replace with your logic.
            Trace.TraceInformation("Dolomite background processing startup completed. Beginning infinite loop.");

            while (true)
            {
                Thread.Sleep(TimeSpan.FromMinutes(10));
                Trace.TraceInformation("Infinite loop 10 minute checkin.");
            }
            // ReSharper disable once FunctionNeverReturns
        }

        /// <summary>
        /// Executed when the worker role is launched. This retrieves the
        /// configuration information and initializes the managers. Then the
        /// background threads are started.
        /// </summary>
        /// <returns>True on successful service initialization. False otherwise.</returns>
        public override bool OnStart()
        {
            // Initialize the managers
            try
            {
                InitializeTrackManager();
            }
            catch (Exception e)
            {
                Trace.TraceError("Failed to initialize managers: {0}", e.Message);
                Trace.TraceError("Giving up on starting service.");
                return false;
            }

            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 12;

            // Start up the onboarding thread
            try
            {
                Trace.TraceInformation("Starting onboarding threads...");
                StartOnboardingThreads(1);
                Trace.TraceInformation("Onboarding threads started");

                Trace.TraceInformation("Starting metadata threads...");
                StartMetadataThreads(1);
                Trace.TraceInformation("Metadata threads started");
            }
            catch (Exception e)
            {
                Trace.TraceError("Failed to start onboarding thread: {0}", e.Message);
                Trace.TraceError("Giving up on starting service");
                return false;
            }

            return base.OnStart();
        }

        /// <summary>
        /// Used for starting up the onboarding threads.
        /// </summary>
        /// <param name="threads">The number of threads to start</param>
        private static void StartOnboardingThreads(int threads)
        {
            // Initialize the onboarding thread list
            OnboardingThreads = new List<Thread>();

            // Grab the configuration information
            try
            {
                // Get the track storage container
                var trackContainer = RoleEnvironment.GetConfigurationSettingValue(TrackContainerKey);
                TrackManager.TrackStorageContainer = trackContainer;
            }
            catch (Exception e)
            {
                throw new InvalidDataException("Failed to initialize the onboarding thread.", e);
            }

            // Create the onboarding threads
            for (int i = 0; i < threads; ++i)
            {
                TrackOnboarding newOnboarder = new TrackOnboarding();
                Thread newThread = new Thread(newOnboarder.Run);
                newThread.Start();

                OnboardingThreads.Add(newThread);
            }
        }

        /// <summary>
        /// Used for starting up the metadata processing threads.
        /// </summary>
        /// <param name="threads">The number of threads to start</param>
        private static void StartMetadataThreads(int threads)
        {
            // Initialize the metadata thread list
            MetadataThreads = new List<Thread>();

            // Grab the configuration information
            try
            {
                // Get the track storage container
                var trackContainer = RoleEnvironment.GetConfigurationSettingValue(TrackContainerKey);
                TrackManager.TrackStorageContainer = trackContainer;
            }
            catch (Exception e)
            {
                throw new InvalidDataException("Failed to initialize the Track Manager.", e);
            }

            // Create the metadata threads
            for (int i = 0; i < threads; ++i)
            {
                MetadataWriting newWriting = new MetadataWriting();
                Thread newThread = new Thread(newWriting.Run);
                newThread.Start();

                MetadataThreads.Add(newThread);
            }
        }

        /// <summary>
        /// Fetches the necessary configuration information from the Azure
        /// config and initializes the track manager with it.
        /// </summary>
        private static void InitializeTrackManager()
        {
            try
            {
                // Get the track storage container
                var trackContainer = RoleEnvironment.GetConfigurationSettingValue(TrackContainerKey);
                TrackManager.TrackStorageContainer = trackContainer;
            }
            catch (Exception e)
            {
                throw new InvalidDataException("Failed to initialize the Track Manager.", e);
            }
        }
    }
}
