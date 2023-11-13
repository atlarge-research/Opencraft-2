using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Networking.Transport;
using UnityEngine;

namespace Unity.NetCode
{
    /*
    /// <summary>
    /// Holds configuration details used during NetCode bootstrapping process.
    /// </summary>
    public static class BootstrappingConfig
    {
        public static ICustomBootstrap BootStrapClass; // Reference to the ICustomBootstrap
        public static ushort ServerPort = 7979;
        public static NetworkEndpoint ClientConnectAddress = NetworkEndpoint.LoopbackIpv4.WithPort(ServerPort);
        public static NetworkEndpoint ServerListenAddress = NetworkEndpoint.AnyIpv4.WithPort(ServerPort);
        public static ushort DeploymentPort = 7980;
        public static NetworkEndpoint DeploymentClientConnectAddress = NetworkEndpoint.LoopbackIpv4.WithPort(DeploymentPort);
        public static NetworkEndpoint DeploymentServerListenAddress = NetworkEndpoint.AnyIpv4.WithPort(DeploymentPort);
    }*/
    
    /// <summary>
    /// Specify additional traits a <see cref="World"/> can have.
    /// </summary>
    [Flags]
    public enum WorldFlagsExtension : int
    {
        /// <summary>
        /// Regular client that can host additional streamed clients
        /// </summary>
        HostClient   = 1 << 11 | WorldFlags.GameClient,
        /// <summary>
        /// Hosts streamed clients but has no local client
        /// </summary>
        CloudHostClient   = 1 << 12 | HostClient |  WorldFlags.GameClient,
        /// <summary>
        /// Streamed world, only contains video stream display and input sending
        /// </summary>
        StreamedClient   = 1 << 13 | WorldFlags.Game,
        /// <summary>
        /// Deployment worlds <see cref="World"/> for remote configuration
        /// </summary>
        DeploymentClient = 1 << 14 | WorldFlags.GameClient,
        DeploymentServer = 1 << 15 | WorldFlags.GameServer,
    }
    
    /// <summary>
    /// Netcode specific extension methods for additional worlds.
    /// </summary>
    public static class WorldExtensions
    {
        /// <summary>
        /// Check if a world is a host client.
        /// </summary>
        /// <param name="world">A <see cref="World"/> instance</param>
        /// <returns></returns>
        public static bool IsHostClient(this World world)
        {
            return ((WorldFlagsExtension)world.Flags & WorldFlagsExtension.HostClient) == WorldFlagsExtension.HostClient;
        }
        
        /// <summary>
        /// Check if a world is a host client.
        /// </summary>
        /// <param name="world">A <see cref="World"/> instance</param>
        /// <returns></returns>
        public static bool IsCloudHostClient(this WorldUnmanaged world)
        {
            return ((WorldFlagsExtension)world.Flags & WorldFlagsExtension.CloudHostClient) == WorldFlagsExtension.CloudHostClient;
        }
        
        /// <summary>
        /// Check if a world is a cloud host client.
        /// </summary>
        /// <param name="world">A <see cref="World"/> instance</param>
        /// <returns></returns>
        public static bool IsCloudHostClient(this World world)
        {
            return ((WorldFlagsExtension)world.Flags & WorldFlagsExtension.CloudHostClient) == WorldFlagsExtension.CloudHostClient;
        }
        
        /// <summary>
        /// Check if a world is a cloud host client.
        /// </summary>
        /// <param name="world">A <see cref="World"/> instance</param>
        /// <returns></returns>
        public static bool IsHostClient(this WorldUnmanaged world)
        {
            return ((WorldFlagsExtension)world.Flags & WorldFlagsExtension.HostClient) == WorldFlagsExtension.HostClient;
        }
        /// <summary>
        /// Check if a world is a streamed client.
        /// </summary>
        /// <param name="world">A <see cref="World"/> instance</param>
        /// <returns></returns>
        public static bool IsStreamedClient(this World world)
        {
            return ((WorldFlagsExtension)world.Flags & WorldFlagsExtension.StreamedClient) == WorldFlagsExtension.StreamedClient;
        }
        
