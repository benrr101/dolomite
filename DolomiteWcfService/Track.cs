using System.Collections.Generic;
using System.IO;

namespace DolomiteWcfService
{
    class Track
    {

        #region Properties

        /// <summary>
        /// The metadata about the track
        /// </summary>
        public Dictionary<string, string> Metadata;

        /// <summary>
        /// A stream that points to the track's blob in Storage.
        /// It is reset to the beginning of the stream to make it readable.
        /// </summary>
        private Stream _fileStream;

        public Stream FileStream
        {
            get { return _fileStream; }
            set
            {
                value.Position = 0;
                _fileStream = value;
            }
        }

        #endregion

    }
}
