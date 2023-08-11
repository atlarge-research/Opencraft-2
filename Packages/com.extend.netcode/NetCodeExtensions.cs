using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Networking.Transport;
using UnityEngine;

namespace Unity.NetCode
{
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
        public static NetworkEndpoint DeploymentClientConnectAddress = NetworkEndpoint.LoopbackIpv4.WithPort(ServerPort);
        public static NetworkEndpoint DeploymentServerListenAddress = NetworkEndpoint.AnyIpv4.WithPort(ServerPort);
    }
    
    /// <summary>
    /// Specify additional traits a <see cref="World"/> can have.
    /// </summary>
    [Flags]
    public enum WorldFlagsExtension : int
    {
        /// <summary>
        /// Deployment worlds <see cref="World"/> for remote configuration
        /// </summary>
        DeploymentClient = 1 << 11 | WorldFlags.GameClient,
        DeploymentServer = 1 << 12 | WorldFlags.GameServer,
    }
    
    /// <summary>
    /// Netcode specific extension methods for deployment worlds.
    /// </summary>
    public static class DeploymentWorldExtensions
    {
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
    /// Initializes server world by calling Listen with the parameters in BootstrappingConfig
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [CreateAfter(typeof(NetworkStreamReceiveSystem))]
    public partial struct CustomConfigureServerWorldSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            if (!state.World.IsServer() && !state.World.IsDeploymentServer() )
                throw new InvalidOperationException("Server worlds must be created with the WorldFlags.GameServer or WorldFlagsExtension.DeploymentServer flag");
            var simulationGroup = state.World.GetExistingSystemManaged<SimulationSystemGroup>();
            simulationGroup.SetRateManagerCreateAllocator(new NetcodeServerRateManager(simulationGroup));

            var predictionGroup = state.World.GetExistingSystemManaged<PredictedSimulationSystemGroup>();
            predictionGroup.RateManager = new NetcodeServerPredictionRateManager(predictionGroup);

            // Still update the default bootstrap for maintaining functionality of editor window and debugging
            ++ClientServerBootstrap.WorldCounts.Data.serverWorlds;
            
            // Call Listen on the server
            
            NetworkEndpoint endpoint;
            if (state.World.IsDeploymentServer())
                endpoint = BootstrappingConfig.DeploymentServerListenAddress;
            else
                endpoint = BootstrappingConfig.ServerListenAddress;
            SystemAPI.GetSingletonRW<NetworkStreamDriver>().ValueRW.Listen(endpoint);
            Debug.Log($"Calling Listen on server at {endpoint}");   
            
            state.Enabled = false;
        }

        public void OnDestroy(ref SystemState state)
        {
            --ClientServerBootstrap.WorldCounts.Data.serverWorlds;
        }
    }
    /// <summary>
    /// Initializes client world by calling connect with the parameters in BootstrappingConfig
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [CreateAfter(typeof(NetworkStreamReceiveSystem))]
    public partial struct CustomConfigureClientWorldSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            if (!state.World.IsClient() && !state.World.IsThinClient() && !state.World.IsDeploymentClient())
                throw new InvalidOperationException("Client worlds must be created with the WorldFlags.GameClient or WorldFlagsExtension.DeploymentClient flag");
            var simulationGroup = state.World.GetExistingSystemManaged<SimulationSystemGroup>();
            simulationGroup.RateManager = new NetcodeClientRateManager(simulationGroup);

            var predictionGroup = state.World.GetExistingSystemManaged<PredictedSimulationSystemGroup>();
            predictionGroup.SetRateManagerCreateAllocator(new NetcodeClientPredictionRateManager(predictionGroup));

            ++ClientServerBootstrap.WorldCounts.Data.clientWorlds;
            
            // Call connect on client
            NetworkEndpoint endpoint;
            if (state.World.IsDeploymentClient())
                endpoint = BootstrappingConfig.DeploymentClientConnectAddress;
            else
                endpoint = BootstrappingConfig.ClientConnectAddress;
            SystemAPI.GetSingletonRW<NetworkStreamDriver>().ValueRW.Connect(state.EntityManager, endpoint);
            Debug.Log($"Calling connect on new client at {endpoint}");    
            
            state.Enabled = false;
        }

        public void OnDestroy(ref SystemState state)
        {
            --ClientServerBootstrap.WorldCounts.Data.clientWorlds;
        }
    }

    /// <summary>
    /// Initializes thin client world by calling connect with the parameters in BootstrappingConfig
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ThinClientSimulation)]
    [CreateAfter(typeof(NetworkStreamReceiveSystem))]
    public partial struct CustomConfigureThinClientWorldSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            if (!state.World.IsThinClient())
                throw new InvalidOperationException("ThinClient worlds must be created with the WorldFlags.GameThinClient flag");
            var simulationGroup = state.World.GetExistingSystemManaged<SimulationSystemGroup>();
            simulationGroup.RateManager = new NetcodeClientRateManager(simulationGroup);

            ++ClientServerBootstrap.WorldCounts.Data.clientWorlds;
            // Call connect on thin client
            Debug.Log($"Calling connect on thin client at {BootstrappingConfig.ClientConnectAddress}");
            SystemAPI.GetSingletonRW<NetworkStreamDriver>().ValueRW.Connect(state.EntityManager, BootstrappingConfig.ClientConnectAddress);

            state.Enabled = false;
        }

        public void OnDestroy(ref SystemState state)
        {
            --ClientServerBootstrap.WorldCounts.Data.clientWorlds;
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


