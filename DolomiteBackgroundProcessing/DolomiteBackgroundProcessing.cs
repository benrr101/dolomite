using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using DolomiteManagement;
using DolomiteModel;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace DolomiteBackgroundProcessing
{
    public class DolomiteBackgroundProcessing : RoleEntryPoint
    {
        #region Constants

        private const string TrackContainerKey = "TrackStorageContainer";
        private const string LocalStorageResourceKey = "OnboardingStorage";
        private const string SqlConnectionStringKey = "SqlConnectionString";

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
                InitializeDatabaseManagers();

                // Grab the connection string for Azure storage
                string azureConnectionString = CloudConfigurationManager.GetSetting(AzureStorageManager.ConnectionStringKey);
                AzureStorageManager.StorageConnectionString = azureConnectionString;

                // Grab the local storage path
                LocalResource localStorage = RoleEnvironment.GetLocalResource(LocalStorageResourceKey);
                LocalStorageManager.LocalResourcePath = localStorage.RootPath;
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
                StartOnboardingThreads(0);
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
                // Get all the configuration values for the onboarding thread
                int sleepSeconds = GetConfigurationValue<int>(TrackOnboarding.SleepSecondsKey);
                TrackOnboarding.SleepSeconds = sleepSeconds;

                // Get the track storage container
                string trackContainer = GetConfigurationValue<string>(TrackContainerKey);
                TrackOnboarding.TrackStorageContainer = trackContainer;
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
                MetadataWriting.TrackStorageContainer = trackContainer;
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
                var trackContainer = GetConfigurationValue<string>(TrackContainerKey);

                // Set it for the onboarding threads
                TrackManager.TrackStorageContainer = trackContainer;
            }
            catch (Exception e)
            {
                throw new InvalidDataException("Failed to initialize the Track Manager.", e);
            }
        }

        /// <summary>
        /// Fetches the SQL configuration information from the role config
        /// </summary>
        private static void InitializeDatabaseManagers()
        {
            try
            {
                // Get the SQL connection string
                var connectionString = GetConfigurationValue<string>(SqlConnectionStringKey);
                
                // Set it on all the database managers
                ArtDbManager.SqlConnectionString = connectionString;
                MetadataDbManager.SqlConnectionString = connectionString;
                WorkDbManager.SqlConnectionString = connectionString;
            }
            catch (Exception e)
            {
                throw new InvalidDataException("Failed to initialize the database managers.", e);
            }
        }

        /// <summary>
        /// Returns the configuration value for the role.
        /// </summary>
        /// <typeparam name="T">Type of the value</typeparam>
        /// <param name="configurationKey">The key to use to look up the config value</param>
        /// <returns>The converted configuration value</returns>
        private static T GetConfigurationValue<T>(string configurationKey) where T : IConvertible
        {
            string configValue = RoleEnvironment.GetConfigurationSettingValue(configurationKey);
            return (T) Convert.ChangeType(configValue, typeof (T));
        }
    }
}
