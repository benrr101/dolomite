using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DolomiteManagement;

namespace DolomiteBackgroundProcessing
{
    class ArtWriting
    {
        #region Properties

        public const string LocalTrackStorageDirectory = "artwork";

        public const string WorkCheckIntervalKey = "Art_WorkCheckInterval";

        /// <summary>
        /// Amount of time to sleep in between checks for work
        /// </summary>
        public static TimeSpan WorkCheckInterval { private get; set; }

        /// <summary>
        /// The container in Azure storage that's holding the tracks
        /// </summary>
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
            
            LocalStorageManager.Instance.InitializeStorageDirectory(LocalTrackStorageDirectory);

        }

    }
}
