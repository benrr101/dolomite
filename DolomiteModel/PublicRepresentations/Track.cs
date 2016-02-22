using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using EF = DolomiteModel.EntityFramework;

namespace DolomiteModel.PublicRepresentations
{
    [DataContract]
    public class Track
    {

        #region Internal Track Quality Class

        [DataContract]
        public class Quality
        {
            #region Serializable Properties

            /// <summary>
            /// The bitrate of the track at this quality
            /// </summary>
            [DataMember]
            public string Bitrate
            {
                get { return BitrateKbps + "kbps"; }
            }

            /// <summary>
            /// The file extension of the quality
            /// </summary>
            [DataMember]
            public string Extension { get; set; }

            /// <summary>
            /// The href to the download for the quality
            /// </summary>
            [DataMember]
            public string Href { get; set; }

            /// <summary>
            /// The name of the quality
            /// </summary>
            [DataMember]
            public string Name { get; set; }

            /// <summary>
            /// The mimetype of the track at this quality
            /// </summary>
            [DataMember]
            public string Mimetype { get; set; }

            #endregion

            /// <summary>
            /// Numeric representation of the bitrate in kbps
            /// </summary>
            public int BitrateKbps { get; set; }

            /// <summary>
            /// The directory of the azure container storing the track at this quality
            /// </summary>
            public string Directory { get; set; }

            /// <summary>
            /// The stream representing this quality of the track. Normally set to null.
            /// </summary>
            public Stream FileStream { get; set; }
        }

        #endregion

        #region Properties

        #region Public Properties
        /// <summary>
        /// The href to the track's art file (if it has one -- it will be null
        /// otherwise)
        /// </summary>
        [DataMember]
        public string ArtHref { get; set; }

        /// <summary>
        /// The unique identifier for the track
        /// </summary>
        [DataMember]
        public Guid Id;

        /// <summary>
        /// The metadata about the track, formatted such that TagName => Value
        /// </summary>
        [DataMember]
        public Dictionary<string, string> Metadata;

        #endregion

        #region Internal Properties

        /// <summary>
        /// Whether or not this object has been fully populated with data
        /// </summary>
        public bool FullObject { get; private set; }

        /// <summary>
        /// Whether or not there's an art change pending on the track. Not
        /// intended for use by clients.
        /// </summary>
        public bool ArtChange { get; set; }

        /// <summary>
        /// The ID for the art object for this track. Not intented for use
        /// by clients.
        /// </summary>
        public long? ArtId { get; set; }

        /// <summary>
        /// The username of the owner of the track. This is not a datamember since
        /// it should not be used by clients.
        /// </summary>
        public string Owner { get; set; }

        /// <summary>
        /// The mimetype of the original upload. This is not a datamember since it should not be
        /// used by the client.
        /// </summary>
        public string OriginalMimetype { get; set; }

        /// <summary>
        /// List of qualities that are available for the given track.
        /// </summary>
        [DataMember]
        public List<Quality> Qualities { get; set; }

        /// <summary>
        /// Whether or not the track is ready for public consumption
        /// </summary>
        public bool Ready { get; set; }

        /// <summary>
        /// The internal ID of the track -- the database primary key
        /// </summary>
        public long InternalId { get; set; }

        #endregion

        #endregion

        public Track()
        {
            FullObject = false;
        }

        #region Internal Constructor

        /// <summary>
        /// Creates an public-ready Track object based on the internal version of the track object.
        /// </summary>
        /// <param name="track">The internal track object to build this version from.</param>
        /// <param name="fullFetch">
        /// Whether or not do a full fetch. A full fetch includes metadata, quality, and art info
        /// while a non-full fetch does not include that information.
        /// </param>
        // TODO: Determine if the query for track is god-awful complex and use IQueryable<EF.Track> if it is
        internal Track(EF.Track track, bool fullFetch = true)
        {
            // Store the minimum information
            // TODO: Don't use magic numbers
            FullObject = fullFetch;
            InternalId = track.Id;
            Id = track.GuidId;
            Ready = track.Status == 3;
            ArtChange = track.ArtChange;
            Owner = track.User.Username;
            OriginalMimetype = track.OriginalMimetype;

            // Break out if we don't need to store anything else
            if (!fullFetch || !Ready)
                return;

            // Store the extended data
            Metadata = track.Metadatas.ToDictionary(m => m.MetadataField.TagName, m => m.Value);

            // TODO: This may not work.
            Qualities = track.AvailableQualities
                .Where(q => q.Quality1.Bitrate != null)
                .Select(q => new Quality
                {
                    // ReSharper disable once PossibleInvalidOperationException Impossible because of the .Where
                    BitrateKbps = q.Quality1.Bitrate.Value,
                    Directory = q.Quality1.Directory,
                    Extension = q.Quality1.Extension,
                    Href = String.Format("/tracks/{0}/{1}", q.Quality1.Directory, track.GuidId),
                    Mimetype = q.Quality1.Mimetype,
                    Name = q.Quality1.Name
                })
                .ToList();
            
            // Add the original quality back to the list of qualities
            Quality originalQuality = new Quality
            {
                // ReSharper disable once PossibleInvalidOperationException 
                //   Shouldn't be possible b/c this field is only set if track has been onboarded
                BitrateKbps = track.OriginalBitrate.Value,
                Directory = "original",
                Extension = track.OriginalExtension,
                Href = String.Format("/tracks/original/{0}", track.Id),
                Mimetype = track.OriginalMimetype,
                Name = "Original"
            };
            Qualities.Add(originalQuality);

            // Add the art information only if the art was defined
            if (track.Art != null)
            {
                ArtHref = String.Format("/tracks/art/{0}", track.Art1.GuidId);
                ArtId = track.Art;
            }
        }

        #endregion

    }
}

