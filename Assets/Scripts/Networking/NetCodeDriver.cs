using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;

namespace Opencraft.Networking
{
    // Custom network settings and driver initialize to specify network parameters
    public class NetCodeDriverConstructor : INetworkStreamDriverConstructor
    {
        // Custom timeout time
        private static readonly int s_DisconnectTimeout = 4000;

        private NetworkSettings CreateNetworkSettings(int maxFrameTime = 0)
        {
            var settings = new NetworkSettings();
            settings.WithNetworkConfigParameters(
                connectTimeoutMS: 1000,
                disconnectTimeoutMS: s_DisconnectTimeout,
                heartbeatTimeoutMS: s_DisconnectTimeout / 2,
                fixedFrameTimeMS: 0,
                maxFrameTimeMS: maxFrameTime);
            settings.WithReliableStageParameters(windowSize: 32)
                .WithFragmentationStageParameters(payloadCapacity: 16 * 1024);
            return settings;
        }

        public void CreateClientDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug)
        {
            var driverInstance = new NetworkDriverStore.NetworkDriverInstance();
    #if UNITY_EDITOR || NETCODE_DEBUG
            var settings = CreateNetworkSettings(0);
            driverInstance.simulatorEnabled = NetworkSimulatorSettings.Enabled;
            if (NetworkSimulatorSettings.Enabled)
            {
                NetworkSimulatorSettings.SetSimulatorSettings(ref settings);
                driverInstance.driver = NetworkDriver.Create(new UDPNetworkInterface(), settings);
                DefaultDriverBuilder.CreateClientSimulatorPipelines(ref driverInstance);
            }
            else
            {
                driverInstance.driver = NetworkDriver.Create(new UDPNetworkInterface(), settings);
                DefaultDriverBuilder.CreateClientPipelines(ref driverInstance);
            }
    #else
            var settings = CreateNetworkSettings();
            driverInstance.driver = NetworkDriver.Create(new UDPNetworkInterface(), settings);
            DefaultDriverBuilder.CreateClientPipelines(ref driverInstance);
    #endif
            driverStore.RegisterDriver(TransportType.Socket, driverInstance);
        }

        public void CreateServerDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug)
        {
            var settings = CreateNetworkSettings();
            var driverInstance = new NetworkDriverStore.NetworkDriverInstance();
            driverInstance.driver = NetworkDriver.Create(new UDPNetworkInterface(), settings);
            DefaultDriverBuilder.CreateServerPipelines(ref driverInstance);
            driverStore.RegisterDriver(TransportType.Socket, driverInstance);
        }
    }
}