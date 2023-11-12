using System;
using Opencraft.Player.Emulated;

namespace Opencraft.Deployment
{
    [Flags]
    [Serializable]
    public enum WorldTypes
    {
        None       = 0,
        Client     = 1,
        ThinClient = 1 << 1,
        Server     = 1 << 2
        
    }
    
    [Serializable]
    public enum MultiplayStreamingRoles
    {
        Disabled,
        Host,
        CloudHost,
        Guest
    }
    
    [Serializable]
    public enum ServiceFilterType
    {
        Includes,
        Excludes,
        Only,
    }
    
    [Serializable]
    public enum InitializationMode
    {
        Create,
        Start,
        Connect
    }
    
    /// <summary>
    /// Represents a world, each node can contain many worlds.
    /// </summary>
    [Serializable]
    public class WorldConfig
    {
        // Name of the world, used to uniquely identify it
        public string worldName;
        // The type of world, determines how connection is performed and what systems are loaded
        public WorldTypes worldType;
        // How this world should be initialized
        public InitializationMode initializationMode;
        // Multiplay role, determines if this world hosts thin clients, is a thin client, or is a normal client.
        public MultiplayStreamingRoles multiplayStreamingRoles;
        // The ID of the node that this world (if it is a non-streamed client) will connect to. Can be this node!
        public int serverNodeID;
        // The ID of the node that this world (if it is a streamed client) will connect to. Can be this node, but why would you do that?
        public int streamingNodeID;
        // The number of thin clients to create and connect. Only valid if worldType is ThinClient
        public int numThinClients;
        // Names of server service Types, handled according to serviceFilterType
        public string[] services;
        // How the service names are handled when instantiating this world
        public ServiceFilterType serviceFilterType;
        // The player emulation behaviour to use on a client world
        public EmulationBehaviours emulationBehaviours;
        
        public override string ToString() =>
            $"[worldType: {worldType}; multiplayStreamingRoles: {multiplayStreamingRoles}; serverNodeID: {serverNodeID}; streamingNodeID: {streamingNodeID};" +
            $"numThinClients: {numThinClients}; services: {services}; serviceFilterType: {serviceFilterType}; emulationBehaviours: {emulationBehaviours}; ]";
    }
}