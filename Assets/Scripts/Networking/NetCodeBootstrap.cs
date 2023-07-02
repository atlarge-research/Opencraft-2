using System;
using System.Collections.Generic;
using System.Linq;
using Opencraft.Player.Multiplay;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;

namespace Opencraft.Networking
{
    // Create a custom bootstrap, which sets the network driver and creates ECS worlds
    [UnityEngine.Scripting.Preserve]
    public class NetCodeBootstrap : ClientServerBootstrap
    {
        public override bool Initialize(string defaultWorldName)
        {
            AutoConnectPort = 7979; // Enables auto connect
            CreateDefaultClientServerWorlds();
            NetworkStreamReceiveSystem.DriverConstructor = new NetCodeDriverConstructor();
            return true;
        }


        // Setup our worlds and specify what systems they run
        protected override void CreateDefaultClientServerWorlds()
        {
            var requestedPlayType = RequestedPlayType;
            if (requestedPlayType is PlayType.Server or PlayType.ClientAndServer)
            {
                CreateServerWorld("ServerWorld");
            }

            if (requestedPlayType is PlayType.Client or PlayType.ClientAndServer)
            {
                if (CmdArgs.ClientStreamingRole == CmdArgs.StreamingRole.Guest)
                {
                    CreateStreamClientWorld("StreamClientWorld");
                }
                else
                {
                    CreateClientWorld("ClientWorld");
                }

#if UNITY_EDITOR
            var requestedNumThinClients = RequestedNumThinClients;
            for (var i = 0; i < requestedNumThinClients; i++)
            {
                CreateThinClientWorld();
            }
#endif
            }
        }

        // Utility method for creating new stream clients worlds.
        public static World CreateStreamClientWorld(string name)
        {
#if UNITY_SERVER && !UNITY_EDITOR
            throw new NotImplementedException();
#else
            var world = new World(name, WorldFlags.Game);
            // On Multiplay guest clients we don't really utilize any ECS-specific systems,
            // so we use MultiplayInitSystem to start the relevant MonoBehaviours
            var systems = new List<Type> { typeof(MultiplayInitSystem) };
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
            
            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);

            if (World.DefaultGameObjectInjectionWorld == null)
                World.DefaultGameObjectInjectionWorld = world;

            return world;
#endif
        }
    }

    // Custom network settings and driver initialize to specify network parameters
    public class NetCodeDriverConstructor : INetworkStreamDriverConstructor
    {
        // Custom timeout time
        private static readonly int s_DisconnectTimeout = 2000;

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
        var settings = CreateNetworkSettings(100);
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


