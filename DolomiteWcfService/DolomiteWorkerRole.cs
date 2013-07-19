using System;
using System.Diagnostics;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Threading;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace DolomiteWcfService
{
    public class DolomiteWorkerRole : RoleEntryPoint
    {

        #region Member Variables

        /// <summary>
        /// Instance of the service host for the dolomite wcf endpoint
        /// </summary>
        private ServiceHost _serviceHost;

        #endregion

        /// <summary>
        /// Executed after the on start method executes successfully. This
        /// will provide an infinite loop to keep the role alive.
        /// </summary>
        public override void Run()
        {
            Trace.TraceInformation("Dolomite WCF Service startup completed. Beginning infinite loop.");

            // Loop forever to keep the role alive
            while (true)
            {
                Thread.Sleep(TimeSpan.FromMinutes(10));
                Trace.TraceInformation("Infinite loop 10 minute checkin.");
            }
        }

        /// <summary>
        /// Executed when the worker role is launched. This retrieves the
        /// endpoint configuration information and spins up a service endpoint
        /// using that information.
        /// </summary>
        /// <returns>True on successful service initialization. False otherwise.</returns>
        public override bool OnStart()
        {
            Trace.TraceInformation("Starting Dolomite WCF Service...");

            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 12;

            // Grab the base address from the role manager
            IPEndPoint endpoint = RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["DolomiteRest"].IPEndpoint;
            Uri baseAddress = new Uri(String.Format("http://{0}:{1}", endpoint.Address, endpoint.Port));
            
            // Spin up a service end point
            try
            {
                _serviceHost = new ServiceHost(typeof (ServiceEndpoint), baseAddress);

                // Enable the http metadata output
                ServiceMetadataBehavior smb = new ServiceMetadataBehavior
                    {
                        HttpGetEnabled = true,
                        MetadataExporter = {PolicyVersion = PolicyVersion.Policy15}
                    };
                _serviceHost.Description.Behaviors.Add(smb);
                _serviceHost.Open();

                Trace.TraceInformation("Started Dolomite WCF Endpoint on {0}", baseAddress.AbsoluteUri);
            }
            catch (Exception e)
            {
                Trace.TraceError("Failed to open service endpoint: {0}", e.Message);
                Trace.TraceError("Giving up on starting service.");
                return false;
            }
            
            return base.OnStart();
        }

        /// <summary>
        /// Executed when the worker role is brought to a stop, gracefully.
        /// Not executed when the role fails to start. It closes the host with
        /// a timeout.
        /// </summary>
        public override void OnStop()
        {
            Trace.TraceInformation("Stopping Dolomite WCF Service...");

            try
            {
                // Shut down the service host, gracefully
                if (_serviceHost != null)
                    _serviceHost.Close(TimeSpan.FromSeconds(10));
            }
            catch (TimeoutException)
            {
                Trace.TraceWarning("Failed to stop WCF endpoint within 10 seconds. Connections will forcefully close.");
            }
            catch (Exception e)
            {
                Trace.TraceError("Failed to stop WCF endpoint: {0}", e.Message);
            }
            Trace.TraceInformation("Stopped Dolomite WCF Endpoint");

            base.OnStop();
        }
    }
}
