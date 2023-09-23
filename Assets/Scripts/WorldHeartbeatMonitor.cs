using System.Collections;
using System.Collections.Generic;
using Opencraft.Bootstrap;
using Unity.Entities;
using UnityEngine;

namespace Opencraft
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
            while (true)
            {
                if (BootstrapInstance.instance.worlds.Count == 0)
                {
                    break;
                }
                foreach (var world in BootstrapInstance.instance.worlds)
                {
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