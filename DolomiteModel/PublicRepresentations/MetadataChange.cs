using System;

namespace DolomiteModel.PublicRepresentations
{
    public class MetadataChange
    {
        public string TagName { get; set; }

        public string Value { get; set; }

        [Obsolete]
        public bool Array { get; set; }     // TODO: Remove references to this

    }
}
