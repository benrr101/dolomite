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
    
    public partial class Metadata
    {
        public int Id { get; set; }
        public System.Guid Track { get; set; }
        public int Field { get; set; }
        public string Value { get; set; }
        public bool WriteOut { get; set; }
    
        public virtual MetadataField MetadataField { get; set; }
        public virtual Track Track1 { get; set; }
    }
}
