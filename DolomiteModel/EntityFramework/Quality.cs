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
    
    public partial class Quality
    {
        public Quality()
        {
            this.AvailableQualities = new HashSet<AvailableQuality>();
        }
    
        public int Id { get; set; }
        public string Name { get; set; }
        public string Codec { get; set; }
        public Nullable<int> Bitrate { get; set; }
        public string Extension { get; set; }
        public string Mimetype { get; set; }
        public string Directory { get; set; }
    
        public virtual ICollection<AvailableQuality> AvailableQualities { get; set; }
    }
}
