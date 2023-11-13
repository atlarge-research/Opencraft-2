using System;
using System.Collections.Generic;
using System.Linq;
using PolkaDOTS.Configuration;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.VisualScripting;
using UnityEngine;
using WebSocketSharp;

namespace PolkaDOTS.Deployment
{
    /// <summary>
    /// Graph of all clients and servers used by the <see cref="DeploymentServiceSystem"/> to remotely configure them
    /// </summary>
    public class DeploymentGraph
    {
        private Dictionary<int, DeploymentNode> Nodes;
        private Dictionary<DeploymentNode, HashSet<DeploymentNode>> NodeConnections;
        public List<DeploymentNodeExperimentAction> ExperimentActionList;

        public DeploymentGraph()
        {
            Nodes = new Dictionary<int, DeploymentNode>(16);
            NodeConnections = new Dictionary<DeploymentNode, HashSet<DeploymentNode>>(16);
            ExperimentActionList = new List<DeploymentNodeExperimentAction>(16);
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
                Nodes[nodeID] = deploymentNode;
            }
        }
        public bool CheckAllNodesConnected()
        {
            foreach (var (nodeID, node) in Nodes)
                if (!node.connected)
                    return false;

            return true;
        }
        
        public void SetEndpoint(int nodeID, NetworkEndpoint endpoint, Entity sourceConnection)
        {
            if (NodeExists(nodeID))
            {
                var deploymentNode = Nodes[nodeID];
                string addr = endpoint.WithPort(0).ToString();
                deploymentNode.endpoint = addr.Substring(0, addr.Length - 2);
                deploymentNode.sourceConnection = sourceConnection;
                Nodes[nodeID] = deploymentNode;
            }
        }
        
