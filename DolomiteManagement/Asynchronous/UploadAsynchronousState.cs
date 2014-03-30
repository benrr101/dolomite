using System;
using System.IO;

namespace DolomiteManagement.Asynchronous
{
    class UploadAsynchronousState : AzureAsynchronousState
    {
        public string Owner { get; set; }
        public Stream Stream { get; set; }
        public Guid TrackGuid { get; set; }
        public string TrackHash { get; set; }
    }
}
