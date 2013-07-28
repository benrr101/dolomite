using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.ServiceModel.Web;

namespace DolomiteWcfService
{
    public class ServiceEndpoint : IServiceEndpoint
    {

        #region Properties

        /// <summary>
        /// Instance of the Azure Storage Manager
        /// </summary>
        private AzureStorageManager StorageManager { get; set; }

        #endregion

        public ServiceEndpoint()
        {
            // Initialize the storage manager
            StorageManager = AzureStorageManager.Instance;
        }

        /// <summary>
        /// Uploads a track from the RESTful API to Azure blob storage. This
        /// also pulls out the track's metadata and loads the info into the db.
        /// Lastly, this kicks off a thread to run the converters to create the
        /// different bitrates of the track.
        /// </summary>
        /// <param name="file">Stream of the file that is uploaded</param>
        /// <returns>Http response. Follows the standard.</returns>
        public void UploadTrack(Stream file)
        {
            // Upload the track to the temporaty storage
            Guid fileName = Guid.NewGuid();
            StorageManager.StoreTrack(fileName.ToString(), file);

            // TODO: Grab track's metadata

            // TODO: Store track's metadata to the database
        }

        public Stream DownloadTrack(string guid)
        {
            Guid trackGuid;
            if (!Guid.TryParse(guid, out trackGuid))
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                return null;
            }

            Stream output = StorageManager.GetTrack(guid);
            if (output == null)
            {
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.NotFound;
                return null;
            }
            return output;
        }

        //TODO: REMOVE THIS TEST METHOD
        public List<string> GetTracks()
        {
            return StorageManager.GetAllTracks();
        }
    }
}
