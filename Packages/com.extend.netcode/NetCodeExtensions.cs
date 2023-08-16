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
        public static NetworkEndpoint DeploymentClientConnectAddress = NetworkEndpoint.LoopbackIpv4.WithPort(DeploymentPort);
        public static NetworkEndpoint DeploymentServerListenAddress = NetworkEndpoint.AnyIpv4.WithPort(DeploymentPort);
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
    /// Autoconnect system
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [CreateAfter(typeof(NetworkStreamReceiveSystem))]
    public partial struct CustomAutoconnectSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<NetworkStreamDriver>(out NetworkStreamDriver netDriver))
            {
                Debug.LogError($"AutoConnect system cannot find a NetworkStreamDriver!");
                return;
            }
                
            if(state.World.IsServer())
            {
                if (state.World.IsDeploymentServer())
                {
                    netDriver.Listen(BootstrappingConfig.DeploymentServerListenAddress);
                    Debug.Log($"Calling Listen on deployment server at {BootstrappingConfig.DeploymentServerListenAddress}");   
                }
                else
                {
                    netDriver.Listen(BootstrappingConfig.ServerListenAddress);
                    Debug.Log($"Calling Listen on server at {BootstrappingConfig.ServerListenAddress}"); 
                }
            }

            if (state.World.IsClient() || state.World.IsThinClient())
            {
                if (state.World.IsDeploymentClient())
                {
                    netDriver.Connect(state.EntityManager, BootstrappingConfig.DeploymentClientConnectAddress);
                    Debug.Log($"Calling connect on deployment client at {BootstrappingConfig.DeploymentClientConnectAddress}");
                }
                else
                {
                    netDriver.Connect(state.EntityManager, BootstrappingConfig.ClientConnectAddress);
                    Debug.Log($"Calling connect on client at {BootstrappingConfig.ClientConnectAddress}");
                }
            }
            state.Enabled = false;
        }
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


