//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace DolomiteModel.EntityFramework
{
    using System;
    using System.Collections.Generic;
    
    public partial class ErrorInfo
    {
        public ErrorInfo()
        {
            this.Tracks = new HashSet<Track>();
        }
    
        public long Id { get; set; }
        public string UserError { get; set; }
        public string AdminError { get; set; }
    
        public virtual ICollection<Track> Tracks { get; set; }
    }
}