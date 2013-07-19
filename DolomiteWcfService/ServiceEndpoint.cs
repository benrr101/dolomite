using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

namespace DolomiteWcfService
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the class name "UploadService" in both code and config file together.
    public class ServiceEndpoint : IServiceEndpoint
    {
        public string DoWork(string stuff)
        {
            return stuff;
        }
    }
}
