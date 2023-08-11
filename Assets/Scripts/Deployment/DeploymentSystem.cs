using Opencraft.Bootstrap;
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
        // TODO
        public int nodeID;
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
                    commandBuffer.AddComponent(res, new ConfigRPC{nodeID = nodeID});
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
    public partial class DeploymentReceiveSystem : SystemBase
    {
        protected override void OnCreate()
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<NetworkId>();
            RequireForUpdate(GetEntityQuery(builder));
        }

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
            
            // Handle received configuration answer RPC
            foreach (var (reqSrc, configRPC, reqEntity) in SystemAPI
                         .Query<RefRO<ReceiveRpcCommandRequest>, RefRO<ConfigRPC>>()
                         .WithEntityAccess())
            {
                var connection = connectionLookup[reqSrc.ValueRO.SourceConnection];
                NetworkEndpoint remoteEndpoint = netDriver.GetRemoteEndPoint(connection);
                Debug.Log($"Received configuration answer from {remoteEndpoint}");
                if (configRPC.ValueRO.nodeID != Config.DeploymentID)
                {
                    Debug.LogWarning($"Configuration answer has wrong ID: {configRPC.ValueRO.nodeID} for node {Config.DeploymentID}");
                }
                // TODO setup local worlds
                commandBuffer.DestroyEntity(reqEntity);
            }
            commandBuffer.Playback(EntityManager);

            
        }

    }
}