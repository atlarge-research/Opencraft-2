using System.Collections;
using System.Collections.Generic;
using PolkaDOTS.Bootstrap;
using PolkaDOTS.Statistics;
using Unity.Entities;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PolkaDOTS
{
    /// <summary>
    /// Periodically check if ECS worlds registered by the Bootstrap have been stopped, and remove them if they have.
    /// Necessary to allow Jobs to complete after a world stops updating.
    /// </summary>
    public class WorldHeartbeatMonitor: MonoBehaviour{
        private void Start()
        {
            StartCoroutine(WorldHeartbeat());
        }
        IEnumerator WorldHeartbeat()
        {
            List<World> toRemove = new List<World>();
            int timeOut = 5;
            while (true)
            {
                if (BootstrapInstance.instance.worlds.Count == 0)
                {
                    timeOut -= 1;
                    if (timeOut <= 0)
                    {
                        Debug.LogWarning("Heartbeat timed out with no worlds found, exiting!");  
                        if (Config.LogStats)
                            StatisticsWriterInstance.WriteStatisticsBuffer();
                
                        #if UNITY_EDITOR
                        EditorApplication.ExitPlaymode();
                        #else
                        Application.Quit();
                        #endif
                    }
                }
                else
                {
                    timeOut = 5;
                }
                
                
                for(int worldID = 0; worldID < BootstrapInstance.instance.worlds.Count; worldID++)
                {
                    var world = BootstrapInstance.instance.worlds[worldID];
                    // If the world has been stopped, kill it
                    if (world.QuitUpdate)
                    {
                        Debug.Log($"World {world.Name} has quit, disposing!");
                        world.Dispose();
                        toRemove.Add(world);
                    }
                }

                foreach (var world in toRemove)
                    BootstrapInstance.instance.worlds.Remove(world);
                
                toRemove.Clear();

                // wait for 2 seconds
                yield return new WaitForSeconds(2f);
            }
        }
    }
}