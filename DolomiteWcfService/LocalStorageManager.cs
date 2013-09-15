using System;
using System.Data;
using System.IO;
using System.Security.Cryptography;
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

        /// <summary>
        /// Generates the path to the file in local storage
        /// </summary>
        /// <param name="filename">The name of the file</param>
        /// <returns>String path to the file</returns>
        public string GetPath(string filename)
        {
            // Create reference to the local storage
            LocalResource localStorage = RoleEnvironment.GetLocalResource("onboardingStorage");

            // Build the path of the file
            return Path.Combine(localStorage.RootPath, filename);
        }

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
        public string StoreStream(Stream stream, string filename)
        {
            // Copy the stream to the file
            using (var newFile = File.Create(GetPath(filename)))
            {
                stream.CopyTo(newFile);
            }

            // Calculate the hash of the file
            return CalculateHash(stream);
        }

        #endregion

        #region Retrieval Methods

        /// <summary>
        /// Retrieves the given filename from the onboarding storage 
        /// <remarks>
        /// YOU MUST BE SURE TO CLOSE THE FILE STREAM AFTER USE!!!
        /// </remarks>
        /// </summary>
        /// <param name="filename">The name of the file to retrieve</param>
        /// <returns>The filestream of the file requested</returns>
        public FileStream RetrieveFile(string filename)
        {
            // Return a stream of the file
            return File.OpenRead(GetPath(filename));
        }

        #endregion

        #region Deletion Methods

        /// <summary>
        /// Deletes the file with the fiven name from the onboarding storage
        /// </summary>
        /// <param name="filename">Name of the file to delete</param>
        public void DeleteFile(string filename)
        {
            // Delete the file
            string filePath = GetPath(filename);
            if (File.Exists(filePath))
            {
                File.Delete(GetPath(filename));
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Calculates the RIPEMD160 hash of the given stream
        /// </summary>
        /// <param name="stream">The stream to calculate the hash of</param>
        /// <returns>The hash of the file</returns>
        private string CalculateHash(Stream stream)
        {
            stream.Position = 0;

            // Calculate the hash and save it
            RIPEMD160 hashCalculator = RIPEMD160Managed.Create();
            byte[] hashBytes = hashCalculator.ComputeHash(stream);
            string hashString = BitConverter.ToString(hashBytes);
            hashString = hashString.Replace("-", String.Empty);

            // Is the track a duplicate?
            if (DatabaseManager.Instance.GetTrackByHash(hashString) != null)
            {
                // The track is a duplicate!
                throw new DuplicateNameException("Track is a duplicate as determined by hash comparison");
            }

            return hashString;
        }

        #endregion

    }
}