        /// <summary>
        /// Check if a world is a streamed client.
        /// </summary>
        /// <param name="world">A <see cref="World"/> instance</param>
        /// <returns></returns>
        public static bool IsStreamedClient(this WorldUnmanaged world)
        {
            return ((WorldFlagsExtension)world.Flags & WorldFlagsExtension.StreamedClient) == WorldFlagsExtension.StreamedClient;
        }
        
        /// <summary>
        /// Check if a world is a deployment server.
        /// </summary>
        /// <param name="world">A <see cref="World"/> instance</param>
        /// <returns></returns>
        public static bool IsDeploymentServer(this World world)
        {
            return ((WorldFlagsExtension)world.Flags & WorldFlagsExtension.DeploymentServer) == WorldFlagsExtension.DeploymentServer;
        }
        
        /// <summary>
        /// Check if an unmanaged world is a deployment server.
        /// </summary>
        /// <param name="world">A <see cref="WorldUnmanaged"/> instance</param>
        /// <returns></returns>
        public static bool IsDeploymentServer(this WorldUnmanaged world)
        {
            return ((WorldFlagsExtension)world.Flags & WorldFlagsExtension.DeploymentServer) == WorldFlagsExtension.DeploymentServer;
        }
        
        /// <summary>
        /// Check if a world is a deployment server.
        /// </summary>
        /// <param name="world">A <see cref="World"/> instance</param>
        /// <returns></returns>
        public static bool IsDeploymentClient(this World world)
        {
            return ((WorldFlagsExtension)world.Flags & WorldFlagsExtension.DeploymentClient) == WorldFlagsExtension.DeploymentClient;
        }
        
        /// <summary>
        /// Check if an unmanaged world is a deployment server.
        /// </summary>
        /// <param name="world">A <see cref="WorldUnmanaged"/> instance</param>
        /// <returns></returns>
        public static bool IsDeploymentClient(this WorldUnmanaged world)
        {
            return ((WorldFlagsExtension)world.Flags & WorldFlagsExtension.DeploymentClient) == WorldFlagsExtension.DeploymentClient;
        }
    }
    
    
    /// <summary>
    /// Create when all scene loading is complete
    /// </summary>
    public struct WorldReady : IComponentData
    {
    }
    
    /// <summary>
    /// Used to call connect/listen when not using the autoconnect system
    /// </summary>
    public struct ConnectRequest : IComponentData
    {
    }
    
    /// <summary>
    /// Configure system
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [CreateAfter(typeof(NetworkStreamReceiveSystem))]
    public partial struct CustomConfigureWorldSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            if(state.World.IsServer())
            {
                var simulationGroup = state.World.GetExistingSystemManaged<SimulationSystemGroup>();
                simulationGroup.SetRateManagerCreateAllocator(new NetcodeServerRateManager(simulationGroup));

                var predictionGroup = state.World.GetExistingSystemManaged<PredictedSimulationSystemGroup>();
                predictionGroup.RateManager = new NetcodeServerPredictionRateManager(predictionGroup);

                ++ClientServerBootstrap.WorldCounts.Data.serverWorlds;
            }

            if (state.World.IsClient())
            {
                var simulationGroup = state.World.GetExistingSystemManaged<SimulationSystemGroup>();
                simulationGroup.RateManager = new NetcodeClientRateManager(simulationGroup);

                var predictionGroup = state.World.GetExistingSystemManaged<PredictedSimulationSystemGroup>();
                predictionGroup.SetRateManagerCreateAllocator(new NetcodeClientPredictionRateManager(predictionGroup));

                ++ClientServerBootstrap.WorldCounts.Data.clientWorlds;
            }

