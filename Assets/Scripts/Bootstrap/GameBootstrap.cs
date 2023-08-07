using System;
using System.Collections.Generic;
using System.Linq;
using Opencraft.Networking;
using Opencraft.Player.Emulated;
using Opencraft.Player.Multiplay;
using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;
using WebSocketSharp;



namespace Opencraft.Bootstrap
{
    // Reads configuration locally or from remote and sets ups worlds accordingly
    [UnityEngine.Scripting.Preserve]
    public class GameBootstrap : ICustomBootstrap
    {

        public bool Initialize(string defaultWorldName)
        {
            CmdArgsReader cmdArgsReader = (CmdArgsReader)GameObject.FindFirstObjectByType(typeof(CmdArgsReader));
            if (!cmdArgsReader.ParseCmdArgs())
            {
                Application.Quit();
            }
            // If signaling server URL is given, wait for it to connect with a timeout and receive configuration from it
            if (!Config.SignalingUrl.IsNullOrEmpty() && Config.ConfigFromSignaling)
            {
                // Wait for connection to signaling server and get config from it, else quit
            }
            
            // Configure server network endpoints for server and clients
            NetworkEndpoint.TryParse(Config.ServerUrl, (ushort)Config.ServerPort, out NetworkEndpoint serverEndpoint,
                NetworkFamily.Ipv4);
            BootstrappingConfig.ClientConnectAddress = serverEndpoint;
            BootstrappingConfig.ServerPort = serverEndpoint.Port;
            BootstrappingConfig.ServerListenAddress = NetworkEndpoint.AnyIpv4.WithPort(BootstrappingConfig.ServerPort);
            NetworkStreamReceiveSystem.DriverConstructor = new NetCodeDriverConstructor();
            //Debug.Log($"Servers listening on {BootstrappingConfig.ServerListenAddress}");
            //Debug.Log($"Clients connecting on {BootstrappingConfig.ClientConnectAddress}");

            // Start worlds based on config
            // Streamed guest
            if (Config.MultiplayStreamingRole == MultiplayStreamingRole.Guest)
            {
                var systems = new List<Type> { typeof(MultiplayInitSystem), typeof(EmulationInitSystem) };
                CreateClientWorld("StreamingGuestWorld", WorldFlags.Game, systems);
            }
            
            //Client
            if (Config.PlayType == BootstrapPlayType.Client || Config.PlayType == BootstrapPlayType.ClientAndServer)
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
                CreateClientWorld("ClientWorld", WorldFlags.GameClient, filteredClientSystems);
            }
            
            // Thin client
            if (Config.NumThinClientPlayers > 0)
            {
                var thinClientSystems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.ThinClientSimulation);
                // Disable the default NetCode world configuration
                var filteredThinClientSystems = new List<Type>();
                foreach (var system in thinClientSystems)
                {
                    if(system.Name == "ConfigureThinClientWorldSystem" || system.Name == "ConfigureClientWorldSystem")
                        continue;
                    filteredThinClientSystems.Add(system);
                }
                for (var i = 0; i < Config.NumThinClientPlayers; i++)
                {
                    CreateClientWorld("ThinClientWorld", WorldFlags.GameThinClient, filteredThinClientSystems);
                }
            }
            
            // Server
            if (Config.PlayType == BootstrapPlayType.Server || Config.PlayType == BootstrapPlayType.ClientAndServer)
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
                CreateServerWorld("ServerWorld", WorldFlags.GameServer, filteredServerSystems );
            }

            return true;
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
#if UNITY_SERVER && !UNITY_EDITOR
            throw new NotImplementedException();
#else
            var world = new World(name, flags);

            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
            
#if UNITY_DOTSRUNTIME
            CustomDOTSWorlds.AppendWorldToClientTickWorld(world);
#else
            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);
#endif

            if (World.DefaultGameObjectInjectionWorld == null)
                World.DefaultGameObjectInjectionWorld = world;

            return world;
#endif
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
#if UNITY_CLIENT && !UNITY_SERVER && !UNITY_EDITOR
            throw new NotImplementedException();
#else

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
#endif
        }

        public enum BootstrapPlayType
        {
            /// <summary>
            /// The application can run as client, server or both. By default, both client and server world are created
            /// and the application can host and play as client at the same time.
            /// <para>
            /// This is the default modality when playing in the editor, unless changed by using the play mode tool.
            /// </para>
            /// </summary>
            ClientAndServer = 0,
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
            ThinClient=3
        }
    }
    
    
}


