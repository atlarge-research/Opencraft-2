using Opencraft.Deployment;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.VisualScripting;
using UnityEngine;

namespace Opencraft.Player.Multiplay
{
    /// <summary>
    /// A component that can be added to a new entity to connect a streamed client/>
    /// </summary>
    public struct StreamedClientRequestConnect : IComponentData
    {
        /// <summary>
        /// The signaling server url.
        /// </summary>
        public FixedString512Bytes url;
    }

    
    // ECS System wrapper around Multiplay class that handles render streaming connections
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class MultiplayInitSystem : SystemBase
    {
        private EntityQuery _requestQuery;
        private bool initialized = false;
        protected override void OnCreate()
        {
            _requestQuery = GetEntityQuery(ComponentType.ReadOnly<StreamedClientRequestConnect>());
            initialized = false;
        }
        
        protected override void OnUpdate()
        {
            Multiplay multiplay = MultiplaySingleton.Instance;
            if (multiplay.IsUnityNull())
                return;
            
            // Only run initialization code once
            if (!initialized)
            {
                Debug.Log("Initializing multiplay!");
                multiplay.InitSettings();
                if (World.Unmanaged.IsStreamedClient() && Config.SwitchToStreamDuration > 0)
                {
                    // Let the client world handle the switch.
                    initialized = true;
                }
                else if (Config.multiplayStreamingRoles == MultiplayStreamingRoles.Disabled ||
                    ((Config.multiplayStreamingRoles == MultiplayStreamingRoles.Guest ) && Config.SwitchToStreamDuration > 0 ))
                {
                    multiplay.SetUpLocalPlayer();
                }
                else if (World.Unmanaged.IsHostClient())
                {
                    multiplay.SetUpHost();
                }
                
                // If we won't be switching streaming mode later, disable this system
                if(Config.SwitchToStreamDuration <= 0 )
                    Enabled = false;
                
                initialized = true;
            }
            
            
            // Don't start multiplay system until switch to stream duration elapsed
            if ((Config.SwitchToStreamDuration > 0)  && (World.Time.ElapsedTime < Config.SwitchToStreamDuration))
            {
                return;
            }


            if (World.Unmanaged.IsStreamedClient())
            {
                // Check for connection requests
                if (!_requestQuery.IsEmpty)
                {
                    var requests = _requestQuery.ToComponentDataArray<StreamedClientRequestConnect>(Allocator.Temp);
                    if(requests.Length > 1)
                        Debug.Log($"Render streaming guest has multiple connection requests! Ignoring all but the first...");
                    multiplay.SetUpGuest(requests[0].url.ToString());
                    EntityManager.DestroyEntity(_requestQuery);
                }
            }
            
        }

    }
}