using System;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using DolomiteModel;

namespace DolomiteManagement
{
    public class LocalStorageManager
    {
        /// <summary>
        /// The path to the local resources folder
        /// </summary>
        public static string LocalResourcePath { get; set; }

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
        /// <exception cref="InvalidOperationException">
        /// Thrown if the local storage path was not set before using
        /// </exception>
        /// <param name="filename">The name of the file</param>
        /// <returns>String path to the file</returns>
        public string GetPath(string filename)
        {
            // Make sure that we have a local path before loading it
            if(String.IsNullOrWhiteSpace(LocalResourcePath))
                throw new InvalidOperationException("Local storage path has not been initialized.");

            // Build the path of the file
            return Path.Combine(LocalResourcePath, filename);
        }

        #region Storage Methods

        /// <summary>
        /// Initializes a given storage directory by creating it if it doesn't already exist.
        /// </summary>
        /// <param name="directory">The directory to initialize</param>
        public void InitializeStorageDirectory(string directory)
        {
            // Create the folder if it doesn't already exist
            string fullPath = GetPath(directory);
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }
        }

        /// <summary>
        /// Stores the contents of the stream to the file with the given filename.
        /// </summary>
        /// <param name="stream">The stream to store</param>
        /// <param name="filename">
        /// The filename to store the file to (usually the GUID of the track)
        /// </param>
        public async Task StoreStreamAsync(Stream stream, string filename)
        {
            // Copy the stream to the local temporary storage
            using (FileStream newFile = File.Create(GetPath(filename)))
            {
                await stream.CopyToAsync(newFile);
            }
        }

        /// <summary>
        /// Creates a new file with the given path and returns the stream for accessing the file.
        /// </summary>
        /// <param name="filename">Path to the file to create</param>
        /// <returns>A stream object that points to the created file</returns>
        public FileStream CreateFile(string filename)
        {
            return File.Create(GetPath(filename));
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
        public FileStream RetrieveReadableFile(string filename)
        {
            // Return a stream of the file
            return File.OpenRead(GetPath(filename));
        }

        #endregion

        #region Deletion Methods

        /// <summary>
        /// Deletes the file with the given name from local storage
        /// </summary>
        /// <param name="filename">
        /// Path of the file to delete, relative to the root of onboarding storage
        /// </param>
        public void DeleteFile(string filename)
        {
            Trace.TraceInformation("Attempting to delete {0} from local storage", filename);

            // Delete the file
            string filePath = GetPath(filename);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                Trace.TraceInformation("Successfully deleted {0}", filePath);
            }
            else
            {
                Trace.TraceInformation("File does not exist, ignoring {0}", filePath);
            }
        }

        #endregion

        #region Hashing Methods

        /// <summary>
        /// Calculates the RIPEMD160 hash of the given stream
        /// </summary>
        /// <param name="stream">The stream to calculate the hash of</param>
        /// <param name="owner">The owner of the track</param>
        /// <returns>The hash of the file</returns>
        /// TODO: Don't check for track stuff in here! Or, create a separate method for calculating art hashes and determining if they are different
        [Obsolete("Use CalculateMd5Hash, check existing hash in your own logic")]
        public string CalculateHash(Stream stream, string owner)
        {
            stream.Position = 0;

            // Calculate the hash and save it
            RIPEMD160 hashCalculator = RIPEMD160.Create();
            byte[] hashBytes = hashCalculator.ComputeHash(stream);
            string hashString = BitConverter.ToString(hashBytes);
            hashString = hashString.Replace("-", String.Empty);

            stream.Position = 0;

            // Is the track a duplicate?
            if (TrackDbManager.Instance.GetTrackByHash(hashString, owner) != null)
            {
                // The track is a duplicate!
                throw new DuplicateNameException("Track is a duplicate as determined by hash comparison");
            }

            return hashString;
        }
        
        /// <summary>
        /// Calculates an MD5 hash of the file specified by <paramref name="filename"/>
        /// </summary>
        /// <param name="filename">The file to hash</param>
        /// <returns>The MD5 hash of the file</returns>
        [Pure]
        public string CalculateMd5Hash(string filename)
        {
            using (FileStream file = File.OpenRead(GetPath(filename)))
            {
                return CalculateMd5Hash(file);
            }
        }

        /// <summary>
        /// Calculates an MD5 hash of the contents specified by <paramref name="stream"/>
        /// </summary>
        /// <param name="stream">The file to hash</param>
        /// <returns>The MD5 hash of the file</returns>
        [Pure]
        public static string CalculateMd5Hash(Stream stream)
        {
            MD5 hasher = MD5.Create();
            byte[] hashBytes = hasher.ComputeHash(stream);
            stream.Position = 0;
            return BitConverter.ToString(hashBytes).Replace("-", String.Empty);
        }

        /// <summary>
        /// Calculates an MD5 hash of the file specified by <paramref name="filename"/>
        /// </summary>
        /// <param name="filename">The file to hash</param>
        /// <returns>The MD5 hash of the file</returns>
        [Pure]
        public async Task<string> CalculateMd5HashAsync(string filename)
        {
            return await Task.Run(() => CalculateMd5Hash(filename));
        }

        /// <summary>
        /// Calculates an MD5 hash of the contents specified by <paramref name="stream"/>
        /// </summary>
        /// <param name="stream">The file to hash</param>
        /// <returns>The MD5 hash of the file</returns>
        [Pure]
        public async Task<string> CalculateMd5HashAsync(Stream stream)
        {
            return await Task.Run(() => CalculateMd5Hash(stream));
        }

        #endregion

    }
}
