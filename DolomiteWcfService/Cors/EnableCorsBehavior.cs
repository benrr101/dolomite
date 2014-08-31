using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Configuration;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;

namespace DolomiteWcfService.Cors
{
    /// <source>
    /// http://enable-cors.org/server_wcf.html
    /// </source>
    public class EnableCorsBehavior : BehaviorExtensionElement, IServiceBehavior
    {
        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
        }

        #region BehaviorExtensionElement Overrides

        public override Type BehaviorType
        {
            get { return typeof(EnableCorsBehavior); }
        }

        protected override object CreateBehavior()
        {
            return new EnableCorsBehavior();
        }

        #endregion

        #region IServiceBehavior Implementations

        public void AddBindingParameters(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase, Collection<ServiceEndpoint> endpoints, BindingParameterCollection bindingParameters)
        {
        }

        public void ApplyDispatchBehavior(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {
            var requiredHeaders = new Dictionary<string, string>
            {
                {"Access-Control-Allow-Methods", "POST, GET, PUT, DELETE, OPTIONS"},
                {"Access-Control-Allow-Headers", "Content-Type,Authorization,Content-Disposition"},
                {"Access-Control-Max-Age", "1728000"},
                {"Access-Control-Allow-Credentials", "true"}
            };

            foreach (EndpointDispatcher epDisp in serviceHostBase.ChannelDispatchers.Cast<ChannelDispatcher>().SelectMany(chDisp => chDisp.Endpoints))
            {
                epDisp.DispatchRuntime.MessageInspectors.Add(new CorsMessageInspector(requiredHeaders));
            }
        }

        public void Validate(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {
        }

        #endregion
    }
}
