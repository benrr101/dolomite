using System.Linq;
using DolomiteModel.EntityFramework;
using Pub = DolomiteModel.PublicRepresentations;

namespace DolomiteModel
{
    public class WorkDbManager
    {
        #region Singleton Instance Code

        /// <summary>
        /// The connection string to the database
        /// </summary>
        public static string SqlConnectionString { get; set; }

        private static WorkDbManager _instance;

        /// <summary>
        /// Singleton instance of the work database manager
        /// </summary>
        public static WorkDbManager Instance
        {
            get { return _instance ?? (_instance = new WorkDbManager()); }
        }

        /// <summary>
        /// Singleton constructor for the work database manager
        /// </summary>
        private WorkDbManager() { }

        #endregion

        #region Get and Lock Methods

        /// <summary>
        /// Use the stored procedure on the database to grab and lock an art writing work item.
        /// </summary>
        /// <returns>The guid of the track to process, or null if none exists</returns>
        /// TODO: Return a Track object (or just wait until the dataflow/message queue thing is ready)
        public Pub.Track GetArtWorkItem()
        {
            using (var context = new Entities(SqlConnectionString))
            {
                // Call the stored procedure and hopefully it'll give us a work item
                long? workItemId = context.GetAndLockTopArtItem().FirstOrDefault();
                return workItemId.HasValue
                    ? TrackDbManager.Instance.GetTrack(workItemId.Value)
                    : null;
            }
        }

        /// <summary>
        /// Use the stored procedure on the database to grab and lock an
        /// metadata writing work item.
        /// </summary>
        /// <returns>The internal id of the track to process, or null if none exists</returns>
        /// TODO: Use the message queue
        public long? GetMetadataWorkItem()
        {
            using (var context = new Entities(SqlConnectionString))
            {
                // Call the stored proc and get a work item
                return context.GetAndLockTopMetadataItem().FirstOrDefault();
            }
        }

        /// <summary>
        /// Use the stored procedure on the database to grab and lock an
        /// onboarding work item.
        /// </summary>
        /// <returns>The guid of the track to process, or null if none exists</returns>
        /// TODO: use message queues when ready
        public long? GetOnboardingWorkItem()
        {
            using (var context = new Entities(SqlConnectionString))
            {
                // Call the stored procedure and hopefully it'll give us a work item
                return context.GetAndLockTopOnboardingItem().FirstOrDefault();
            }
        }

        #endregion

        #region Release Lock Methods

        /// <summary>
        /// Releases the lock on the work item and completes the art change
        /// process via a stored procedure
        /// </summary>
        /// <param name="workItem">The work item to release</param>
        public void ReleaseAndCompleteArtItem(long workItem)
        {
            using (var context = new Entities(SqlConnectionString))
            {
                // Call the stored procedure to complete the track onboarding
                context.ReleaseAndCompleteArtChange(workItem);
            }
        }

        /// <summary>
        /// Calls the sproc to unlock the given track, and remove any flag
        /// on metadata for the track to show that the metadata needs to be
        /// written out to file.
        /// </summary>
        /// <param name="workItem">The track to release</param>
        public void ReleaseAndCompleteMetadataItem(long workItem)
        {
            using (var context = new Entities(SqlConnectionString))
            {
                context.ReleaseAndCompleteMetadataUpdate(workItem);
            }
        }

        /// <summary>
        /// Releases the lock on the work item and completes the onboarding
        /// process via a stored procedure
        /// </summary>
        /// <param name="workItem">The work item to release</param>
        public void ReleaseAndCompleteOnboardingItem(long workItem)
        {
            using (var context = new Entities(SqlConnectionString))
            {
                // Call the stored procedure to complete the track onboarding
                context.ReleaseAndCompleteOnboardingItem(workItem);
            }
        }

        #endregion
    }
}
