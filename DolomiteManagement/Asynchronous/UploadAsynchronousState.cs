using System.IO;

namespace DolomiteManagement.Asynchronous
{
    abstract class UploadAsynchronousState : AzureAsynchronousState
    {
        public string Owner { get; set; }
        public Stream Stream { get; set; }
        public string TrackHash { get; set; }
    }
}
