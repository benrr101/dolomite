﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TagLib;
using Model = DolomiteModel;

namespace DolomiteWcfService
{
    class TrackManager
    {

        #region Constants

        public const string StorageContainerKey = "trackStorageContainer";

        #endregion

        #region Properties and Member Variables

        private AzureStorageManager AzureStorageManager { get; set; }

        private DatabaseManager DatabaseManager { get; set; }

        private LocalStorageManager LocalStorageManager { get; set; }

        #endregion

        #region Singleton Instance Code

        private static TrackManager _instance;

        /// <summary>
        /// Singleton instance of the track manager
        /// </summary>
        public static TrackManager Instance
        {
            get { return _instance ?? (_instance = new TrackManager()); }
        }

        /// <summary>
        /// Singleton constructor for the Track Manager
        /// </summary>
        private TrackManager() 
        {
            // Check for the existence of the track container and store it
            if (Properties.Settings.Default[StorageContainerKey] == null)
            {
                throw new InvalidDataException("Track storage container key not set in settings.");
            }

            // Get an instance of the azure storage manager
            AzureStorageManager = AzureStorageManager.Instance;

            // Make sure the track container exists
            AzureStorageManager.InitializeContainer(StorageContainerKey);

            // Get an instance of the database manager
            DatabaseManager = DatabaseManager.Instance;

            // Get an instance of the local storage manager
            LocalStorageManager = LocalStorageManager.Instance;
        }

        #endregion

        #region Public Methods

        public void DeleteTrack(Guid trackGuid)
        {
            // Does the track exist?
            Track track = DatabaseManager.GetTrackByGuid(trackGuid);
            if (track == null)
                throw new FileNotFoundException(String.Format("Track with guid {0} does not exist.", trackGuid));

            // TODO: Verify that we can't have inconsistent states for the database

            // Delete the track from Azure
            foreach (Track.Quality quality in track.Qualities)
            {
                string path = quality.Directory + '/' + trackGuid;
                AzureStorageManager.DeleteBlob(StorageContainerKey, path);
            }

            // Delete the record for the track in the database
            DatabaseManager.DeleteTrack(trackGuid);
        }

        /// <summary>
        /// Retreives the info about the track
        /// </summary>
        /// <param name="trackGuid">The guid of the track to retreive</param>
        /// <returns>Object representation of the track</returns>
        public Track GetTrack(Guid trackGuid)
        {
            // Retrieve the track from the database
            return DatabaseManager.GetTrackByGuid(trackGuid);
        }

        /// <summary>
        /// Fetches the stream representing a track with a specific quality
        /// from the database.
        /// </summary>
        /// <param name="trackGuid">The guid of the track to fetch</param>
        /// <param name="quality">The quality of the track to fetch</param>
        /// <returns>A stream for the matching guid and quality</returns>
        public Track.Quality GetTrackStream(Track track, string quality)
        {
            // Find the correct quality
            var qualityObj = track.Qualities.FirstOrDefault(q => q.Directory == quality);
            if (qualityObj == null)
                throw new UnsupportedFormatException(
                    String.Format("The track with guid {0} and quality {1} does not exist", track.Id, quality));

            // Fetch the file from Azure
            qualityObj.FileStream = AzureStorageManager.GetBlob(StorageContainerKey, qualityObj.Directory + "/" + track.Id);
            return qualityObj;
        }

        /// <summary>
        /// Retrieve an index of all the tracks in the database
        /// </summary>
        /// <returns>List of track objects in the database</returns>
        public List<Track> FetchAllTracks()
        {
            // Get the tracks from the database
            var tracks = DatabaseManager.FetchAllTracks();

            // Condense them into a list of tracks
            return tracks.ToList();
        }

        public void ReplaceTrack(Stream stream, Guid guid, out string hash)
        {
            // Step 0: Fetch the track
            Track track = DatabaseManager.GetTrackByGuid(guid);

            // Step 1: Upload the track to temporary storage
            hash = LocalStorageManager.StoreStream(stream, guid.ToString());

            // Step 2: Delete existing blobs from azure
            foreach (Track.Quality quality in track.Qualities)
            {
                string path = quality.Directory + '/' + guid;
                AzureStorageManager.DeleteBlob(StorageContainerKey, path);
            }

            // Step 3: Mark the track as not onboarded
            DatabaseManager.MarkTrackAsNotOnboarderd(guid, hash);
        }

        /// <summary>
        /// Uploads the track to the system. Places the track in temporary
        /// azure storage then kicks off threads to do the rest of the work. We
        /// detect duplicate tracks based on hash here.
        /// </summary>
        /// <param name="stream">Stream of the uploaded track</param>
        /// <param name="guid">Output variable for the guid of the track</param>
        /// <param name="hash">Output variable for the hash of the track</param>
        /// <returns>The guid for identifying the track</returns>
        public void UploadTrack(Stream stream, out Guid guid, out string hash)
        {
            // Step 1: Upload the track to temporary storage in azure
            guid = Guid.NewGuid();
            hash = LocalStorageManager.StoreStream(stream, guid.ToString());

            // Step 2: Create the inital record of the track in the database
            DatabaseManager.CreateInitialTrackRecord(guid, hash);
        }

        

        #endregion
    }
}
