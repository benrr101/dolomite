using Microsoft.WindowsAzure.Storage.Blob;

namespace DolomiteManagement.Asynchronous
{
    public class AzureAsynchronousState
    {
        public CloudBlockBlob Blob { get; set; }
    }
}
