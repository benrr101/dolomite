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
        private const string OriginHeader = "Access-Control-Allow-Origin";

        readonly Dictionary<string, string> _requiredHeaders;
        public CorsMessageInspector(Dictionary<string, string> headers)
        {
            _requiredHeaders = headers ?? new Dictionary<string, string>();
        }

        public object AfterReceiveRequest(ref Message request, IClientChannel channel, InstanceContext instanceContext)
        {
            // Grab the HTTP headers from the message. Don't do anything if they don't exist.
            var httpHeader = request.Properties["httpRequest"] as HttpRequestMessageProperty;
            if (httpHeader == null)
                return null;

            // Set the HTTP origin as-defined or * if not defined.
            return string.IsNullOrWhiteSpace(httpHeader.Headers["Origin"]) ? "*" : httpHeader.Headers["Origin"];;
        }

        public void BeforeSendReply(ref Message reply, object correlationState)
        {
            // Add all the headers to the message
            var httpHeader = reply.Properties["httpResponse"] as HttpResponseMessageProperty;
            foreach (var item in _requiredHeaders)
            {
                httpHeader.Headers.Add(item.Key, item.Value);
            }

            // Add the origin header to the http headers
            httpHeader.Headers.Add(OriginHeader, (string)correlationState);
        }
    }
}
