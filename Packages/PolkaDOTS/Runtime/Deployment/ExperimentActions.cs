using System;

namespace PolkaDOTS.Deployment
{
   
    /// <summary>
    /// Specified set of world actions to perform on node with matching id
    /// </summary>
    [Serializable]
    public class NodeAction
    {
        public int nodeID;
        public string[] worldNames;
        public WorldAction[] actions;
    }
    
    [Serializable]
    public enum WorldAction
    {
        Stop,
        Start,
        Connect,
    }
}