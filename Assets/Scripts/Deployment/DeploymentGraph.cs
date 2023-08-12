using System;
using System.Collections.Generic;
using System.Linq;
using Opencraft.Bootstrap;
using Opencraft.Player.Emulated;
using Opencraft.Player.Multiplay;
using Unity.Entities;
using Unity.Networking.Transport;
using Unity.VisualScripting;
using UnityEngine;
using WebSocketSharp;

namespace Opencraft.Deployment
{
    /// <summary>
    /// Graph of all clients and servers used by the <see cref="DeploymentServiceSystem"/> to remotely configure them
    /// </summary>
    public class DeploymentGraph
    {
        private Dictionary<int, DeploymentNode> Nodes;
        private Dictionary<DeploymentNode, HashSet<DeploymentNode>> NodeConnections;

        public DeploymentGraph()
        {
            Nodes = new Dictionary<int, DeploymentNode>(16);
            NodeConnections = new Dictionary<DeploymentNode, HashSet<DeploymentNode>>(16);
            ParseDeploymentConfig();
        }

        public bool NodeExists(int nodeID)
        {
            return Nodes.Keys.Contains(nodeID);
        }
        
        public void SetConnected(int nodeID)
        {
            if (NodeExists(nodeID))
            {
                var deploymentNode = Nodes[nodeID];
                deploymentNode.connected = true;
            }
        }
        
        public void SetEndpoint(int nodeID, NetworkEndpoint endpoint)
        {
            if (NodeExists(nodeID))
            {
                var deploymentNode = Nodes[nodeID];
                deploymentNode.endpoint = endpoint;
            }
        }
        
        public NetworkEndpoint? GetEndpoint(int nodeID)
        {
            if (NodeExists(nodeID))
            {
                var deploymentNode = Nodes[nodeID];
                return deploymentNode.endpoint;
            }

            return null;
        }
        
        /// <summary>
        /// Returns false if node has pre-configured ip different than specified endpoint, true otherwise
        /// </summary> 
        public bool CompareEndpoint(int nodeID, NetworkEndpoint endpoint)
        {
            if (NodeExists(nodeID))
            {
                var deploymentNode = Nodes[nodeID];
                if (deploymentNode.endpoint.IsAny)
                    return true;
                // We ignore port equality on the server side
                return deploymentNode.endpoint.WithPort(0) == endpoint.WithPort(0);
            }

            return false;
        }
        
        public DeploymentNode? GetNodeByID(int nodeID)
        {
            if(NodeExists(nodeID))
                return Nodes[nodeID];
            return null;
        }

        /// <summary>
        /// Creates and fills a <see cref="ConfigRPC"/> from a Node.
        /// </summary>
        /// <param name="nodeID"> The ID of the node to fill the ConfigRPC</param>
        /// <returns><see cref="ConfigRPC"/></returns>
        public ConfigRPC NodeToConfigRPC(int nodeID)
        {
            ConfigRPC cRPC = new ConfigRPC();
            if (NodeExists(nodeID))
            {
                var node = Nodes[nodeID];
                cRPC.nodeID =  node.id;
                cRPC.worldTypes =  (int)node.worldTypes;
                cRPC.numThinClients =  node.numThinClients;
                // Set cRPC server ip
                if(node.serverNodeID == node.id)
                    cRPC.serverIP = "127.0.0.1";
                else
                    if (NodeExists(node.serverNodeID))
                    {
                        var serverNode = Nodes[node.serverNodeID];
                        // Get server node address without port
                        NetworkEndpoint endpoint = serverNode.endpoint.WithPort(0);
                        string address = endpoint.Address;
                        cRPC.serverIP = address.Substring(0, address.Length - 2);
                    }
                    else
                    {
                        Debug.LogWarning($"Node {nodeID} has nonexistent server node id: {node.serverNodeID}!");
                    }
                // Give the same server port to all nodes
                cRPC.serverPort = Config.ServerPort;
                // There is only one global signalling service, likely to be co-deployed with this deployment service
                if (!Config.SignalingUrl.IsNullOrEmpty())
                    cRPC.signallingIP = Config.SignalingUrl;
                else
                    cRPC.signallingIP = "";
                cRPC.signallingPort = Config.SignalingPort;
                // Player emulation
                cRPC.emulationBehaviours = node.emulationBehaviours;
                //Debug.Log($"NODE {node} -> CRPC {cRPC}");

            }
            else
            {
                Debug.LogWarning("NodeToConfig called with nonexistent nodeID!");
                cRPC.nodeID = -1;
            }
            
            
            return cRPC;
        }



