//------------------------------------------------------------------------------
// <auto-generated>
//    This code was generated from a template.
//
//    Manual changes to this file may cause unexpected behavior in your application.
//    Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace DolomiteModel.EntityFramework
{
    using System;
    using System.Collections.Generic;
    
    public partial class Track
    {
        public Track()
        {
            this.AvailableQualities = new HashSet<AvailableQuality>();
            this.Metadatas = new HashSet<Metadata>();
            this.PlaylistTracks = new HashSet<PlaylistTrack>();
        }
    
        public System.Guid Id { get; set; }
        public int Owner { get; set; }
        public string Hash { get; set; }
        public Nullable<int> Album { get; set; }
        public Nullable<System.Guid> Art { get; set; }
        public Nullable<int> OriginalBitrate { get; set; }
        public Nullable<int> OriginalSampling { get; set; }
        public string OriginalMimetype { get; set; }
        public string OriginalExtension { get; set; }
        public bool HasBeenOnboarded { get; set; }
        public bool Locked { get; set; }
        public bool TrackInTempStorage { get; set; }
    
        public virtual Album Album1 { get; set; }
        public virtual Art Art1 { get; set; }
        public virtual ICollection<AvailableQuality> AvailableQualities { get; set; }
        public virtual ICollection<Metadata> Metadatas { get; set; }
        public virtual ICollection<PlaylistTrack> PlaylistTracks { get; set; }
        public virtual User User { get; set; }
    }
}
