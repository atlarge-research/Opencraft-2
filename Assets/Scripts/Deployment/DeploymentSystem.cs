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

    public struct ConfigRPC : IRpcCommand
    {
        public int nodeID;
        // World and game details
        public int worldTypes;
        public int numThinClients;
        
        // Game server connection
        public FixedString64Bytes serverIP;
        public ushort serverPort;
        
        //  Multiplay streaming host/guest
        public FixedString64Bytes signallingIP;
        public ushort signallingPort;
        
        // Emulation
        public EmulationBehaviours emulationBehaviours;
        
        // Services?
        
        public override string ToString() =>
            $"[nodeID: { nodeID};  worldTypes: {(WorldTypes)worldTypes}; numThinClients: {numThinClients};" +
            $"emulationBehaviours: {emulationBehaviours}; ]";
        
    }

    public enum ConfigErrorType
    {
        UnknownID,
        DuplicateID,
    }


    public struct ConfigErrorRPC : IRpcCommand
    {
        public ConfigErrorType errorType;
    }
    
    /// <summary>
    /// A component used to signal that a connection has asked for deployment configuration
    /// </summary>
    public struct ConfigurationSent : IComponentData
    {
    }
    
    /// <summary>
    /// Listens for <see cref="RequestConfigRPC"/> and responds with a <see cref="ConfigRPC"/> containing
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
            DeploymentNode? node = _deploymentGraph.GetNodeByID(Config.DeploymentID);
            if (node != null)
            {
                Debug.Log("Overriding local config from deployment graph");
                ConfigRPC cRPC = _deploymentGraph.NodeToConfigRPC(Config.DeploymentID);
                DeploymentConfigHelpers.ReadConfigRPC(cRPC);
            }
            // Setup worlds from local configuration
            GameBootstrap bootstrap = (GameBootstrap)BootstrappingConfig.BootStrapClass;
            bootstrap.SetupWorldsFromConfig();
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
                    commandBuffer.AddComponent(res, new ConfigErrorRPC{errorType = ConfigErrorType.UnknownID});
                } else if (node.Value.connected)
                {
                    Debug.LogWarning($"Received configuration request from node with already connected ID: {req.ValueRO.nodeID}");
                    commandBuffer.AddComponent(res, new ConfigErrorRPC{errorType = ConfigErrorType.DuplicateID});
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
                    ConfigRPC cRPC = _deploymentGraph.NodeToConfigRPC(nodeID);
                    commandBuffer.AddComponent(res, cRPC);
                }
                
                // Mark we have a response for request, and destroy the request
                commandBuffer.AddComponent(res, new SendRpcCommandRequest { TargetConnection = sourceConn });
                commandBuffer.DestroyEntity(reqEntity);
            }
            commandBuffer.Playback(EntityManager);
        }

    }
    
    /// <summary>
    /// Sends <see cref="RequestConfigRPC"/> and uses the configuration in the response <see cref="ConfigRPC"/>
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
            
            // Handle received configuration answer RPC
            bool configurationReceived = false;
            foreach (var (reqSrc, configRPC, reqEntity) in SystemAPI
                         .Query<RefRO<ReceiveRpcCommandRequest>, RefRO<ConfigRPC>>()
                         .WithEntityAccess())
            {
                var connection = connectionLookup[reqSrc.ValueRO.SourceConnection];
                NetworkEndpoint remoteEndpoint = netDriver.GetRemoteEndPoint(connection);
                Debug.Log($"Received configuration answer from {remoteEndpoint}");
                configurationReceived = true;
                ConfigRPC cRPC = configRPC.ValueRO;
                DeploymentConfigHelpers.ReadConfigRPC(cRPC);
                Debug.Log($"cRPC with worldtype {(WorldTypes)cRPC.worldTypes} results in config: worldTypes- {Config.playTypes};" +
                          $" streamingRole- {Config.multiplayStreamingRoles}; emulationType-{Config.EmulationType}");
                // Setup worlds from received configuration and stop this system
                GameBootstrap bootstrap = (GameBootstrap)BootstrappingConfig.BootStrapClass;
                bootstrap.SetupWorldsFromConfig();
                commandBuffer.DestroyEntity(reqEntity);
            }
            commandBuffer.Playback(EntityManager);
            if (configurationReceived)
                Enabled = false;
        }

    }

    [BurstCompile]
    internal static class DeploymentConfigHelpers
    {
        public static void ReadConfigRPC(ConfigRPC cRPC)
        {
            if (cRPC.nodeID != Config.DeploymentID)
            {
                Debug.LogWarning(
                    $"Configuration answer has wrong ID: {cRPC.nodeID} for node {Config.DeploymentID}");
            }

            //Override playtype and streaming role
            WorldTypes worldTypes = (WorldTypes)cRPC.worldTypes;
            Debug.Log($"cRPC has worldtypes {worldTypes}");
            if ((worldTypes & WorldTypes.Client) == WorldTypes.Client)
            {

                if ((worldTypes & WorldTypes.Server) == WorldTypes.Server)
                    Config.playTypes = GameBootstrap.BootstrapPlayTypes.ClientAndServer;
                else
                    Config.playTypes = GameBootstrap.BootstrapPlayTypes.Client;
            }
            else if ((worldTypes & WorldTypes.Server) == WorldTypes.Server)
                Config.playTypes = GameBootstrap.BootstrapPlayTypes.Server;
            
            if ((worldTypes & WorldTypes.StreamGuest) == WorldTypes.StreamGuest)
            {
                Config.playTypes = GameBootstrap.BootstrapPlayTypes.StreamedClient;
                Config.multiplayStreamingRoles = MultiplayStreamingRoles.Guest;
            }
            if ((worldTypes & WorldTypes.StreamHost) == WorldTypes.StreamHost)
                Config.multiplayStreamingRoles = MultiplayStreamingRoles.Host;

            // Game server URL details
            FixedString64Bytes serverIP = cRPC.serverIP;
            if (!serverIP.IsEmpty)
                Config.ServerUrl = serverIP.ToString();
            ushort serverPort = cRPC.serverPort;
            if (serverPort != 0)
                Config.ServerPort = serverPort;

            // Signalling server URL details
            FixedString64Bytes signalingIP = cRPC.signallingIP;
            if (!signalingIP.IsEmpty)
                Config.SignalingUrl = signalingIP.ToString();
            ushort signalingPort = cRPC.signallingPort;
            if (signalingPort != 0)
                Config.SignalingPort = signalingPort;

            // Number of thin clients
            int numThinClients = cRPC.numThinClients;
            if (numThinClients > 0)
                Config.NumThinClientPlayers = numThinClients;

            // Player emulation
            Config.EmulationType = cRPC.emulationBehaviours;

        }
    }
}