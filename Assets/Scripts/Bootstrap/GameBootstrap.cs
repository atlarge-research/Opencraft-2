using System;
using System.Collections.Generic;
using Opencraft.Deployment;
using Opencraft.Networking;
using Opencraft.Player.Emulated;
using Opencraft.Player.Multiplay;
using Opencraft.Rendering;
using Opencraft.Statistics;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Scenes;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif


namespace Opencraft.Bootstrap
{
    public static class BootstrapInstance
    {
        public static GameBootstrap instance; // Reference to the ICustomBootstrap
    }
    
    /// <summary>
    /// Reads configuration locally or from remote and sets ups deployment or game worlds accordingly
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public class GameBootstrap : ICustomBootstrap
    {
        // TODO use dict<str worldName, World world>
        public List<World> worlds;
        private World deploymentWorld;
        public bool Initialize(string defaultWorldName)
        {
            // Get the global command line reader class
            if (!CmdArgsReader.ParseCmdArgs())
            {
                Application.Quit();
                return false;
            }

            worlds = new List<World>();

            // Pre world creation initialization
            BootstrapInstance.instance = this;
            NetworkStreamReceiveSystem.DriverConstructor = new NetCodeDriverConstructor();
            
            // Deployment world handles both requesting and answering configuration requests
            if (Config.GetRemoteConfig || Config.isDeploymentService)
            {
                deploymentWorld = SetupDeploymentServiceWorld();
                // Create connection listen/connect request in deployment world
                if (Config.GetRemoteConfig)
                {
                    // Deployment client
                    // Parse deployment network endpoint
                    if (!NetworkEndpoint.TryParse(Config.DeploymentURL, Config.DeploymentPort,
                            out NetworkEndpoint deploymentEndpoint,
                            NetworkFamily.Ipv4))
                    {
                        Debug.Log($"Couldn't parse deployment URL of {Config.DeploymentURL}:{Config.DeploymentPort}, falling back to 127.0.0.1!");
                        deploymentEndpoint = NetworkEndpoint.LoopbackIpv4.WithPort(Config.DeploymentPort);
                    }
                    
                    Entity connReq = deploymentWorld.EntityManager.CreateEntity();
                    deploymentWorld.EntityManager.AddComponentData(connReq,
                        new NetworkStreamRequestConnect { Endpoint = deploymentEndpoint });
                }
                else
                {
                    // Deployment server
                    Entity listenReq = deploymentWorld.EntityManager.CreateEntity();
                    deploymentWorld.EntityManager.AddComponentData(listenReq,
                        new NetworkStreamRequestListen { Endpoint = NetworkEndpoint.AnyIpv4.WithPort(Config.DeploymentPort) });
                }
            }
            else
            {
                // Use only local configuration
                SetupWorldsFromLocalConfig();
            }
            
            return true;
        }
    
        /// <summary>
        /// Creates a world with a minimal set of systems necessary for Netcode for Entities to connect, and the
        /// Deployment systems <see cref="DeploymentReceiveSystem"/> and <see cref="DeploymentServiceSystem"/>.
        /// </summary>
        /// <returns></returns>
        private World SetupDeploymentServiceWorld()
        {
            Debug.Log("Creating deployment world");
            //BootstrappingConfig.DeploymentClientConnectAddress = deploymentEndpoint;
            //BootstrappingConfig.DeploymentPort = Config.DeploymentPort;
            //BootstrappingConfig.DeploymentServerListenAddress = NetworkEndpoint.AnyIpv4.WithPort(BootstrappingConfig.DeploymentPort);
            
            // Create the world
            WorldFlagsExtension flags =  Config.GetRemoteConfig ? WorldFlagsExtension.DeploymentClient : WorldFlagsExtension.DeploymentServer;
            var world = new World("DeploymentWorld", (WorldFlags)flags);
            // Fetch all editor and package (but not user-added) systems
            WorldSystemFilterFlags filterFlags =  Config.GetRemoteConfig ? WorldSystemFilterFlags.ClientSimulation : WorldSystemFilterFlags.ServerSimulation;
            NativeList<SystemTypeIndex> systems = TypeManager.GetUnitySystemsTypeIndices(filterFlags);
            
            // Remove built-in NetCode world initialization 
            NativeList<SystemTypeIndex> filteredSystems = new NativeList<SystemTypeIndex>(64, Allocator.Temp);
            foreach (var system in systems)
            {
                var systemName = TypeManager.GetSystemName(system);
                if( systemName.Contains((FixedString64Bytes)"ConfigureThinClientWorldSystem")
                    ||  systemName.Contains((FixedString64Bytes)"ConfigureClientWorldSystem")
                    ||  systemName.Contains((FixedString64Bytes)"ConfigureServerWorldSystem"))
                    continue;
                filteredSystems.Add(system);
            }

            // Add deployment service systems
            if (Config.GetRemoteConfig)
                filteredSystems.Add(TypeManager.GetSystemTypeIndex(typeof(DeploymentReceiveSystem)));
            else
                filteredSystems.Add(TypeManager.GetSystemTypeIndex(typeof(DeploymentServiceSystem)));

            // Add Unity Scene System for managing GUIDs
            filteredSystems.Add(TypeManager.GetSystemTypeIndex(typeof(SceneSystem)));
            // Add NetCode monitor
            filteredSystems.Add(TypeManager.GetSystemTypeIndex(typeof(ConnectionMonitorSystem)));
            
            // Add AuthoringSceneLoader
            filteredSystems.Add(TypeManager.GetSystemTypeIndex(typeof(AuthoringSceneLoaderSystem)));
            
            // Re-sort the systems
            TypeManager.SortSystemTypesInCreationOrder(filteredSystems);
           
            
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, filteredSystems);
            
            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);

