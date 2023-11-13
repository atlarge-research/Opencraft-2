using PolkaDOTS.Emulation;
using PolkaDOTS.Multiplay;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PolkaDOTS
{
    /// <summary>
    /// A component used to signal that this world should quit
    /// </summary>
    public struct ExitWorld : IComponentData
    {
    }
    
    /// <summary>
    /// Run in all worlds to exit upon receiving a quit flag
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)] 
    public partial class StopWorldSystem : SystemBase
    {
        private EntityQuery exitQuery;
        protected override void OnCreate()
        {
           exitQuery = new EntityQueryBuilder(Allocator.Temp)
               .WithAllRW<ExitWorld>()
               .Build(this);
        }

        /// <summary>
        /// When Exit request received, stop updating this world. It will be removed by <see cref="WorldHeartbeatMonitor"/>
        /// </summary>
        protected override void OnUpdate()
        {
            if (!exitQuery.IsEmpty)
            {
                EntityManager.DestroyEntity(exitQuery);
                Debug.Log($"Exit world flag received on world {World.Unmanaged.Name}, setting quiting updates.");
                if (World.IsStreamedClient() || World.IsHostClient())
                {
                    // Cleanup Multiplay
                    Multiplay.Multiplay multiplay = MultiplaySingleton.Instance;
                    if (multiplay is not null)
                    {
                        multiplay.StopMultiplay();
                    }
                    // Stop emulation
                    Emulation.Emulation emulation = EmulationSingleton.Instance;
                    if (emulation is not null)
                    {
                        emulation.Pause();
                    }
                }
                World.QuitUpdate = true;
            }
        }
    }
}