        /// <summary>
        /// Constructs the deployment graph from a Json file
        /// </summary>
        private void ParseDeploymentConfig()
        {
            JsonDeploymentNode[] jsonNodes = Config.DeploymentConfig.nodes;
            for (int i = 0; i < jsonNodes.Length; i++)
            {
                JsonDeploymentNode jsonNode = jsonNodes[i];
                DeploymentNode newNode = new DeploymentNode();
                newNode.id = jsonNode.nodeID;
                
                newNode.worldTypes = WorldTypes.None;
                if (jsonNode.playTypes is GameBootstrap.BootstrapPlayTypes.Client or GameBootstrap.BootstrapPlayTypes.ClientAndServer )
                    newNode.worldTypes |= WorldTypes.Client;
                if (jsonNode.playTypes == GameBootstrap.BootstrapPlayTypes.StreamedClient || jsonNode.streamingRoles == MultiplayStreamingRoles.Guest)
                    newNode.worldTypes |= WorldTypes.StreamGuest;
                if (jsonNode.streamingRoles == MultiplayStreamingRoles.Host)
                    newNode.worldTypes |= WorldTypes.StreamHost;
                if (jsonNode.playTypes is GameBootstrap.BootstrapPlayTypes.Server or GameBootstrap.BootstrapPlayTypes.ClientAndServer )
                    newNode.worldTypes |= WorldTypes.Server;

                newNode.serverNodeID = jsonNode.serverNodeID;
                
                newNode.numThinClients = jsonNode.numThinClients;
                
                newNode.connected = false;
                // Node ip can either be unknown or known in advance
                if (jsonNode.ip.IsNullOrEmpty())
                {
                    // IP not known yet, update when we receive communication from this node
                    newNode.endpoint = NetworkEndpoint.AnyIpv4; 
                }
                else
                {
                    if(NetworkEndpoint.TryParse(jsonNode.ip, Config.DeploymentPort, out NetworkEndpoint endpoint,
                        NetworkFamily.Ipv4))
                    {
                        newNode.endpoint = endpoint;
                    }
                    else
                    {
                        Debug.LogWarning($"Node deployment IP: {jsonNode.ip} cannot be parsed!");
                        newNode.endpoint = NetworkEndpoint.AnyIpv4; 
                    }
                }
                // Services
                // TODO Node connections based on service dependencies!
                foreach (var serviceName in jsonNode.services)
                {
                    Type serviceType = GetTypeByName(serviceName);
                    if (serviceType.IsUnityNull())
                    {
                        Debug.LogWarning($"Could not find service with name {serviceName}!");
                        continue;
                    }
                    if (!serviceType.IsAssignableFrom(typeof(ISystem) ) && !serviceType.IsAssignableFrom(typeof(ComponentSystemBase) ) )
                    {
                        Debug.LogWarning($"{serviceName} is not a service!");
                        continue;
                    }
                    newNode.services.Add(serviceType);
                }
                
                // Player emulation
                newNode.emulationBehaviours = jsonNode.emulationBehaviours;

                Nodes.Add(newNode.id, newNode);
                //Debug.Log($"JSON {jsonNode} -> NODE {newNode}");
            }
        }

        // Generic solution, from https://stackoverflow.com/questions/20008503/get-type-by-name
        private static Type GetTypeByName(string name)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Reverse())
            {
                var tt = assembly.GetType(name);
                if (tt != null)
                {
                    return tt;
                }
            }

            return null;
        }
    }

    [Flags]
    [Serializable]
    public enum WorldTypes : int
    {
        None        = 0,
        Client      = 1,
        Server      = 1 << 1,
        StreamGuest = 1 << 2,
        StreamHost  = 1 << 3
    }
    
    public struct DeploymentNode : IEquatable<DeploymentNode>
    {
        // ID of the node, used for equality
        public int id;
        // Set when this node has communicated with deployment service
        public bool connected;
        
        // Network location of this node
        public NetworkEndpoint endpoint;
        // Client/Server details
        public WorldTypes worldTypes;
        public int numThinClients;

        public int serverNodeID; // What node the game will connect to
        
        // Server services
        public List<Type> services;
        
        // Player emulation
        public EmulationBehaviours emulationBehaviours;
        
        public bool Equals(DeploymentNode other)
        {
            return id == other.id;
        }

        public override bool Equals(object obj)
        {
            return obj is DeploymentNode other && Equals(other);
        }

        public override int GetHashCode()
        {
            return id;
        }
        
        public override string ToString() =>
            $"[nodeID: {id};  endpoint: { endpoint}; worldTypes: {worldTypes}; numThinClients: {numThinClients};" +
            $"serverNodeID: {serverNodeID}; services: {services}; emulationBehaviours: {emulationBehaviours}; ]";
    }

}