            if (World.DefaultGameObjectInjectionWorld == null)
                World.DefaultGameObjectInjectionWorld = world;

            return world;
        }
        

        public void SetupWorldsFromLocalConfig()
        {
            //SetBootStrapConfig(Config.ServerUrl, Config.ServerPort);
            NativeList<WorldUnmanaged> newWorlds = new NativeList<WorldUnmanaged>(Allocator.Temp);
            SetupWorlds(Config.multiplayStreamingRoles, Config.playTypes, ref newWorlds,
                Config.NumThinClientPlayers, autoStart: true, autoConnect: true, Config.ServerUrl, Config.ServerPort, Config.SignalingUrl);
        }
        
        
        /// <summary>
        /// Sets up bootstrapping details and creates local worlds
        /// </summary>
        public void SetupWorlds(MultiplayStreamingRoles mRole, BootstrapPlayTypes playTypes, ref NativeList<WorldUnmanaged> worldReferences,
            int numThinClients, bool autoStart, bool autoConnect, string serverUrl, ushort serverPort, string signalingUrl, string worldName = "")
        {

            Debug.Log($"Setting up worlds with playType {playTypes} and streaming role {mRole}");

            List<World> newWorlds = new List<World>();
            
            // ================== SETUP WORLDS ==================
            
            Config.multiplayStreamingRoles = mRole;
            
            
            if (playTypes == BootstrapPlayTypes.StreamedClient)
            {
                mRole = MultiplayStreamingRoles.Guest;
            }
            
            //Client
            if (playTypes == BootstrapPlayTypes.Client || playTypes == BootstrapPlayTypes.ClientAndServer)
            {
                // Streamed client
                if (mRole == MultiplayStreamingRoles.Guest)
                {
                    var world = CreateStreamedClientWorld(worldName);
                    newWorlds.Add(world);
                }

                if (mRole != MultiplayStreamingRoles.Guest){
                    var world = CreateDefaultClientWorld(worldName, mRole == MultiplayStreamingRoles.Host, mRole == MultiplayStreamingRoles.CloudHost);
                    newWorlds.Add(world);
                }
            }
            

            // Thin client
            if (playTypes == BootstrapPlayTypes.ThinClient && numThinClients > 0)
            {
                var world = CreateThinClientWorlds(numThinClients, worldName);
                newWorlds.AddRange(world);
                
            }

            // Server
            if (playTypes == BootstrapPlayTypes.Server || playTypes == BootstrapPlayTypes.ClientAndServer)
            {
                var world = CreateDefaultServerWorld(worldName);
                newWorlds.Add(world);
            }

            foreach (var world in newWorlds)
            {
                worldReferences.Add(world.Unmanaged);
                worlds.Add(world);
                
                if (autoStart)
                    SetWorldToUpdating(world);
            }
            
            
            if(autoConnect)
                ConnectWorlds(mRole, playTypes,  serverUrl, serverPort, signalingUrl);
        }


        public void SetWorldToUpdating(World world)
        {
#if UNITY_DOTSRUNTIME
            CustomDOTSWorlds.AppendWorldToClientTickWorld(world);
#else
            if (!ScriptBehaviourUpdateOrder.IsWorldInCurrentPlayerLoop(world))
            {
                Debug.Log($"Adding world {world.Name} to update list");
                ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);
            }
