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
    
    public partial class MetadataField
    {
        public MetadataField()
        {
            this.AutoplaylistRules = new HashSet<AutoplaylistRule>();
            this.Metadatas = new HashSet<Metadata>();
        }
    
        public int Id { get; set; }
        public string TagName { get; set; }
        public string DisplayName { get; set; }
        public string Type { get; set; }
    
        public virtual ICollection<AutoplaylistRule> AutoplaylistRules { get; set; }
        public virtual ICollection<Metadata> Metadatas { get; set; }
    }
}
