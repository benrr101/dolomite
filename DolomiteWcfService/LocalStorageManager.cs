using System;
using System.IO;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace DolomiteWcfService
{
    class LocalStorageManager
    {
        #region Singleton Instance

        private static LocalStorageManager _instance;

        /// <summary>
        /// Singleton instance of the Azure Storage manager
        /// </summary>
        public static LocalStorageManager Instance
        {
            get { return _instance ?? (_instance = new LocalStorageManager()); }
        }

        /// <summary>
        /// Singleton constructor for the AzureStorageManager
        /// </summary>
        private LocalStorageManager() {}

        #endregion

        #region Storage Methods

        /// <summary>
        /// Stores the contents of the stream to the file with the given file name
        /// <remarks>
        /// This automatically uploads to the onboarding storage. It may be
        /// necessary to expand this to support other local storage later.
        /// </remarks>
        /// </summary>
        /// <param name="stream">The stream to store</param>
        /// <param name="filename">The name of the file</param>
        public void StoreStream(Stream stream, string filename)
        {
            // Create a reference to the local storage
            LocalResource localStorage = RoleEnvironment.GetLocalResource("onboardingStorage");
            
            // Build the true path of the file
            string path = Path.Combine(localStorage.RootPath, filename);

            // Copy the stream to the file
            using (var newFile = File.Create(path))
            {
                stream.CopyTo(newFile);
            }
        }

        #endregion

        #region Retrieval Methods

        public FileStream RetrieveFile(string filename)
        {
            // Create reference to the local storage
            LocalResource localStorage = RoleEnvironment.GetLocalResource("onboardingStorage");

            // Build the path of the file
            string path = Path.Combine(localStorage.RootPath, filename);

            // Return a stream of the file
            return File.OpenRead(path);
        }

        #endregion

    }
}
