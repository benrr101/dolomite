using System.Diagnostics;
using System.IO;

namespace DolomiteWcfService
{
    public class ServiceEndpoint : IServiceEndpoint
    {
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
            byte[] buffer = new byte[10000];
            int bytesRead, totalBytesRead = 0;
            do
            {
                bytesRead = file.Read(buffer, 0, buffer.Length);
                totalBytesRead += bytesRead;
            } while (bytesRead > 0);
            Trace.TraceInformation("Service: Received file with {0} bytes", totalBytesRead);

            // TODO: Write the file out to azure storage

            // TODO: Grab track's metadata

            // TODO: Store track's metadata to the database
        }
    }
}