        public string GetEndpoint(int nodeID)
        {
            if (NodeExists(nodeID))
            {
                return Nodes[nodeID].endpoint;
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
                string addr = endpoint.WithPort(0).ToString();
                return deploymentNode.endpoint == addr.Substring(0, addr.Length-2);
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
                        cRPC.action = ConfigRPCActions.Create;
                    else if (worldConfig.initializationMode == InitializationMode.Start)
                        cRPC.action = ConfigRPCActions.Create | ConfigRPCActions.Start;
                    else if (worldConfig.initializationMode == InitializationMode.Connect)
                        cRPC.action = ConfigRPCActions.Create | ConfigRPCActions.Start | ConfigRPCActions.Connect;

                    cRPC.worldName = new FixedString64Bytes(worldConfig.worldName);
                    
                    cRPC.worldType = worldConfig.worldType;
                    cRPC.multiplayStreamingRoles = worldConfig.multiplayStreamingRoles;
                    
                    if (worldConfig.serverNodeID == node.id)
                        cRPC.serverIP = "127.0.0.1";
                    else
                    if (NodeExists(worldConfig.serverNodeID))
                    {
                        var serverNode = Nodes[worldConfig.serverNodeID];
                        // If that node is the one this system is running on, tell the remote to use our ip
                        FixedString64Bytes endpoint = serverNode.endpoint;
                        if (worldConfig.serverNodeID == Config.DeploymentID && node.id != Config.DeploymentID )
                            endpoint = "source";
                        cRPC.serverIP = endpoint;
                    }
                    else
                    {
                       Debug.Log($"Node {nodeID} World {worldConfig.worldName} has nonexistent server node id: {worldConfig.serverNodeID}!");
                    }
                    // Give the same server port to all nodes
                    cRPC.serverPort = Config.ServerPort;

                    // Find and set signaling/streaming URL
                    if(worldConfig.streamingNodeID == node.id)
                        cRPC.signallingIP  = "127.0.0.1";
                    else
                    if (NodeExists(worldConfig.streamingNodeID))
                    {
                        var streamingNode = Nodes[worldConfig.streamingNodeID];
                        
                        FixedString64Bytes endpoint = streamingNode.endpoint;
                        if (worldConfig.serverNodeID == Config.DeploymentID && node.id != Config.DeploymentID )
                            endpoint = "source";
                        
                        cRPC.signallingIP = endpoint;
                    }
                    else
                    {
                       Debug.Log($"Node {nodeID} World {worldConfig.worldName} has nonexistent streaming node id: {worldConfig.streamingNodeID}!");
                    }
                    
                    
                    //cRPC.emulationBehaviours = worldConfig.emulationBehaviours;
                    
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
                    newNode.endpoint = "127.0.0.1"; 
                }
                else
                {
                    if(NetworkEndpoint.TryParse(jsonNode.nodeIP, Config.DeploymentPort, out NetworkEndpoint endpoint,
                           NetworkFamily.Ipv4))
                    {
                        newNode.endpoint = jsonNode.nodeIP;
                    }
                    else
                    {
                       Debug.Log($"Node deployment IP: {jsonNode.nodeIP} cannot be parsed!");
                        newNode.endpoint = "127.0.0.1"; 
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
            // Check experiment action validity
            ExperimentAction[] actionsArray = Config.DeploymentConfig.experimentActions;
            foreach (ExperimentAction experimentAction in actionsArray)
            {
                DeploymentNodeExperimentAction nodeExperimentAction = new DeploymentNodeExperimentAction
                {
                    delay = experimentAction.delay,
                    deploymentNodeActions = new List<DeploymentNodeAction>(),
                    done = false
                };

                NodeAction[] nodeActions = experimentAction.actions;

                foreach (var nodeAction in nodeActions)
                {
                    DeploymentNode? deploymentNode = GetNodeByID(nodeAction.nodeID);
                    if (!deploymentNode.HasValue)
                    {
                        Debug.LogWarning($"NodeAction specified for node {nodeAction.nodeID} which does not exist!");
                        continue;
                    }
                    
                    if (nodeAction.worldNames.Length != nodeAction.actions.Length)
                    {
                        Debug.LogWarning($"NodeAction World and WorldActions are not the same length!");
                        continue;
                    }
                    
                    var worldConfigs = deploymentNode.Value.worldConfigs;
                    List<int> worldIDs = new List<int>();
                    bool worldNotFound = false;
                    foreach (string worldName in nodeAction.worldNames)
                    {
                        bool foundWorld = false;
                        for(int worldID = 0; worldID < worldConfigs.Count; worldID++)
                        {
                            var worldConfig = worldConfigs[worldID];
                            if (worldConfig.worldName == worldName)
                            {
                                foundWorld = true;
                                worldIDs.Add(worldID);
                                break;
                            }
                        }

                        if (!foundWorld)
                        {
                            Debug.LogWarning($"World {worldName} not found in Node {nodeAction.nodeID}");
                            worldNotFound = true;
                        }
                    }
                    if(worldNotFound)
                        continue;

                    // If all checks are valid, add this action to the list
                    nodeExperimentAction.deploymentNodeActions.Add(
                        new DeploymentNodeAction
                        {
                            nodeID = nodeAction.nodeID,
                            worldConfigID = worldIDs.ToArray(),
                            worldActions = nodeAction.actions
                        });
                    
                }
                
                
                ExperimentActionList.Add(nodeExperimentAction);
            }

        }

        // Generic solution, from https://stackoverflow.com/questions/20008503/get-type-by-name
        /*private static Type GetTypeByName(string name)
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
        }*/
    }
    
    
    public struct DeploymentNode : IEquatable<DeploymentNode>
    {
        // ID of the node, used for equality
        public int id;
        // Set when this node has communicated with deployment service
        public bool connected;
        
        // Source connection Entity of this node
        public Entity sourceConnection;
        
        // Network location of this node
        public string endpoint;
        
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

    public struct DeploymentNodeExperimentAction
    {
        public int delay;
        public bool done;
        public List<DeploymentNodeAction> deploymentNodeActions;

    }
    
    public struct DeploymentNodeAction
    {
        public int nodeID;
        public int[] worldConfigID;
        public WorldAction[] worldActions;

    }
    

}