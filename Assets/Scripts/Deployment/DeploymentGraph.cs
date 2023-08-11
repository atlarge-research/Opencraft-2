using System;
using System.Collections.Generic;
using System.Linq;
using Opencraft.Bootstrap;
using Unity.Collections;
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
                newNode.isClient = jsonNode.isClient;
                newNode.isThinClient = jsonNode.isThinClient;
                newNode.isServer = jsonNode.isServer;
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
        
        // Network location of this node
        public NetworkEndpoint endpoint;
        // Client type
        public bool isClient;
        public bool isThinClient;
        // Server type
        public bool isServer;
        // Server services
        public List<Type> services;
        
        // Connected
        public bool connected;
        
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
    }

}