#endif
        }
        
        /// <summary>
        /// Adds worlds to update list
        /// </summary>
        public void StartWorlds(bool autoConnect, MultiplayStreamingRoles mRole,BootstrapPlayTypes playTypes,  string serverUrl,
            ushort serverPort, string signalingUrl)
        {
            
            Debug.Log($"Starting worlds with playType {playTypes} and streaming role {mRole}");
            // ================== SETUP WORLDS ==================
            foreach (var world in worlds)
            {
                // Client worlds
                if (playTypes is BootstrapPlayTypes.Client or BootstrapPlayTypes.ClientAndServer 
                    && world.IsClient() && !world.IsThinClient() && !world.IsStreamedClient())
                {
                    SetWorldToUpdating(world);
                }
                // Streamed guest client worlds
                if (playTypes is BootstrapPlayTypes.Client or BootstrapPlayTypes.ClientAndServer 
                    && mRole == MultiplayStreamingRoles.Guest
                    && world.IsStreamedClient())
                {
                    SetWorldToUpdating(world);
                }
                // Thin client worlds
                if (playTypes == BootstrapPlayTypes.ThinClient && world.IsThinClient())
                {
                    SetWorldToUpdating(world);
                }
                // Server worlds
                if (playTypes is BootstrapPlayTypes.Server or BootstrapPlayTypes.ClientAndServer
                    && world.IsServer())
                {
                    SetWorldToUpdating(world);
                }
            }
            
            if(autoConnect)
                ConnectWorlds(mRole, playTypes,  serverUrl, serverPort, signalingUrl);
        }
        
        
        /// <summary>
        /// Connects worlds with types specified through playTypes and streaming roles
        /// </summary>
        public void ConnectWorlds(MultiplayStreamingRoles mRole,BootstrapPlayTypes playTypes,  string serverUrl,
            ushort serverPort, string signalingUrl)
        {
            
            Debug.Log($"Connecting worlds with playType {playTypes} and streaming role {mRole}");
            // ================== SETUP WORLDS ==================
            foreach (var world in worlds)
            {
                // Client worlds
                if (playTypes is BootstrapPlayTypes.Client or BootstrapPlayTypes.ClientAndServer 
                    && world.IsClient() && !world.IsThinClient() && !world.IsStreamedClient()
                    && mRole != MultiplayStreamingRoles.Guest)
                {
                    Entity connReq = world.EntityManager.CreateEntity();
                    NetworkEndpoint.TryParse(serverUrl, serverPort,
                        out NetworkEndpoint gameEndpoint, NetworkFamily.Ipv4);
                    Debug.Log($"Created connection request for {gameEndpoint}");
                    world.EntityManager.AddComponentData(connReq,
                        new NetworkStreamRequestConnect { Endpoint = gameEndpoint });
                }
                // Streamed guest client worlds
                if ((playTypes == BootstrapPlayTypes.Client || playTypes == BootstrapPlayTypes.ClientAndServer) 
                    && mRole == MultiplayStreamingRoles.Guest
                    && world.IsStreamedClient())
                {
                    Entity connReq = world.EntityManager.CreateEntity();
                    
                    Debug.Log($"Creating multiplay guest connect with endpoint {signalingUrl}");
                    world.EntityManager.AddComponentData(connReq,
                        new StreamedClientRequestConnect{ url = new FixedString512Bytes(signalingUrl) });
                }
                // Thin client worlds
                if (playTypes == BootstrapPlayTypes.ThinClient && world.IsThinClient())
                {
                    Entity connReq = world.EntityManager.CreateEntity();
                    NetworkEndpoint.TryParse(serverUrl,serverPort,
                        out NetworkEndpoint gameEndpoint, NetworkFamily.Ipv4);
                    Debug.Log($"Created connection request for {gameEndpoint}");
                    world.EntityManager.AddComponentData(connReq,
                        new NetworkStreamRequestConnect { Endpoint = gameEndpoint });
                }
                // Server worlds
                if ((playTypes == BootstrapPlayTypes.Server || playTypes == BootstrapPlayTypes.ClientAndServer)
                    && world.IsServer())
                {
                    Entity connReq = world.EntityManager.CreateEntity();
                    var listenNetworkEndpoint = NetworkEndpoint.AnyIpv4.WithPort(serverPort);
                    Debug.Log($"Created listen request for {listenNetworkEndpoint}");
                    world.EntityManager.AddComponentData(connReq,
                        new NetworkStreamRequestListen { Endpoint = listenNetworkEndpoint });
                }
                
            }
        }
        
        public static World CreateDefaultClientWorld(string worldName, bool isHost = false, bool isCloudHost = false)
        {
            var clientSystems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.ClientSimulation |
                                                                         WorldSystemFilterFlags.Presentation);
            // Disable the default NetCode world configuration
            var filteredClientSystems = new List<Type>();
            foreach (var system in clientSystems)
            {
                if(system.Name == "ConfigureThinClientWorldSystem" || system.Name == "ConfigureClientWorldSystem")
                    continue;
                filteredClientSystems.Add(system);
            }
            
            if (isHost)
            {
                if (worldName == "")
                    worldName = "HostClientWorld";
                return CreateClientWorld(worldName, (WorldFlags)WorldFlagsExtension.HostClient,
                    filteredClientSystems);
            }
            else if(isCloudHost)
            {
                if (worldName == "")
                    worldName = "CloudHostClientWorld";
                return CreateClientWorld(worldName, (WorldFlags)WorldFlagsExtension.CloudHostClient,
                    filteredClientSystems); 
            }
            else
            {
                if (worldName == "")
                    worldName = "ClientWorld";
                return CreateClientWorld(worldName, WorldFlags.GameClient, filteredClientSystems);
            }
                
        }

        public static World CreateStreamedClientWorld(string worldName)
        {
            var systems = new List<Type> { typeof(MultiplayInitSystem), typeof(EmulationInitSystem), typeof(TakeScreenshotSystem), typeof(UpdateWorldTimeSystem), typeof(StopWorldSystem) };
            if (worldName == "")
                worldName = "StreamingGuestWorld";
            return CreateClientWorld(worldName, (WorldFlags)WorldFlagsExtension.StreamedClient, systems);
        }
        
        
        public static List<World> CreateThinClientWorlds(int numThinClients, string worldName)
        {
            List<World> newWorlds = new List<World>();
            
            var thinClientSystems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.ThinClientSimulation);
            
            // Disable the default NetCode world configuration
            var filteredThinClientSystems = new List<Type>();
            foreach (var system in thinClientSystems)
            {
                if(system.Name == "ConfigureThinClientWorldSystem" || system.Name == "ConfigureClientWorldSystem")
                    continue;
                filteredThinClientSystems.Add(system);
            }
            
            //if(autoConnect)
            //    filteredThinClientSystems.Add(typeof(CustomAutoconnectSystem));

            if (worldName == "")
                worldName = "ThinClientWorld_";
            for (var i = 0; i < numThinClients; i++)
            {
                newWorlds.Add(CreateClientWorld(worldName+$"{i}", WorldFlags.GameThinClient, filteredThinClientSystems));
            }

            return newWorlds;
        }

        public static World CreateDefaultServerWorld(string worldName)
        {
            // todo: specify what systems in the server world
            var serverSystems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.ServerSimulation);
            
            var filteredServerSystems = new List<Type>();
            foreach (var system in serverSystems)
            {
                if(system.Name == "ConfigureServerWorldSystem")
                    continue;
                filteredServerSystems.Add(system);
            }

            if (worldName == "")
                worldName = "ServerWorld";
            return CreateServerWorld(worldName, WorldFlags.GameServer, filteredServerSystems );
        }

        /// <summary>
        /// Utility method for creating new client worlds.
        /// </summary>
        /// <param name="name">The client world name</param>
        /// <param name="flags">WorldFlags for the created world</param>
        /// <param name="systems">List of systems the world will include</param>
        /// <returns></returns>
        public static World CreateClientWorld(string name, WorldFlags flags, IReadOnlyList<Type> systems)
        {
            var world = new World(name, flags);

            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
            

            if (World.DefaultGameObjectInjectionWorld == null)
                World.DefaultGameObjectInjectionWorld = world;

            return world;
        }
        
        /// <summary>
        /// Utility method for creating a new server world.
        /// Can be used in custom implementations of `Initialize` as well as in your game logic (in particular client/server build)
        /// when you need to create server programmatically (ex: frontend that allow selecting the role or other logic).
        /// </summary>
        /// <param name="name">The server world name</param>
        /// <returns></returns>
        public static World CreateServerWorld(string name, WorldFlags flags, IReadOnlyList<Type> systems)
        {
            var world = new World(name, flags);
            
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
            

#if UNITY_DOTSRUNTIME
            CustomDOTSWorlds.AppendWorldToServerTickWorld(world);
#else
            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);
