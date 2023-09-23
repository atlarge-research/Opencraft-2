using System;
using System.Collections.Generic;
using System.Linq;
using Opencraft.Bootstrap;
using Opencraft.Player.Emulated;
using Opencraft.Player.Multiplay;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
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
        /// Creates and fills a list of <see cref="DeploymentConfigRPC"/> from a Node.
        /// </summary>
        /// <param name="nodeID"> The ID of the node to create ConfigRPC from</param>
        /// <returns></returns>
        public List<DeploymentConfigRPC> NodeToConfigRPCs(int nodeID)
        {
            List<DeploymentConfigRPC> configRpcs = new List<DeploymentConfigRPC>();
            if (NodeExists(nodeID))
            {
                var node = Nodes[nodeID];

                for (int i = 0; i < node.worldConfigs.Count; i++)
                {
                    DeploymentConfigRPC cRPC = new DeploymentConfigRPC();
                    cRPC.nodeID =  node.id;
                    WorldConfig worldConfig = node.worldConfigs[i];
                    
                    if (worldConfig.initializationMode == InitializationMode.Create)
                        cRPC.action = ConfigRPCActions.Initialize;
                    else
                        cRPC.action = ConfigRPCActions.InitializeAndConnect;

                    cRPC.worldType = worldConfig.worldType;
                    cRPC.multiplayStreamingRoles = worldConfig.multiplayStreamingRoles;
                    
                    if(worldConfig.serverNodeID == node.id)
                        cRPC.serverIP = "127.0.0.1";
                    else
                    if (NodeExists(worldConfig.serverNodeID))
                    {
                        var serverNode = Nodes[worldConfig.serverNodeID];
                        // Get server node address without port
                        NetworkEndpoint endpoint = serverNode.endpoint.WithPort(0);
                        string address = endpoint.Address;
                        cRPC.serverIP = address.Substring(0, address.Length - 2);
                    }
                    else
                    {
                       Debug.Log($"Node {nodeID} World {worldConfig.worldType} has nonexistent server node id: {worldConfig.serverNodeID}!");
                    }
                    // Give the same server port to all nodes
                    cRPC.serverPort = Config.ServerPort;

                    // Find and set signaling/streaming URL
                    if(worldConfig.streamingNodeID == node.id)
                        cRPC.signallingIP  = "127.0.0.1";
                    else
                    if (NodeExists(worldConfig.streamingNodeID))
                    {
                        var serverNode = Nodes[worldConfig.streamingNodeID];
                        // Get server node address without port
                        NetworkEndpoint endpoint = serverNode.endpoint.WithPort(0);
                        string address = endpoint.Address;
                        cRPC.signallingIP = address.Substring(0, address.Length - 2);
                    }
                    else
                    {
                       Debug.Log($"Node {nodeID} World {worldConfig.worldType} has nonexistent streaming node id: {worldConfig.streamingNodeID}!");
                    }
                    
                    
                    cRPC.emulationBehaviours = worldConfig.emulationBehaviours;
                    
                    configRpcs.Add(cRPC);
                }

            }
            else
            {
               Debug.Log("NodeToConfig called with nonexistent nodeID!");
            }
            
            
            return configRpcs;
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
                newNode.connected = false;
                // Node ip can either be unknown or known in advance
                if (jsonNode.nodeIP.IsNullOrEmpty())
                {
                    // IP not known yet, update when we receive communication from this node
                    newNode.endpoint = NetworkEndpoint.LoopbackIpv4; 
                }
                else
                {
                    if(NetworkEndpoint.TryParse(jsonNode.nodeIP, Config.DeploymentPort, out NetworkEndpoint endpoint,
                           NetworkFamily.Ipv4))
                    {
                        newNode.endpoint = endpoint;
                    }
                    else
                    {
                       Debug.Log($"Node deployment IP: {jsonNode.nodeIP} cannot be parsed!");
                        newNode.endpoint = NetworkEndpoint.LoopbackIpv4; 
                    }
                }
                
                // Handle worlds list
                newNode.worldConfigs = new List<WorldConfig>(jsonNode.worldConfigs);
                
                
                /* Services
                // TODO Node connections based on service dependencies!
                foreach (var serviceName in jsonNode.services)
                {
                    Type serviceType = GetTypeByName(serviceName);
                    if (serviceType.IsUnityNull())
                    {
                       Debug.Log($"Could not find service with name {serviceName}!");
                        continue;
                    }
                    if (!serviceType.IsAssignableFrom(typeof(ISystem) ) && !serviceType.IsAssignableFrom(typeof(ComponentSystemBase) ) )
                    {
                       Debug.Log($"{serviceName} is not a service!");
                        continue;
                    }
                    newNode.services.Add(serviceType);
                }*/

                Nodes.Add(newNode.id, newNode);
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
    
    
    public struct DeploymentNode : IEquatable<DeploymentNode>
    {
        // ID of the node, used for equality
        public int id;
        // Set when this node has communicated with deployment service
        public bool connected;
        // Network location of this node
        public NetworkEndpoint endpoint;
        
        // World details
        public List<WorldConfig> worldConfigs;
        
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
            $"[nodeID: {id}; endpoint: {endpoint}; worldConfig: {worldConfigs};]";
    }

}