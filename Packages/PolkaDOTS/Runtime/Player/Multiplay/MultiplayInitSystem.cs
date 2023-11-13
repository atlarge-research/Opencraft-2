using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.VisualScripting;
using UnityEngine;

namespace PolkaDOTS.Multiplay
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
        private EntityQuery _connQuery;
        private bool initialized = false;
        protected override void OnCreate()
        {
            _requestQuery = GetEntityQuery(ComponentType.ReadOnly<StreamedClientRequestConnect>());
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<NetworkId, NetworkStreamInGame>();
            _connQuery = GetEntityQuery(builder);
            initialized = false;
        }
        
        protected override void OnUpdate()
        {
            Multiplay multiplay = MultiplaySingleton.Instance;
            if (multiplay is null)
                return;


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
            else
            {
                if (!_connQuery.IsEmpty)
                {
                    // Run initialization after connection to server made
                    if (!initialized)
                    {
                        if (World.Unmanaged.IsClient() && !World.Unmanaged.IsHostClient())
                        {
                            multiplay.SetUpLocalPlayer();
                            Enabled = false;
                        }
                        else if (World.Unmanaged.IsHostClient())
                        {
                            multiplay.SetUpHost(World.Unmanaged.IsCloudHostClient());
                            Enabled = false;
                        }

                        initialized = true;
                    }
                }
            }
            
        }

    }
}