            if (state.World.IsThinClient())
            {
                var simulationGroup = state.World.GetExistingSystemManaged<SimulationSystemGroup>();
                simulationGroup.RateManager = new NetcodeClientRateManager(simulationGroup);
                // No prediction on thin client
                
                ++ClientServerBootstrap.WorldCounts.Data.clientWorlds;
            }
            state.Enabled = false;
        }
        
        public void OnDestroy(ref SystemState state)
        {
            if(state.World.IsServer())
            {
                --ClientServerBootstrap.WorldCounts.Data.serverWorlds;
            }

            if (state.World.IsClient() || state.World.IsThinClient())
            {
                --ClientServerBootstrap.WorldCounts.Data.clientWorlds;
            }
        }
        
    }

#if UNITY_DOTSRUNTIME
    /// <summary>
    /// Manages creation of DOTS worlds and ensures necessary systems are present. 
    /// </summary>
    public static class CustomDOTSWorlds
    {
        public static void CreateTickWorld()
        {
            if (World.DefaultGameObjectInjectionWorld == null)
            {
                World.DefaultGameObjectInjectionWorld = new World("NetcodeTickWorld", WorldFlags.Game);

                var systems = new Type[]
                {
#if !UNITY_SERVER
                    typeof(TickClientInitializationSystem), typeof(TickClientSimulationSystem),
                    typeof(TickClientPresentationSystem),
#endif
#if !UNITY_CLIENT
                    typeof(TickServerInitializationSystem), typeof(TickServerSimulationSystem),
#endif
                    typeof(WorldUpdateAllocatorResetSystem)
                };
                DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(World.DefaultGameObjectInjectionWorld,
                    systems);
            }
        }
#if !UNITY_CLIENT
        public static void AppendWorldToServerTickWorld(World childWorld)
        {
            CreateTickWorld();
            var initializationTickSystem = World.DefaultGameObjectInjectionWorld
                ?.GetExistingSystemManaged<TickServerInitializationSystem>();
            var simulationTickSystem = World.DefaultGameObjectInjectionWorld
                ?.GetExistingSystemManaged<TickServerSimulationSystem>();

            //Bind main world group to tick systems (DefaultWorld tick the client world)
            if (initializationTickSystem == null || simulationTickSystem == null)
                throw new InvalidOperationException(
                    "Tying to add a world to the tick systems of the default world, but the default world does not have the tick systems");

            var initializationGroup = childWorld.GetExistingSystemManaged<InitializationSystemGroup>();
            var simulationGroup = childWorld.GetExistingSystemManaged<SimulationSystemGroup>();

            if (initializationGroup != null)
                initializationTickSystem.AddSystemGroupToTickList(initializationGroup);
            if (simulationGroup != null)
                simulationTickSystem.AddSystemGroupToTickList(simulationGroup);
        }
#endif
#if !UNITY_SERVER
        public static void AppendWorldToClientTickWorld(World childWorld)
        {
            CreateTickWorld();
            var initializationTickSystem = World.DefaultGameObjectInjectionWorld
                ?.GetExistingSystemManaged<TickClientInitializationSystem>();
            var simulationTickSystem = World.DefaultGameObjectInjectionWorld
                ?.GetExistingSystemManaged<TickClientSimulationSystem>();
            var presentationTickSystem = World.DefaultGameObjectInjectionWorld
                ?.GetExistingSystemManaged<TickClientPresentationSystem>();

            //Bind main world group to tick systems (DefaultWorld tick the client world)
            if (initializationTickSystem == null || simulationTickSystem == null || presentationTickSystem == null)
                throw new InvalidOperationException(
                    "Tying to add a world to the tick systems of the default world, but the default world does not have the tick systems");

            var initializationGroup = childWorld.GetExistingSystemManaged<InitializationSystemGroup>();
            var simulationGroup = childWorld.GetExistingSystemManaged<SimulationSystemGroup>();
            var presentationGroup = childWorld.GetExistingSystemManaged<PresentationSystemGroup>();

            if (initializationGroup != null)
                initializationTickSystem.AddSystemGroupToTickList(initializationGroup);
            if (simulationGroup != null)
                simulationTickSystem.AddSystemGroupToTickList(simulationGroup);
            if (presentationGroup != null)
                presentationTickSystem.AddSystemGroupToTickList(presentationGroup);
        }
#endif
    }
#endif
}


