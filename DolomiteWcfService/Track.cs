using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

namespace DolomiteWcfService
{
    [DataContract]
    public class Track
    {

        #region Properties

        /// <summary>
        /// The unique identifier for the track
        /// </summary>
        [DataMember] public Guid Id;

        /// <summary>
        /// The metadata about the track
        /// </summary>
        [DataMember]
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
                // Reset the stream to be beginning
                if (value != null)
                {
                    value.Position = 0;
                }
                _fileStream = value;
            }
        }

        #endregion

    }
}
