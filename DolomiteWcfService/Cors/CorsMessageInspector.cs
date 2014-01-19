using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;

namespace DolomiteWcfService.Cors
{

    /// <source>
    /// http://enable-cors.org/server_wcf.html
    /// </source>
    public class CorsMessageInspector : IDispatchMessageInspector
    {
        readonly Dictionary<string, string> _requiredHeaders;
        public CorsMessageInspector(Dictionary<string, string> headers)
        {
            _requiredHeaders = headers ?? new Dictionary<string, string>();
        }

        public object AfterReceiveRequest(ref Message request, IClientChannel channel, InstanceContext instanceContext)
        {
            return null;
        }

        public void BeforeSendReply(ref Message reply, object correlationState)
        {
            var httpHeader = reply.Properties["httpResponse"] as HttpResponseMessageProperty;
            foreach (var item in _requiredHeaders)
            {
                httpHeader.Headers.Add(item.Key, item.Value);
            }
        }
    }
}