#endif

            if (World.DefaultGameObjectInjectionWorld == null)
                World.DefaultGameObjectInjectionWorld = world;

            return world;
        }


        public void ExitGame()
        {
            if (Config.LogStats)
                StatisticsWriterInstance.WriteStatisticsBuffer();
                
            #if UNITY_EDITOR
                EditorApplication.ExitPlaymode();
            #else
                Application.Quit();
            #endif
        }
        
        [Serializable]
        public enum BootstrapPlayTypes
        {
            /// <summary>
            /// The application can run as client, server or both. By default, both client and server world are created
            /// and the application can host and play as client at the same time.
            /// <para>
            /// This is the default modality when playing in the editor, unless changed by using the play mode tool.
            /// </para>
            /// </summary>
            ClientAndServer = 0,
            ServerAndClient = 0, // Aliases
            ClientServer    = 0,
            ServerClient    = 0,
            /// <summary>
            /// The application run as a client. Only clients worlds are created and the application should connect to
            /// a server.
            /// </summary>
            Client = 1,
            /// <summary>
            /// The application run as a server. Usually only the server world is created and the application can only
            /// listen for incoming connection.
            /// </summary>
            Server = 2,
            /// <summary>
            /// The application run as a thin client. Only connections to a server and input emulation are performed.
            /// No frontend systems are run. 
            /// </summary>
            StreamedClient=3,
            StreamClient=3,
            GuestClient=3,
            /// <summary>
            /// Minimal client for running player emulation with no frontend, useful for experiments and debugging
            /// </summary>
            ThinClient = 4
        }
    }
    
    
}


