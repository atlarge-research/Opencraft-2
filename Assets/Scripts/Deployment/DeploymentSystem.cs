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

namespace Opencraft.Deployment
{
    public struct RequestConfigRPC : IRpcCommand
    {
        public int nodeID;
    }
    
    [Serializable]
    public enum ConfigRPCActions
    {
        Initialize,
        InitializeAndConnect,
        Connect,
        Disconnect,
        Destroy
    }

    public struct DeploymentConfigRPC : IRpcCommand
    {
        public int nodeID;
        
        //What action to apply to these world types
        public ConfigRPCActions action;
        
        public WorldTypes worldType;
        
        public MultiplayStreamingRoles multiplayStreamingRoles;
        
        // Game server connection
        public FixedString64Bytes serverIP;
        public ushort serverPort;
        
        //  Multiplay streaming host/guest
        public FixedString64Bytes signallingIP;
        public ushort signallingPort;
        
        public int numThinClients;
        
        // Names of server service Types, handled according to serviceFilterType
        // public string[] services;
        // How the service names are handled when instantiating this world
        // public ServiceFilterType serviceFilterType;
        // The player emulation behaviour to use on a client world
        public EmulationBehaviours emulationBehaviours;

        /*public override string ToString() =>
            $"[nodeID: { nodeID};  worldTypes: {(WorldTypes)worldTypes}; numThinClients: {numThinClients};" +
            $"emulationBehaviours: {emulationBehaviours}; ]";*/
    }

    [Serializable]
    public enum ConfigErrorType
    {
        UnknownID,
        DuplicateID,
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
        protected override void OnCreate()
        {
            _deploymentGraph = new DeploymentGraph();
            // Check if deployment graph contains configuration for this local node
            GameBootstrap bootstrap = (GameBootstrap)BootstrappingConfig.BootStrapClass;
            DeploymentNode? node = _deploymentGraph.GetNodeByID(Config.DeploymentID);
            if (node != null)
            {
                Debug.Log("Overriding local config from deployment graph");
                List<DeploymentConfigRPC> cRPCs = _deploymentGraph.NodeToConfigRPCs(Config.DeploymentID);
                foreach (var cRPC in cRPCs)
                {
                    DeploymentConfigHelpers.HandleDeploymentConfigRPC(bootstrap, cRPC, out NativeList<WorldUnmanaged> newWorlds);
                    // Should not need to use the authoring scene loader as all worlds will be created in the first tick
                }
            }
            else
            {
                // Setup worlds from local configuration
                bootstrap.SetupWorldsFromLocalConfig();
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
                
                var res = commandBuffer.CreateEntity();
                int nodeID = req.ValueRO.nodeID;
                DeploymentNode? node = _deploymentGraph.GetNodeByID(nodeID);
                Debug.Log($"Got configuration request for node with ID {nodeID}");
                // Check request validity
                if (node == null)
                {
                    Debug.LogWarning($"Received configuration request from node with unknown ID: {req.ValueRO.nodeID}");
                    commandBuffer.AddComponent(res, new ConfigErrorRPC{nodeID = nodeID, errorType = ConfigErrorType.UnknownID});
                    commandBuffer.AddComponent(res, new SendRpcCommandRequest { TargetConnection = sourceConn });
                } else if (node.Value.connected)
                {
                    Debug.LogWarning($"Received configuration request from node with already connected ID: {req.ValueRO.nodeID}");
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
                        Debug.LogWarning($"Received config request for node {nodeID} from endpoint {remoteEndpoint}," +
                                         $"even though this node is configured to be at endpoint {_deploymentGraph.GetEndpoint(nodeID)}");
                        // should we exit here?
                    }
                    _deploymentGraph.SetEndpoint(nodeID, remoteEndpoint);
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
                
                // Destroy the request
                commandBuffer.DestroyEntity(reqEntity);
            }
            
            // Handle received configuration error RPC
            foreach (var (reqSrc, errorRPC, reqEntity) in SystemAPI
                         .Query<RefRO<ReceiveRpcCommandRequest>, RefRO<ConfigErrorRPC >>()
                         .WithEntityAccess())
            {
                Debug.LogWarning($"Received configuration error response of type: {errorRPC.ValueRO.errorType} from node with ID {errorRPC.ValueRO.nodeID}");
                commandBuffer.DestroyEntity(reqEntity);
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
        [BurstCompile]
        protected override void OnCreate()
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<NetworkId>();
            RequireForUpdate(GetEntityQuery(builder));
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
                Debug.LogWarning($"Received configuration error response of type: {errorRPC.ValueRO.errorType}");
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
                
                Debug.Log($"Received configuration {cRPC.action} RPC on worldType {cRPC.worldType} from {remoteEndpoint}");
                
                GameBootstrap bootstrap = (GameBootstrap)BootstrappingConfig.BootStrapClass;
                DeploymentConfigHelpers.HandleDeploymentConfigRPC(bootstrap, cRPC, out NativeList<WorldUnmanaged> newWorlds);
                
                if(!newWorlds.IsEmpty) 
                    GenerateAuthoringSceneLoadRequests(commandBuffer, ref newWorlds);
                
                commandBuffer.DestroyEntity(reqEntity);
            }
            commandBuffer.Playback(EntityManager);

        }

        private void GenerateAuthoringSceneLoadRequests(EntityCommandBuffer ecb, ref NativeList<WorldUnmanaged> newWorlds)
        {
            foreach (var world in newWorlds)
            {
                if (world.IsClient() || world.IsServer() || world.IsThinClient())
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
        public static void HandleDeploymentConfigRPC(GameBootstrap bootstrap, DeploymentConfigRPC cRPC, out NativeList<WorldUnmanaged> newWorlds)
        {
            newWorlds = new NativeList<WorldUnmanaged>(16, Allocator.Temp);
            if (cRPC.worldType == WorldTypes.None)
            {
                Debug.LogWarning($"Received deployment config RPC with no worldtype!");
                return;
            }
            GameBootstrap.BootstrapPlayTypes playTypes = GameBootstrap.BootstrapPlayTypes.ServerAndClient;
            if (cRPC.worldType == WorldTypes.Client)
                playTypes = GameBootstrap.BootstrapPlayTypes.Client;
            if (cRPC.worldType == WorldTypes.Server)
                playTypes = GameBootstrap.BootstrapPlayTypes.Server;
            if (cRPC.worldType == WorldTypes.ThinClient)
                playTypes = GameBootstrap.BootstrapPlayTypes.ThinClient;

            if (cRPC.action == ConfigRPCActions.InitializeAndConnect)
            {
                bootstrap.SetBootStrapConfig(cRPC.serverIP.ToString(), cRPC.serverPort);
                bootstrap.SetupWorlds(cRPC.multiplayStreamingRoles, playTypes, ref newWorlds, cRPC.numThinClients, autoConnect: true );
            }
            else if (cRPC.action == ConfigRPCActions.Initialize)
            {
                bootstrap.SetBootStrapConfig(cRPC.serverIP.ToString(), cRPC.serverPort);
                bootstrap.SetupWorlds(cRPC.multiplayStreamingRoles, playTypes, ref newWorlds, cRPC.numThinClients, autoConnect: false );
            }
            else
            {
                Debug.LogWarning($"Received unsupported configuration action {cRPC.action}");
            }
        }
    }
}