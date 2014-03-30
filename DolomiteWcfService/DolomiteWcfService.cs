using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Web;
using System.Threading;
using DolomiteManagement;
using DolomiteWcfService.Cors;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace DolomiteWcfService
{
    public class DolomiteWcfService : RoleEntryPoint
    {
        #region Constants

        private const string IdleTimeoutKey = "IdleTimeout";
        private const string AbsolulteTimeoutKey = "AbsoluteTimeout";
        private const string TrackContainerKey = "TrackStorageContainer";
        private const string UserKeyEnabledKey = "UserKeysEnabled";

        #endregion

        #region Member Variables

        /// <summary>
        /// Instance of the service host for the dolomite wcf endpoint
        /// </summary>
        private WebServiceHost _tracksHost;

        private WebServiceHost _autoPlaylistHost;

        private WebServiceHost _staticPlaylistHost;

        private WebServiceHost _userHost;

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
            // ReSharper disable once FunctionNeverReturns
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

            // Initialize the managers
            try
            {
                InitializeUserManager();
                InitializeTrackManager();
            }
            catch (Exception e)
            {
                Trace.TraceError("Failed to initialize managers: {0}", e.Message);
                Trace.TraceError("Giving up on starting service.");
                return false;
            }

            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 12;

            // Grab the base address from the role manager
            IPEndPoint endpoint = RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["DolomiteRest"].IPEndpoint;
            WebHttpBinding webBinding = new WebHttpBinding
            {
                MaxReceivedMessageSize = 200 * 1024 * 2024, // 200Mb
                Security = {Mode = WebHttpSecurityMode.Transport, Transport = new HttpTransportSecurity()} 
            };
            Uri baseAddress = new Uri(String.Format("https://{0}:{1}", endpoint.Address, endpoint.Port));

            // Spin up a service end point
            try
            {
                _tracksHost = new WebServiceHost(typeof (TracksEndpoint), baseAddress);
                _tracksHost.AddServiceEndpoint(typeof (ITracksEndpoint), webBinding, "/tracks/");

                _autoPlaylistHost = new WebServiceHost(typeof(AutoPlaylistEndpoint), baseAddress);
                _autoPlaylistHost.AddServiceEndpoint(typeof (IAutoPlaylistEndpoint), webBinding, "/playlists/auto/");

                _staticPlaylistHost = new WebServiceHost(typeof (StaticPlaylistEndpoint), baseAddress);
                _staticPlaylistHost.AddServiceEndpoint(typeof (IStaticPlaylistEndpoint), webBinding,
                    "/playlists/static/");

                _userHost = new WebServiceHost(typeof(UserEndpoint), baseAddress);
                _userHost.AddServiceEndpoint(typeof (IUserEndpoint), webBinding, "/users/");

                // Enable the http metadata output
                ServiceMetadataBehavior smb = new ServiceMetadataBehavior
                    {
                        HttpGetEnabled = true,
                        MetadataExporter = {PolicyVersion = PolicyVersion.Policy15},
                    };

                // Enable CORS on the endpoints
                IServiceBehavior ecb = new EnableCorsBehavior();

                _tracksHost.Description.Behaviors.Add(smb);
                _tracksHost.Description.Behaviors.Add(ecb);
                _tracksHost.Open();

                _autoPlaylistHost.Description.Behaviors.Add(smb);
                _autoPlaylistHost.Description.Behaviors.Add(ecb);
                _autoPlaylistHost.Open();

                _staticPlaylistHost.Description.Behaviors.Add(smb);
                _staticPlaylistHost.Description.Behaviors.Add(ecb);
                _staticPlaylistHost.Open();

                _userHost.Description.Behaviors.Add(smb);
                _userHost.Description.Behaviors.Add(ecb);
                _userHost.Open();

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
                if (_tracksHost != null)
                    _tracksHost.Close(TimeSpan.FromSeconds(10));
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

        private static void InitializeUserManager()
        {
            try
            {
                // Get the user enabled key
                var ukEnabled = RoleEnvironment.GetConfigurationSettingValue(UserKeyEnabledKey);
                UserManager.UserKeysEnabled = bool.Parse(ukEnabled);

                // Get the idle timeout
                var idleTimeout = RoleEnvironment.GetConfigurationSettingValue(IdleTimeoutKey);
                UserManager.IdleTimeoutInterval = TimeSpan.Parse(idleTimeout);

                // Get the absolute timeout
                var absTimeout = RoleEnvironment.GetConfigurationSettingValue(AbsolulteTimeoutKey);
                UserManager.AbsoluteTimeoutInterval = TimeSpan.Parse(absTimeout);
            }
            catch (Exception e)
            {
                throw new InvalidDataException("Failed to initialize the User Manager.", e);
            }
        }

        private static void InitializeTrackManager()
        {
            try
            {
                // Get the track storage container
                var trackContainer = RoleEnvironment.GetConfigurationSettingValue(TrackContainerKey);
                TrackManager.TrackStorageContainer = trackContainer;
            }
            catch (Exception e)
            {
                throw new InvalidDataException("Failed to initialize the Track Manager.", e);
            }
        }
    }
}
