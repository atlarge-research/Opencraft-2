using System;
using System.Collections.Generic;
using Opencraft.Bootstrap;
using Opencraft.Player.Emulated;
using Opencraft.Player.Multiplay;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;
using static Opencraft.Deployment.ConfigRPCActions;

namespace Opencraft.Deployment
{
    public struct RequestConfigRPC : IRpcCommand
    {
        public int nodeID;
    }
    
    [Flags]
    [Serializable]
    public enum ConfigRPCActions
    {
        Create = 1, // Creates the structures for a world but does not start running it
        Start = 1 << 1, // Adds a created world to the player running world list
        Connect = 1 << 2, // Creates a connection request in a running world
    }

    public struct DeploymentConfigRPC : IRpcCommand
    {
        public int nodeID;
        
        //What action to apply to these world types
        public ConfigRPCActions action;
        
        public FixedString64Bytes worldName;
        
        public WorldTypes worldType;
        
        public MultiplayStreamingRoles multiplayStreamingRoles;
        
        // Game server connection
        public FixedString64Bytes serverIP;
        public ushort serverPort;
        
        //  Multiplay streaming host/guest
        public FixedString64Bytes signallingIP;
        
        public int numThinClients;
        
        // Names of server service Types, handled according to serviceFilterType
        // public string[] services;
        // How the service names are handled when instantiating this world
        // public ServiceFilterType serviceFilterType;
        // The player emulation behaviour to use on a client world
        //public EmulationBehaviours emulationBehaviours;

        /*public override string ToString() =>
            $"[nodeID: { nodeID};  worldTypes: {(WorldTypes)worldTypes}; numThinClients: {numThinClients};" +
            $"emulationBehaviours: {emulationBehaviours}; ]";*/
    }

    public struct WorldActionRPC : IRpcCommand
    {
        public int nodeID;
        public FixedString64Bytes worldName;
        public WorldAction action;
        public FixedString64Bytes connectionIP;
        public ushort connectionPort;
    }

    [Serializable]
    public enum ConfigErrorType
    {
        UnknownID,
        DuplicateID,
        UnknownWorld,
    }


    public struct ConfigErrorRPC : IRpcCommand
    {
        public int nodeID;
        public ConfigErrorType errorType;
    }
    
    /// <summary>
    /// A component used to signal that a connection has asked for deployment configuration
    /// </summary>
    public struct ConfigurationSent : IComponentData
    {
    }
    
    /// <summary>
    /// Listens for <see cref="RequestConfigRPC"/> and responds with one or more <see cref="DeploymentConfigRPC"/> containing
    /// configuration set in the <see cref="DeploymentGraph"/> 
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Disabled)] // Don't automatically add to worlds
    public partial class DeploymentServiceSystem : SystemBase
    {
        private DeploymentGraph _deploymentGraph;
        private bool _allNodesConnected;
        private double _startTime;
        protected override void OnCreate()
        {
            _deploymentGraph = new DeploymentGraph();
            // Check if deployment graph contains configuration for this local node
            DeploymentNode? node = _deploymentGraph.GetNodeByID(Config.DeploymentID);
            _allNodesConnected = false;
            _startTime = double.NaN;
            if (node.HasValue)
            {
                Debug.Log("Overriding local config from deployment graph");
                _deploymentGraph.SetConnected(Config.DeploymentID);
                List<DeploymentConfigRPC> cRPCs = _deploymentGraph.NodeToConfigRPCs(Config.DeploymentID);
                foreach (var cRPC in cRPCs)
                {
                    DeploymentConfigHelpers.HandleDeploymentConfigRPC(cRPC, NetworkEndpoint.LoopbackIpv4, out NativeList<WorldUnmanaged> newWorlds);
                    // Should not need to use the authoring scene loader as all worlds will be created in the first tick
                }
            }
            else
            {
                // Setup worlds from local configuration
                BootstrapInstance.instance.SetupWorldsFromLocalConfig();
            }
        }
        

        protected override void OnUpdate()
        {
            // Answer received configuration request RPCs
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            var connectionLookup = GetComponentLookup<NetworkStreamConnection>();
            var netDriver = SystemAPI.GetSingleton<NetworkStreamDriver>();

            foreach (var (reqSrc, req, reqEntity) in SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<RequestConfigRPC>>()
                         .WithEntityAccess())
            {
                var sourceConn = reqSrc.ValueRO.SourceConnection;
                commandBuffer.AddComponent<ConfigurationSent>(sourceConn); // Mark this connection as request received
                //commandBuffer.AddComponent<NetworkStreamInGame>(sourceConn);
                
                var res = commandBuffer.CreateEntity();
                int nodeID = req.ValueRO.nodeID;
                DeploymentNode? node = _deploymentGraph.GetNodeByID(nodeID);
                Debug.Log($"Got configuration request for node with ID {nodeID}");
                // Check request validity
                if (node == null)
                {
                    Debug.Log($"Received configuration request from node with unknown ID: {req.ValueRO.nodeID}");
                    commandBuffer.AddComponent(res, new ConfigErrorRPC{nodeID = nodeID, errorType = ConfigErrorType.UnknownID});
                    commandBuffer.AddComponent(res, new SendRpcCommandRequest { TargetConnection = sourceConn });
                } else if (node.Value.connected)
                {
                    Debug.Log($"Received configuration request from node with already connected ID: {req.ValueRO.nodeID}");
                    commandBuffer.AddComponent(res, new ConfigErrorRPC{nodeID = nodeID, errorType = ConfigErrorType.DuplicateID});
                    commandBuffer.AddComponent(res, new SendRpcCommandRequest { TargetConnection = sourceConn });
                }
                else
                {
                    // Mark we have received a request from this node
                    _deploymentGraph.SetConnected(nodeID);
                    // Get the source network endpoint of the node
                    var connection = connectionLookup[sourceConn];
                    NetworkEndpoint remoteEndpoint = netDriver.GetRemoteEndPoint(connection);
                    if (!_deploymentGraph.CompareEndpoint(nodeID, remoteEndpoint))
                    {
                        Debug.Log($"Received config request for node {nodeID} from endpoint {remoteEndpoint}," +
                                         $"even though this node is configured to be at endpoint {_deploymentGraph.GetEndpoint(nodeID)}");
                        // should we exit here?
                    }
                    _deploymentGraph.SetEndpoint(nodeID, remoteEndpoint, sourceConn);
                    // Build response with configuration details
                    List<DeploymentConfigRPC> cRPCs = _deploymentGraph.NodeToConfigRPCs(nodeID);
                    // Create a set of configuration RPCs
                    foreach (var cRPC in cRPCs)
                    {
                        commandBuffer.AddComponent(res, cRPC);
                        commandBuffer.AddComponent(res, new SendRpcCommandRequest { TargetConnection = sourceConn });
                        res = commandBuffer.CreateEntity();
                    }
                }
                // Check if all nodes connected
                _allNodesConnected = _deploymentGraph.CheckAllNodesConnected();
                if (_allNodesConnected && double.IsNaN(_startTime))
                {
                    _startTime = World.Time.ElapsedTime;
                }
                // Destroy the request
                commandBuffer.DestroyEntity(reqEntity);
            }
            
            // Handle received configuration error RPC
            foreach (var (reqSrc, errorRPC, reqEntity) in SystemAPI
                         .Query<RefRO<ReceiveRpcCommandRequest>, RefRO<ConfigErrorRPC >>()
                         .WithEntityAccess())
            {
                Debug.Log($"Received configuration error response of type: {errorRPC.ValueRO.errorType} from node with ID {errorRPC.ValueRO.nodeID}");
                commandBuffer.DestroyEntity(reqEntity);
            }
            
            // Handle experiment control events
            for (int experimentID = 0; experimentID < _deploymentGraph.ExperimentActionList.Count; experimentID++)
            {
                // Wait for all nodes to connect to begin experiment
                if (!_allNodesConnected)
                    break;
                
                var experimentAction = _deploymentGraph.ExperimentActionList[experimentID];
                double elapsed = World.Time.ElapsedTime - _startTime;
                if (elapsed > Config.Duration)
                {
                    Debug.Log($"[{DateTime.Now.TimeOfDay}]: Experiment duration of {Config.Duration} seconds elapsed! Exiting.");
                    BootstrapInstance.instance.ExitGame();
                }
                if (elapsed > experimentAction.delay && !experimentAction.done)
                {
                    var nodeActions = experimentAction.deploymentNodeActions;

                    foreach (var nodeAction in nodeActions)
                    {
                        // Do the action
                        DeploymentNode node = _deploymentGraph.GetNodeByID(nodeAction.nodeID).Value;
                        if (!node.connected)
                        {
                            Debug.LogWarning($"NodeAction failed, node {node.id} has not connected!");
                            continue;
                        }
                        for (int i = 0; i < nodeAction.worldActions.Length; i++)
                        {
                            WorldConfig worldConfig = node.worldConfigs[nodeAction.worldConfigID[i]];
                            string worldName = worldConfig.worldName;
                            WorldAction action = nodeAction.worldActions[i];

                            FixedString64Bytes connectionURL = new FixedString64Bytes("127.0.0.1");
                            ushort connectionPort = 7979;
                            
                            if (action == WorldAction.Connect)
                            {
                                // Streamed client world
                                if (worldConfig.worldType == WorldTypes.Client && worldConfig.multiplayStreamingRoles ==
                                    MultiplayStreamingRoles.Guest)
                                {
                                    DeploymentNode? targetNode =
                                        _deploymentGraph.GetNodeByID(worldConfig.streamingNodeID);
                                    if (!targetNode.HasValue)
                                    {
                                        Debug.LogWarning($"Target node for streaming {worldConfig.streamingNodeID} does not exist!");
                                    }
                                    else
                                        connectionURL = targetNode.Value.endpoint;

                                    connectionPort = 7981;
                                }
                                // Client world
                                else if (worldConfig.worldType == WorldTypes.Client && worldConfig.multiplayStreamingRoles !=
                                           MultiplayStreamingRoles.Guest)
                                {
                                    DeploymentNode? targetNode =
                                        _deploymentGraph.GetNodeByID(worldConfig.serverNodeID);
                                    if (!targetNode.HasValue)
                                    {
                                        Debug.LogWarning($"Target node for game {worldConfig.serverNodeID} does not exist!");
                                    }
                                    else
                                        connectionURL = targetNode.Value.endpoint;
                                    
                                }
                                // Server world
                                else if (worldConfig.worldType == WorldTypes.Server)
                                {
                                    // defaults work for now
                                }
                            }

                            WorldActionRPC wa = new WorldActionRPC
                            {
                                nodeID = node.id, action = action, worldName = worldName, connectionIP = connectionURL, connectionPort = connectionPort
                            };
                            if (node.id == Config.DeploymentID)
                            {
                                // If the action is for the local node, handle it 
                                if (!DeploymentConfigHelpers.HandleWorldAction(wa, NetworkEndpoint.LoopbackIpv4))
                                {
                                    Debug.LogWarning($"World {wa.worldName} not found!");
                                }
                            }
                            else
                            {
                                var res = commandBuffer.CreateEntity();
                                commandBuffer.AddComponent(res, wa);
                                commandBuffer.AddComponent(res,
                                    new SendRpcCommandRequest { TargetConnection = node.sourceConnection });
                            }
                        }
                        
                    }

                    experimentAction.done = true;
                    _deploymentGraph.ExperimentActionList[experimentID] = experimentAction;
                }
            }
            
            commandBuffer.Playback(EntityManager);
        }

    }

    /// <summary>
    /// Sends <see cref="RequestConfigRPC"/> and uses the configuration in the response <see cref="DeploymentConfigRPC"/>
    /// to create local worlds
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Disabled)] // Don't automatically add to worlds
    [BurstCompile]
    public partial class DeploymentReceiveSystem : SystemBase
    {
        private double _startTime;
        private bool _configReceived;
        [BurstCompile]
        protected override void OnCreate()
        {
            //var builder = new EntityQueryBuilder(Allocator.Temp)
            //    .WithAll<NetworkId>();
            //RequireForUpdate(GetEntityQuery(builder));
            _startTime = double.NaN;
            _configReceived = false;
        }

        [BurstCompile]
        protected override void OnUpdate()
        {
            var connectionLookup = GetComponentLookup<NetworkStreamConnection>();
            var netDriver = SystemAPI.GetSingleton<NetworkStreamDriver>();
            
            // Send configuration request RPC
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (netID, entity) in SystemAPI.Query<RefRO<NetworkId>>().WithEntityAccess().WithNone<ConfigurationSent>())
            {
                commandBuffer.AddComponent<ConfigurationSent>(entity);
                //commandBuffer.AddComponent<NetworkStreamInGame>(entity);
                var req = commandBuffer.CreateEntity();
                commandBuffer.AddComponent(req, new RequestConfigRPC{nodeID = Config.DeploymentID});
                commandBuffer.AddComponent(req, new SendRpcCommandRequest { TargetConnection = entity });
                Debug.Log($"Sending configuration request.");
            }
            
            // Handle received configuration error RPC
            foreach (var (reqSrc, errorRPC, reqEntity) in SystemAPI
                         .Query<RefRO<ReceiveRpcCommandRequest>, RefRO<ConfigErrorRPC >>()
                         .WithEntityAccess())
            {
                Debug.Log($"Received configuration error response of type: {errorRPC.ValueRO.errorType}");
                commandBuffer.DestroyEntity(reqEntity);
            }
            
            // Handle all received configuration RPCs
            foreach (var (reqSrc, configRPC, reqEntity) in SystemAPI
                         .Query<RefRO<ReceiveRpcCommandRequest>, RefRO<DeploymentConfigRPC>>()
                         .WithEntityAccess())
            {
                var connection = connectionLookup[reqSrc.ValueRO.SourceConnection];
                NetworkEndpoint remoteEndpoint = netDriver.GetRemoteEndPoint(connection);
                
                DeploymentConfigRPC cRPC = configRPC.ValueRO;
                
                Debug.Log($"[{DateTime.Now.TimeOfDay}]: Received configuration {cRPC.action} RPC on world {cRPC.worldName} with type {cRPC.worldType}:{cRPC.multiplayStreamingRoles} from {remoteEndpoint}");
                // Mark when we receive the config requests
                _startTime = World.Time.ElapsedTime;
                _configReceived = true;
                
                DeploymentConfigHelpers.HandleDeploymentConfigRPC(cRPC, remoteEndpoint, out NativeList<WorldUnmanaged> newWorlds);
                
                if(!newWorlds.IsEmpty) 
                    GenerateAuthoringSceneLoadRequests(commandBuffer, ref newWorlds);
                
                commandBuffer.DestroyEntity(reqEntity);
            }
            
            // Handle all received experiment WorldActionRPCs
            foreach (var (reqSrc, worldActionRPC, reqEntity) in SystemAPI
                         .Query<RefRO<ReceiveRpcCommandRequest>, RefRO<WorldActionRPC>>()
                         .WithEntityAccess())
            {
                var connection = connectionLookup[reqSrc.ValueRO.SourceConnection];
                NetworkEndpoint remoteEndpoint = netDriver.GetRemoteEndPoint(connection);
                
                WorldActionRPC wRPC = worldActionRPC.ValueRO;
                
                if (!DeploymentConfigHelpers.HandleWorldAction(wRPC, remoteEndpoint))
                {
                    Debug.Log($"World with name {wRPC.worldName} not found!");
                    Entity res = commandBuffer.CreateEntity();
                    commandBuffer.AddComponent(res, new ConfigErrorRPC{nodeID = Config.DeploymentID, errorType = ConfigErrorType.UnknownWorld});
                    commandBuffer.AddComponent(res, new SendRpcCommandRequest { TargetConnection = reqSrc.ValueRO.SourceConnection });
                }
               
                
                commandBuffer.DestroyEntity(reqEntity);
            }
            
            commandBuffer.Playback(EntityManager);

            if (_configReceived && (World.Time.ElapsedTime - _startTime) > Config.Duration)
            {
                Debug.Log($"[{DateTime.Now.TimeOfDay}]: Experiment duration of {Config.Duration} seconds elapsed! Exiting.");
                BootstrapInstance.instance.ExitGame();
            }

        }

        private void GenerateAuthoringSceneLoadRequests(EntityCommandBuffer ecb, ref NativeList<WorldUnmanaged> newWorlds)
        {
            foreach (var world in newWorlds)
            {
                if ((world.IsClient() || world.IsServer() || world.IsThinClient()) && !world.IsStreamedClient())
                {
                    Entity e = ecb.CreateEntity();
                    ecb.AddComponent(e, new LoadAuthoringSceneRequest{world = world});
                }
            }
        }

    }

    [BurstCompile]
    internal static class DeploymentConfigHelpers
    {
        public static void HandleDeploymentConfigRPC(DeploymentConfigRPC cRPC, NetworkEndpoint sourceConn, out NativeList<WorldUnmanaged> newWorlds)
        {
            newWorlds = new NativeList<WorldUnmanaged>(16, Allocator.Temp);
            if (cRPC.worldType == WorldTypes.None)
            {
                Debug.Log($"Received deployment config RPC with no worldtype!");
                return;
            }
            GameBootstrap.BootstrapPlayTypes playTypes = GameBootstrap.BootstrapPlayTypes.ServerAndClient;
            if (cRPC.worldType == WorldTypes.Client)
                playTypes = GameBootstrap.BootstrapPlayTypes.Client;
            if (cRPC.worldType == WorldTypes.Server)
                playTypes = GameBootstrap.BootstrapPlayTypes.Server;
            if (cRPC.worldType == WorldTypes.ThinClient)
                playTypes = GameBootstrap.BootstrapPlayTypes.ThinClient;

            bool create = (Create & cRPC.action) == Create;
            bool start = (Start & cRPC.action) == Start;
            bool connect = (Connect & cRPC.action) == Connect;

            // If the node we are connecting to is the deployment node, use its external IP rather than internal 
            if (cRPC.serverIP == "source")
            {
                string addr = sourceConn.WithPort(0).ToString();
                cRPC.serverIP = addr.Substring(0, addr.Length - 2);
                // todo remove this
                if (cRPC.serverIP == "127.0.0.1")
                {
                    cRPC.serverIP = Config.ServerUrl;
                }
            }
            if (cRPC.signallingIP == "source")
            {
                string addr = sourceConn.WithPort(0).ToString();
                cRPC.signallingIP  = addr.Substring(0, addr.Length - 2);
                // todo remove this
                if (cRPC.signallingIP  == "127.0.0.1")
                {
                    cRPC.serverIP = Config.SignalingUrl;
                }
            }
            
            if (create)
            {
                BootstrapInstance.instance.SetupWorlds(cRPC.multiplayStreamingRoles, playTypes, ref newWorlds, cRPC.numThinClients,
                    autoStart: start, autoConnect: connect,cRPC.serverIP.ToString(), cRPC.serverPort,cRPC.signallingIP.ToString(), cRPC.worldName.ToString());
            } else if (start)
            {
                BootstrapInstance.instance.StartWorlds(autoConnect: connect, cRPC.multiplayStreamingRoles, playTypes,
                    cRPC.serverIP.ToString(), cRPC.serverPort,cRPC.signallingIP.ToString());
            } else if (connect)
            {
                BootstrapInstance.instance.ConnectWorlds(cRPC.multiplayStreamingRoles, playTypes,
                    cRPC.serverIP.ToString(), cRPC.serverPort,cRPC.signallingIP.ToString());
            }
        }
        
         public static bool HandleWorldAction(WorldActionRPC wRPC, NetworkEndpoint sourceConn)
        {
            Debug.Log($"[{DateTime.Now.TimeOfDay}]: Received worldAction {wRPC.action} RPC for world {wRPC.worldName}");
            World world = null;
            foreach(var currentWorld in BootstrapInstance.instance.worlds)
            {
                if (currentWorld.Name == wRPC.worldName.ToString())
                   world = currentWorld;
            }
               
            if (world == null)
            {
                return false;
            }
            string connURL = wRPC.connectionIP.ToString();
            ushort connPort = wRPC.connectionPort;
            
            // If the node we are connecting to is the deployment node, use its external IP rather than internal 
            if (connURL == "source")
            {
                string addr = sourceConn.WithPort(0).ToString();
                connURL = addr.Substring(0, addr.Length - 2);
                Debug.Log($"'source' url converted to {connURL}");
            }
            
            switch (wRPC.action)
            {
                case WorldAction.Stop:
                    Entity exitReq = world.EntityManager.CreateEntity();
                    world.EntityManager.AddComponentData(exitReq, new ExitWorld());
                    break;
                case WorldAction.Start:
                    BootstrapInstance.instance.SetWorldToUpdating(world);
                    break;
                case WorldAction.Connect:
                    BootstrapInstance.instance.SetWorldToUpdating(world);
                    Entity connReq = world.EntityManager.CreateEntity();
                    
                    if (world.IsStreamedClient())
                    {
                        string signalingConnUrl = $"ws://{connURL}:{connPort}";
                        // todo remove this
                        if (connURL == "127.0.0.1")
                        {
                            signalingConnUrl = Config.SignalingUrl;
                        }
                        world.EntityManager.AddComponentData(connReq,
                            new StreamedClientRequestConnect{ url = new FixedString512Bytes(signalingConnUrl) });
                    } else if (world.IsClient() && !world.IsStreamedClient())
                    {
                        // todo remove this
                        if (connURL == "127.0.0.1")
                        {
                            connURL = Config.ServerUrl;
                        }
                        NetworkEndpoint.TryParse(connURL, connPort,
                            out NetworkEndpoint gameEndpoint, NetworkFamily.Ipv4);
                        Debug.Log($"Connecting client world {world.Name} to {connURL} : {connPort} = {gameEndpoint}");
                        world.EntityManager.AddComponentData(connReq,
                            new NetworkStreamRequestConnect { Endpoint = gameEndpoint });
                    } else if (world.IsServer())
                    {
                        var listenNetworkEndpoint = NetworkEndpoint.AnyIpv4.WithPort(connPort);
                        world.EntityManager.AddComponentData(connReq,
                            new NetworkStreamRequestListen { Endpoint = listenNetworkEndpoint });
                    }
                    break;
            }

            return true;
        }
    }
}